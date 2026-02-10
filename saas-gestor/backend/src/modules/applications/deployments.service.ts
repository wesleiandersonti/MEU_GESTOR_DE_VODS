import { Injectable, NotFoundException, Inject } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { InjectQueue } from '@nestjs/bullmq';
import { Queue } from 'bullmq';
import { Repository } from 'typeorm';
import { CACHE_MANAGER } from '@nestjs/cache-manager';
import { Cache } from 'cache-manager';
import { Deployment, DeploymentStatus, DeploymentType } from './entities/deployment.entity';
import { Application, ApplicationStatus } from './entities/application.entity';
import { Build } from './entities/build.entity';
import { ApplicationsService } from './applications.service';
import { BuildsService } from './builds.service';

export interface CreateDeploymentDto {
  buildId: number;
  deploymentType?: DeploymentType;
  autoRollback?: boolean;
}

@Injectable()
export class DeploymentsService {
  constructor(
    @InjectRepository(Deployment)
    private deploymentRepository: Repository<Deployment>,
    @InjectRepository(Application)
    private applicationRepository: Repository<Application>,
    @InjectRepository(Build)
    private buildRepository: Repository<Build>,
    @InjectQueue('deployments') private deploymentsQueue: Queue,
    @Inject(CACHE_MANAGER) private cacheManager: Cache,
    private applicationsService: ApplicationsService,
    private buildsService: BuildsService,
  ) {}

  /**
   * Get all deployments for an application
   */
  async findAll(applicationId: number, tenantId: number): Promise<Deployment[]> {
    await this.applicationsService.findOne(applicationId, tenantId);

    return this.deploymentRepository.find({
      where: { applicationId },
      order: { createdAt: 'DESC' },
      take: 50,
      relations: ['build'],
    });
  }

  /**
   * Get deployment by ID
   */
  async findOne(id: number, applicationId: number, tenantId: number): Promise<Deployment> {
    await this.applicationsService.findOne(applicationId, tenantId);

    const deployment = await this.deploymentRepository.findOne({
      where: { id, applicationId },
      relations: ['build'],
    });

    if (!deployment) {
      throw new NotFoundException('Deployment not found');
    }

    return deployment;
  }

  /**
   * Create and queue a new deployment
   */
  async create(
    applicationId: number,
    createDto: CreateDeploymentDto,
    tenantId: number,
    userId: number,
  ): Promise<Deployment> {
    // Verify application and build
    const application = await this.applicationsService.findOne(applicationId, tenantId);
    const build = await this.buildsService.findOne(createDto.buildId, applicationId, tenantId);

    if (build.status !== 'success') {
      throw new Error('Cannot deploy a build that did not complete successfully');
    }

    // Get next deployment number
    const lastDeployment = await this.deploymentRepository.findOne({
      where: { applicationId },
      order: { deploymentNumber: 'DESC' },
    });
    const deploymentNumber = (lastDeployment?.deploymentNumber || 0) + 1;

    // Determine deployment type
    const deploymentType = createDto.deploymentType || DeploymentType.BLUE_GREEN;

    // Get previous successful deployment for rollback capability
    const previousDeployment = await this.deploymentRepository.findOne({
      where: { 
        applicationId, 
        status: DeploymentStatus.SUCCESS 
      },
      order: { createdAt: 'DESC' },
    });

    // Create deployment record
    const deployment = this.deploymentRepository.create({
      applicationId,
      buildId: createDto.buildId,
      deploymentNumber,
      status: DeploymentStatus.QUEUED,
      deploymentType,
      previousDeploymentId: previousDeployment?.id?.toString(),
      triggeredById: userId,
      triggerType: 'manual',
    });

    const saved = await this.deploymentRepository.save(deployment);

    // Update application status
    application.status = ApplicationStatus.DEPLOYING;
    await this.applicationRepository.save(application);

    // Add to queue
    await this.deploymentsQueue.add('deploy', {
      deploymentId: saved.id,
      applicationId,
      tenantId,
      buildId: createDto.buildId,
      deploymentType,
      autoRollback: createDto.autoRollback ?? true,
      application: {
        name: application.name,
        slug: application.slug,
        domain: application.domain,
        port: application.port,
        dockerImage: application.dockerImage,
        envVars: application.envVars,
        startCommand: application.startCommand,
        healthCheckUrl: application.healthCheckUrl,
      },
      build: {
        artifactPath: build.artifactPath,
        commitHash: build.commitHash,
      },
    }, {
      attempts: 1, // Don't retry deployments automatically
    });

    // Invalidate cache
    await this.cacheManager.del(`apps:tenant:${tenantId}`);
    await this.cacheManager.del(`app:${applicationId}`);

    return saved;
  }

