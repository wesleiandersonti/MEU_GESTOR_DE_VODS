import { Injectable, NotFoundException, Inject } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { InjectQueue } from '@nestjs/bullmq';
import { Queue } from 'bullmq';
import { Repository } from 'typeorm';
import { CACHE_MANAGER } from '@nestjs/cache-manager';
import { Cache } from 'cache-manager';
import { Build, BuildStatus } from './entities/build.entity';
import { Application } from './entities/application.entity';
import { ApplicationsService } from './applications.service';

export interface CreateBuildDto {
  commitHash?: string;
  commitMessage?: string;
  branch?: string;
}

@Injectable()
export class BuildsService {
  constructor(
    @InjectRepository(Build)
    private buildRepository: Repository<Build>,
    @InjectRepository(Application)
    private applicationRepository: Repository<Application>,
    @InjectQueue('builds') private buildsQueue: Queue,
    @Inject(CACHE_MANAGER) private cacheManager: Cache,
    private applicationsService: ApplicationsService,
  ) {}

  /**
   * Get all builds for an application
   */
  async findAll(applicationId: number, tenantId: number): Promise<Build[]> {
    // Verify application belongs to tenant
    await this.applicationsService.findOne(applicationId, tenantId);

    return this.buildRepository.find({
      where: { applicationId },
      order: { createdAt: 'DESC' },
      take: 50,
    });
  }

  /**
   * Get build by ID
   */
  async findOne(id: number, applicationId: number, tenantId: number): Promise<Build> {
    // Verify application belongs to tenant
    await this.applicationsService.findOne(applicationId, tenantId);

    const build = await this.buildRepository.findOne({
      where: { id, applicationId },
    });

    if (!build) {
      throw new NotFoundException('Build not found');
    }

    return build;
  }

  /**
   * Create and queue a new build
   */
  async create(
    applicationId: number,
    createDto: CreateBuildDto,
    tenantId: number,
    userId: number,
  ): Promise<Build> {
    // Verify application
    const application = await this.applicationsService.findOne(applicationId, tenantId);

    // Get next build number
    const lastBuild = await this.buildRepository.findOne({
      where: { applicationId },
      order: { buildNumber: 'DESC' },
    });
    const buildNumber = (lastBuild?.buildNumber || 0) + 1;

    // Create build record
    const build = this.buildRepository.create({
      applicationId,
      buildNumber,
      commitHash: createDto.commitHash,
      commitMessage: createDto.commitMessage,
      commitAuthor: 'Manual Trigger', // TODO: Get from git
      status: BuildStatus.QUEUED,
      triggeredById: userId,
      triggerType: 'manual',
    });

    const saved = await this.buildRepository.save(build);

    // Update application status
    application.status = ApplicationStatus.BUILDING;
    application.lastBuildAt = new Date();
    await this.applicationRepository.save(application);

    // Add to queue
    await this.buildsQueue.add('build', {
      buildId: saved.id,
      applicationId,
      tenantId,
      repositoryUrl: application.repositoryUrl,
      repositoryBranch: createDto.branch || application.repositoryBranch,
      buildCommand: application.buildCommand,
    }, {
      attempts: 3,
      backoff: {
        type: 'exponential',
        delay: 5000,
      },
    });

    // Invalidate cache
    await this.cacheManager.del(`apps:tenant:${tenantId}`);
    await this.cacheManager.del(`app:${applicationId}`);

    return saved;
  }

  /**
   * Get build logs (with streaming support)
   */
  async getLogs(id: number, applicationId: number, tenantId: number): Promise<string> {
    const build = await this.findOne(id, applicationId, tenantId);
    return build.logs || 'No logs available yet...';
  }

  /**
   * Cancel a running build
   */
  async cancel(id: number, applicationId: number, tenantId: number): Promise<Build> {
    const build = await this.findOne(id, applicationId, tenantId);

    if (build.status !== BuildStatus.RUNNING && build.status !== BuildStatus.QUEUED) {
      throw new Error('Cannot cancel build that is not running or queued');
    }

    // Remove from queue if queued
    if (build.status === BuildStatus.QUEUED) {
      // Find and remove job from queue
      const jobs = await this.buildsQueue.getJobs(['waiting', 'delayed']);
      const job = jobs.find(j => j.data.buildId === id);
      if (job) {
        await job.remove();
      }
    }

    build.status = BuildStatus.CANCELLED;
    build.completedAt = new Date();
    
    const saved = await this.buildRepository.save(build);

    // Update application status
    const application = await this.applicationsService.findOne(applicationId, tenantId);
    application.status = ApplicationStatus.ACTIVE;
    await this.applicationRepository.save(application);

    return saved;
  }

  /**
   * Get queue status
   */
  async getQueueStatus(): Promise<any> {
    const [waiting, active, completed, failed] = await Promise.all([
      this.buildsQueue.getWaitingCount(),
      this.buildsQueue.getActiveCount(),
      this.buildsQueue.getCompletedCount(),
      this.buildsQueue.getFailedCount(),
    ]);

    return {
      waiting,
      active,
      completed,
      failed,
    };
  }

  /**
   * Retry a failed build
   */
  async retry(id: number, applicationId: number, tenantId: number, userId: number): Promise<Build> {
    const build = await this.findOne(id, applicationId, tenantId);

    if (build.status !== BuildStatus.FAILED && build.status !== BuildStatus.CANCELLED) {
      throw new Error('Can only retry failed or cancelled builds');
    }

    // Create new build with same params
    return this.create(
      applicationId,
      {
        commitHash: build.commitHash,
        commitMessage: build.commitMessage,
      },
      tenantId,
      userId,
    );
  }
}
