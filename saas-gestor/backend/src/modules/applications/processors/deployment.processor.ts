import { Processor, Process, OnQueueActive, OnQueueCompleted, OnQueueFailed } from '@nestjs/bullmq';
import { Job } from 'bullmq';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { Deployment, DeploymentStatus } from '../entities/deployment.entity';
import { Application, ApplicationStatus } from '../entities/application.entity';
import { Build } from '../entities/build.entity';
import { Logger } from '@nestjs/common';
import { exec } from 'child_process';
import { promisify } from 'util';
import * as fs from 'fs/promises';
import * as path from 'path';
import { AnalyticsService } from '../../analytics/analytics.service';
import { MetricType } from '../../analytics/entities/analytics-metric.entity';

const execAsync = promisify(exec);

interface DeployJobData {
  deploymentId: number;
  applicationId: number;
  tenantId: number;
  buildId: number;
  deploymentType: string;
  autoRollback: boolean;
  application: {
    name: string;
    slug: string;
    domain: string;
    port: number;
    dockerImage: string;
    envVars: string;
    startCommand: string;
    healthCheckUrl: string;
  };
  build: {
    artifactPath: string;
    commitHash: string;
  };
}

@Processor('deployments')
export class DeploymentProcessor {
  private readonly logger = new Logger(DeploymentProcessor.name);

  constructor(
    @InjectRepository(Deployment)
    private deploymentRepository: Repository<Deployment>,
    @InjectRepository(Application)
    private applicationRepository: Repository<Application>,
    @InjectRepository(Build)
    private buildRepository: Repository<Build>,
    private analyticsService: AnalyticsService,
  ) {}

