import { Processor, WorkerHost, OnWorkerEvent } from '@nestjs/bullmq';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { Job } from 'bullmq';
import { Logger } from '@nestjs/common';
import * as simpleGit from 'simple-git';
import * as fs from 'fs-extra';
import * as path from 'path';
import { exec } from 'child_process';
import { promisify } from 'util';
import { Build, BuildStatus } from '../entities/build.entity';
import { Application, ApplicationStatus } from '../entities/application.entity';
import { AnalyticsService } from '../../analytics/analytics.service';
import { MetricType } from '../../analytics/entities/analytics-metric.entity';

const execAsync = promisify(exec);

interface BuildJobData {
  buildId: number;
  applicationId: number;
  tenantId: number;
  repositoryUrl: string;
  repositoryBranch: string;
  buildCommand: string;
}

@Processor('builds', {
  concurrency: 2, // Run 2 builds simultaneously
})
export class BuildProcessor extends WorkerHost {
  private readonly logger = new Logger(BuildProcessor.name);
  private readonly buildsDir = '/tmp/builds';

  constructor(
    @InjectRepository(Build)
    private buildRepository: Repository<Build>,
    @InjectRepository(Application)
    private applicationRepository: Repository<Application>,
    private analyticsService: AnalyticsService,
  ) {
    super();
    // Ensure builds directory exists
    fs.ensureDirSync(this.buildsDir);
  }

  async process(job: Job<BuildJobData>): Promise<void> {
    const { buildId, applicationId, tenantId, repositoryUrl, repositoryBranch, buildCommand } = job.data;
    
    this.logger.log(`🚀 Starting build ${buildId} for application ${applicationId}`);
    
    const buildDir = path.join(this.buildsDir, `build-${buildId}`);
    const logs: string[] = [];
    const startTime = Date.now();

    const addLog = (message: string) => {
      const timestamp = new Date().toISOString();
      const logLine = `[${timestamp}] ${message}`;
      logs.push(logLine);
      this.logger.log(logLine);
    };

    try {
      // Track build start
      await this.analyticsService.trackMetric(
        tenantId,
        MetricType.BUILD_STARTED,
        { buildId },
        applicationId,
      );

      // Update build status to running
      await this.updateBuildStatus(buildId, BuildStatus.RUNNING, 'initializing', 5);
      addLog('Build initialized');

      // Clone repository
      await this.updateBuildStatus(buildId, BuildStatus.RUNNING, 'cloning', 10);
      addLog(`Cloning repository: ${repositoryUrl}`);
      addLog(`Branch: ${repositoryBranch}`);
      
      if (!repositoryUrl) {
        throw new Error('No repository URL configured');
      }

      await simpleGit().clone(repositoryUrl, buildDir, ['--branch', repositoryBranch, '--single-branch', '--depth', '1']);
      addLog('Repository cloned successfully');

      // Install dependencies
      await this.updateBuildStatus(buildId, BuildStatus.RUNNING, 'installing', 30);
      addLog('Installing dependencies...');
      
      try {
        const { stdout: installOutput } = await execAsync('npm install', {
          cwd: buildDir,
          timeout: 300000, // 5 minutes
        });
        addLog(installOutput);
      } catch (error) {
        addLog(`Warning during install: ${error.message}`);
        // Continue even if warnings
      }

      // Run build command
      await this.updateBuildStatus(buildId, BuildStatus.RUNNING, 'building', 60);
      if (buildCommand) {
        addLog(`Running build command: ${buildCommand}`);
        const { stdout: buildOutput, stderr: buildError } = await execAsync(buildCommand, {
          cwd: buildDir,
          timeout: 600000, // 10 minutes
        });
        addLog(buildOutput);
        if (buildError) {
          addLog(`Build warnings: ${buildError}`);
        }
      } else {
        addLog('No build command specified, skipping...');
      }

      // Create artifact (simple tar.gz for now)
      await this.updateBuildStatus(buildId, BuildStatus.RUNNING, 'packaging', 90);
      addLog('Creating artifact...');
      
      const artifactName = `build-${buildId}.tar.gz`;
      const artifactPath = path.join(this.buildsDir, artifactName);
      await execAsync(`tar -czf ${artifactPath} -C ${buildDir} .`);
      addLog(`Artifact created: ${artifactPath}`);

      // Build completed successfully
      const duration = Math.round((Date.now() - startTime) / 1000);
      await this.completeBuild(buildId, BuildStatus.SUCCESS, duration, logs.join('\n'), artifactPath);
      addLog(`✅ Build completed successfully in ${duration}s`);

      // Track successful build
      await this.analyticsService.trackMetric(
        tenantId,
        MetricType.BUILD_COMPLETED,
        { buildId, duration },
        applicationId,
      );

      // Update application status
      await this.updateApplicationStatus(applicationId, ApplicationStatus.ACTIVE, buildId);

    } catch (error) {
      const duration = Math.round((Date.now() - startTime) / 1000);
      addLog(`❌ Build failed: ${error.message}`);
      addLog(error.stack);

      await this.completeBuild(buildId, BuildStatus.FAILED, duration, logs.join('\n'), null, error.message);

      // Track failed build
      await this.analyticsService.trackMetric(
        tenantId,
        MetricType.BUILD_FAILED,
        { buildId, duration, errorMessage: error.message },
        applicationId,
      );

      // Update application status
      await this.updateApplicationStatus(applicationId, ApplicationStatus.ERROR);

      throw error; // Re-throw to mark job as failed
    } finally {
      // Cleanup build directory
      try {
        await fs.remove(buildDir);
        addLog('Cleanup completed');
      } catch (cleanupError) {
        this.logger.error('Failed to cleanup build directory', cleanupError);
      }
    }
  }

  private async updateBuildStatus(
    buildId: number,
    status: BuildStatus,
    stage: string,
    progress: number,
  ): Promise<void> {
    await this.buildRepository.update(buildId, {
      status,
      stage,
      progress,
      startedAt: status === BuildStatus.RUNNING ? new Date() : undefined,
    });
  }

  private async completeBuild(
    buildId: number,
    status: BuildStatus,
    duration: number,
    logs: string,
    artifactPath: string | null,
    errorMessage?: string,
  ): Promise<void> {
    await this.buildRepository.update(buildId, {
      status,
      stage: status === BuildStatus.SUCCESS ? 'completed' : 'failed',
      progress: status === BuildStatus.SUCCESS ? 100 : 0,
      duration,
      logs,
      artifactPath,
      errorMessage,
      completedAt: new Date(),
    });
  }

  private async updateApplicationStatus(
    applicationId: number,
    status: ApplicationStatus,
    currentBuildId?: number,
  ): Promise<void> {
    const updateData: any = { status };
    
    if (status === ApplicationStatus.ACTIVE && currentBuildId) {
      updateData.currentBuildId = currentBuildId;
      updateData.currentVersion = `build-${currentBuildId}`;
    }
    
    await this.applicationRepository.update(applicationId, updateData);
  }

  @OnWorkerEvent('completed')
  onCompleted(job: Job) {
    this.logger.log(`✅ Job ${job.id} completed successfully`);
  }

  @OnWorkerEvent('failed')
  onFailed(job: Job, error: Error) {
    this.logger.error(`❌ Job ${job.id} failed: ${error.message}`);
  }
}