  /**
   * Get deployment logs
   */
  async getLogs(id: number, applicationId: number, tenantId: number): Promise<string> {
    const deployment = await this.findOne(id, applicationId, tenantId);
    return deployment.logs || 'No logs available yet...';
  }

  /**
   * Cancel a pending deployment
   */
  async cancel(id: number, applicationId: number, tenantId: number): Promise<Deployment> {
    const deployment = await this.findOne(id, applicationId, tenantId);

    if (deployment.status !== DeploymentStatus.QUEUED && deployment.status !== DeploymentStatus.PENDING) {
      throw new Error('Cannot cancel deployment that is not queued or pending');
    }

    // Remove from queue if queued
    if (deployment.status === DeploymentStatus.QUEUED) {
      const jobs = await this.deploymentsQueue.getJobs(['waiting', 'delayed']);
      const job = jobs.find(j => j.data.deploymentId === id);
      if (job) {
        await job.remove();
      }
    }

    deployment.status = DeploymentStatus.FAILED;
    deployment.errorMessage = 'Cancelled by user';
    deployment.completedAt = new Date();
    
    const saved = await this.deploymentRepository.save(deployment);

    // Update application status
    const application = await this.applicationsService.findOne(applicationId, tenantId);
    application.status = ApplicationStatus.ACTIVE;
    await this.applicationRepository.save(application);

    return saved;
  }

  /**
   * Rollback to previous deployment
   */
  async rollback(id: number, applicationId: number, tenantId: number, userId: number): Promise<Deployment> {
    const deployment = await this.findOne(id, applicationId, tenantId);

    if (!deployment.previousDeploymentId) {
      throw new Error('No previous deployment available for rollback');
    }

    if (deployment.status !== DeploymentStatus.SUCCESS && deployment.status !== DeploymentStatus.FAILED) {
      throw new Error('Can only rollback successful or failed deployments');
    }

    // Update deployment status
    deployment.status = DeploymentStatus.ROLLING_BACK;
    deployment.rolledBackAt = new Date();
    await this.deploymentRepository.save(deployment);

    // Queue rollback job
    await this.deploymentsQueue.add('rollback', {
      deploymentId: deployment.id,
      applicationId,
      tenantId,
      previousDeploymentId: deployment.previousDeploymentId,
      triggeredById: userId,
    });

    return deployment;
  }

  /**
   * Get current deployment (latest successful)
   */
  async getCurrent(applicationId: number, tenantId: number): Promise<Deployment | null> {
    await this.applicationsService.findOne(applicationId, tenantId);

    return this.deploymentRepository.findOne({
      where: { 
        applicationId, 
        status: DeploymentStatus.SUCCESS 
      },
      order: { createdAt: 'DESC' },
      relations: ['build'],
    });
  }

  /**
   * Get deployment statistics
   */
  async getStats(applicationId: number, tenantId: number): Promise<any> {
    await this.applicationsService.findOne(applicationId, tenantId);

    const [
      totalDeployments,
      successfulDeployments,
      failedDeployments,
      avgDuration,
    ] = await Promise.all([
      this.deploymentRepository.count({ where: { applicationId } }),
      this.deploymentRepository.count({ 
        where: { applicationId, status: DeploymentStatus.SUCCESS } 
      }),
      this.deploymentRepository.count({ 
        where: { applicationId, status: DeploymentStatus.FAILED } 
      }),
      this.deploymentRepository
        .createQueryBuilder('d')
        .select('AVG(d.duration)', 'avg')
        .where('d.applicationId = :applicationId', { applicationId })
        .andWhere('d.duration IS NOT NULL')
        .getRawOne(),
    ]);

    return {
      total: totalDeployments,
      successful: successfulDeployments,
      failed: failedDeployments,
      successRate: totalDeployments > 0 
        ? Math.round((successfulDeployments / totalDeployments) * 100) 
        : 0,
      averageDuration: Math.round(avgDuration?.avg || 0),
    };
  }

  /**
   * Retry a failed deployment
   */
  async retry(id: number, applicationId: number, tenantId: number, userId: number): Promise<Deployment> {
    const deployment = await this.findOne(id, applicationId, tenantId);

    if (deployment.status !== DeploymentStatus.FAILED) {
      throw new Error('Can only retry failed deployments');
    }

    // Create new deployment with same build
    return this.create(
      applicationId,
      {
        buildId: deployment.buildId,
        deploymentType: deployment.deploymentType,
      },
      tenantId,
      userId,
    );
  }
}