  @Process('deploy')
  async handleDeploy(job: Job<DeployJobData>): Promise<void> {
    const { deploymentId, applicationId, tenantId, application, build } = job.data;

    this.logger.log(`Starting deployment ${deploymentId} for ${application.name}`);

    const deployment = await this.deploymentRepository.findOne({
      where: { id: deploymentId },
    });

    if (!deployment) {
      throw new Error(`Deployment ${deploymentId} not found`);
    }

    // Track deployment start
    await this.analyticsService.trackMetric(
      tenantId,
      MetricType.DEPLOYMENT_STARTED,
      { deploymentId, buildId: job.data.buildId },
      applicationId,
    );

    // Update status to running
    deployment.status = DeploymentStatus.RUNNING;
    deployment.stage = 'preparing';
    deployment.startedAt = new Date();
    await this.deploymentRepository.save(deployment);

    const logs: string[] = [];
    const addLog = (message: string) => {
      const timestamp = new Date().toISOString();
      const logLine = `[${timestamp}] ${message}`;
      logs.push(logLine);
      this.logger.log(`[Deployment ${deploymentId}] ${message}`);
    };

    try {
      addLog(`Starting deployment for ${application.name}`);
      addLog(`Deployment type: ${job.data.deploymentType}`);
      addLog(`Build artifact: ${build.artifactPath}`);

      // Determine color for blue-green deployment
      const deployedColor = await this.getNextColor(applicationId);
      deployment.deployedColor = deployedColor;
      addLog(`Deploying to ${deployedColor} environment`);

      // Stage: Prepare Docker container
      deployment.stage = 'deploying';
      deployment.progress = 20;
      await this.deploymentRepository.save(deployment);

      const containerName = `${application.slug}-${deployedColor}`;
      const hostPort = application.port || await this.getAvailablePort();

      // Stop existing container if running
      try {
        await execAsync(`docker stop ${containerName} 2>/dev/null || true`);
        await execAsync(`docker rm ${containerName} 2>/dev/null || true`);
        addLog(`Stopped existing container: ${containerName}`);
      } catch (error) {
        // Container might not exist, that's fine
      }

      // Build and start new container
      const artifactDir = path.dirname(build.artifactPath);
      const dockerfile = this.generateDockerfile(application);
      
      await fs.writeFile(
        path.join(artifactDir, 'Dockerfile.deploy'),
        dockerfile,
      );

      addLog('Building Docker image...');
      const imageName = `saas-gestor/${application.slug}:${deploymentId}`;
      
      await execAsync(
        `cd ${artifactDir} && docker build -f Dockerfile.deploy -t ${imageName} .`,
      );
      addLog(`Docker image built: ${imageName}`);

      // Parse environment variables
      const envVars = application.envVars ? JSON.parse(application.envVars) : {};
      const envString = Object.entries(envVars)
        .map(([key, value]) => `-e ${key}="${value}"`)
        .join(' ');

      // Run container
      addLog(`Starting container on port ${hostPort}...`);
      const { stdout: containerId } = await execAsync(
        `docker run -d --name ${containerName} \
          -p ${hostPort}:3000 \
          ${envString} \
          --restart unless-stopped \
          ${imageName}`,
      );

      deployment.containerId = containerId.trim();
      deployment.containerPort = hostPort;
      addLog(`Container started: ${deployment.containerId.substring(0, 12)}`);

      // Stage: Health check
      deployment.stage = 'health_check';
      deployment.progress = 60;
      await this.deploymentRepository.save(deployment);

      addLog('Waiting for health check...');
      await this.sleep(5000); // Give container time to start

      const healthCheckUrl = application.healthCheckUrl || `http://localhost:${hostPort}/health`;
      const isHealthy = await this.performHealthCheck(healthCheckUrl);

      if (!isHealthy) {
        throw new Error(`Health check failed: ${healthCheckUrl}`);
      }
      addLog('Health check passed!');

      // Stage: Completing
      deployment.stage = 'completing';
      deployment.progress = 80;
      await this.deploymentRepository.save(deployment);

      // Update nginx configuration for blue-green switch
      await this.updateNginxConfig(application.slug, hostPort, deployedColor);
      addLog('Nginx configuration updated');

      // Reload nginx
      await execAsync('docker exec nginx nginx -s reload || nginx -s reload');
      addLog('Nginx reloaded');

      // Update application record
      const app = await this.applicationRepository.findOne({
        where: { id: applicationId },
      });
      if (app) {
        app.status = ApplicationStatus.ACTIVE;
        app.currentVersion = build.commitHash?.substring(0, 7) || deploymentId.toString();
        app.currentBuildId = job.data.buildId;
        app.lastDeployedAt = new Date();
        app.port = hostPort;
        await this.applicationRepository.save(app);
      }

      // Mark deployment as successful
      deployment.status = DeploymentStatus.SUCCESS;
      deployment.progress = 100;
      deployment.completedAt = new Date();
      deployment.duration = Math.round(
        (deployment.completedAt.getTime() - deployment.startedAt.getTime()) / 1000,
      );
      deployment.logs = logs.join('\n');
      await this.deploymentRepository.save(deployment);

      addLog('Deployment completed successfully!');

      // Track successful deployment
      await this.analyticsService.trackMetric(
        tenantId,
        MetricType.DEPLOYMENT_COMPLETED,
        { deploymentId, duration: deployment.duration },
        applicationId,
      );

      // Cleanup old containers (keep last 2)
      await this.cleanupOldContainers(application.slug, deployedColor);

    } catch (error) {
      this.logger.error(`Deployment ${deploymentId} failed:`, error);

      addLog(`ERROR: ${error.message}`);

      deployment.status = DeploymentStatus.FAILED;
      deployment.errorMessage = error.message;
      deployment.completedAt = new Date();
      deployment.logs = logs.join('\n');

      if (deployment.startedAt) {
        deployment.duration = Math.round(
          (deployment.completedAt.getTime() - deployment.startedAt.getTime()) / 1000,
        );
      }

      await this.deploymentRepository.save(deployment);

      // Track failed deployment
      await this.analyticsService.trackMetric(
        tenantId,
        MetricType.DEPLOYMENT_FAILED,
        { deploymentId, duration: deployment.duration, errorMessage: error.message },
        applicationId,
      );

      // Update application status
      const app = await this.applicationRepository.findOne({
        where: { id: applicationId },
      });
      if (app) {
        app.status = ApplicationStatus.ERROR;
        await this.applicationRepository.save(app);
      }

      // Auto-rollback if enabled
      if (job.data.autoRollback && deployment.previousDeploymentId) {
        addLog('Initiating automatic rollback...');
        // This would trigger a rollback job
      }

      throw error;
    }
  }

  @Process('rollback')
  async handleRollback(job: Job<any>): Promise<void> {
    const { deploymentId, applicationId, previousDeploymentId, tenantId } = job.data;

    this.logger.log(`Rolling back deployment ${deploymentId} to ${previousDeploymentId}`);

    // Track rollback
    await this.analyticsService.trackMetric(
      tenantId,
      MetricType.DEPLOYMENT_ROLLED_BACK,
      { deploymentId, previousDeploymentId },
      applicationId,
    );

    // TODO: Implement rollback logic
    // This would restore the previous container and nginx config
  }

  @OnQueueActive()
  onActive(job: Job): void {
    this.logger.log(`Processing job ${job.id} of type ${job.name}`);
  }

  @OnQueueCompleted()
  onCompleted(job: Job): void {
    this.logger.log(`Job ${job.id} completed`);
  }

  @OnQueueFailed()
  onFailed(job: Job, error: Error): void {
    this.logger.error(`Job ${job.id} failed:`, error);
  }

  private async getNextColor(applicationId: number): Promise<string> {
    // Alternate between blue and green
    const lastDeployment = await this.deploymentRepository.findOne({
      where: { applicationId, status: DeploymentStatus.SUCCESS },
      order: { createdAt: 'DESC' },
    });

    if (!lastDeployment || !lastDeployment.deployedColor) {
      return 'blue';
    }

    return lastDeployment.deployedColor === 'blue' ? 'green' : 'blue';
  }

  private async getAvailablePort(): Promise<number> {
    // Simple port allocation - in production, use a proper port manager
    // Ports 10000-20000 reserved for apps
    const minPort = 10000;
    const maxPort = 20000;
    
    // Get used ports
    const apps = await this.applicationRepository.find({
      where: { port: { not: null } },
      select: ['port'],
    });
    
    const usedPorts = new Set(apps.map(a => a.port));
    
    for (let port = minPort; port <= maxPort; port++) {
      if (!usedPorts.has(port)) {
        return port;
      }
    }
    
    throw new Error('No available ports');
  }

  private generateDockerfile(application: any): string {
    const baseImage = application.dockerImage || 'node:18-alpine';
    
    return `FROM ${baseImage}

WORKDIR /app

COPY . .

${application.startCommand ? `CMD ${application.startCommand}` : 'CMD ["npm", "start"]'}

EXPOSE 3000
`;
  }

  private async performHealthCheck(url: string): Promise<boolean> {
    const maxRetries = 30;
    const retryDelay = 2000;

    for (let i = 0; i < maxRetries; i++) {
      try {
        const { stdout } = await execAsync(`curl -sf ${url} || echo "FAIL"`);
        if (stdout !== 'FAIL') {
          return true;
        }
      } catch {
        // Ignore errors, keep retrying
      }
      await this.sleep(retryDelay);
    }

    return false;
  }

  private async updateNginxConfig(slug: string, port: number, color: string): Promise<void> {
    const nginxConfig = `
# Auto-generated config for ${slug}
upstream ${slug}_backend {
    server localhost:${port};
}

server {
    listen 80;
    server_name ${slug}.local;
    
    location / {
        proxy_pass http://${slug}_backend;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
`;

    const configPath = `/etc/nginx/conf.d/${slug}.conf`;
    await fs.writeFile(configPath, nginxConfig);
  }

  private async cleanupOldContainers(slug: string, currentColor: string): Promise<void> {
    try {
      const otherColor = currentColor === 'blue' ? 'green' : 'blue';
      const oldContainer = `${slug}-${otherColor}`;
      
      // Stop and remove old container
      await execAsync(`docker stop ${oldContainer} 2>/dev/null || true`);
      await execAsync(`docker rm ${oldContainer} 2>/dev/null || true`);
      
      this.logger.log(`Cleaned up old container: ${oldContainer}`);
    } catch (error) {
      this.logger.warn(`Failed to cleanup old container: ${error.message}`);
    }
  }

  private sleep(ms: number): Promise<void> {
    return new Promise(resolve => setTimeout(resolve, ms));
  }
}
