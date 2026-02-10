import { Injectable, NotFoundException, ForbiddenException, Inject } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository, FindManyOptions } from 'typeorm';
import { CACHE_MANAGER } from '@nestjs/cache-manager';
import { Cache } from 'cache-manager';
import { Application, ApplicationStatus, ApplicationType, ApplicationEnvironment } from './entities/application.entity';
import { User } from '../users/entities/user.entity';
import { UserRole } from '../users/entities/user.entity';

export interface CreateApplicationDto {
  name: string;
  slug: string;
  description?: string;
  type: ApplicationType;
  environment?: ApplicationEnvironment;
  repositoryUrl?: string;
  repositoryBranch?: string;
  buildCommand?: string;
  startCommand?: string;
  envVars?: Record<string, string>;
}

export interface UpdateApplicationDto extends Partial<CreateApplicationDto> {
  status?: ApplicationStatus;
}

@Injectable()
export class ApplicationsService {
  constructor(
    @InjectRepository(Application)
    private applicationRepository: Repository<Application>,
    @Inject(CACHE_MANAGER) private cacheManager: Cache,
  ) {}

  /**
   * Get all applications for a tenant with caching
   */
  async findAll(tenantId: number, options?: FindManyOptions<Application>): Promise<Application[]> {
    const cacheKey = `apps:tenant:${tenantId}`;
    
    // Try cache
    const cached = await this.cacheManager.get<Application[]>(cacheKey);
    if (cached) {
      console.log('✅ Applications loaded from cache');
      return cached;
    }

    // Fetch from database
    const applications = await this.applicationRepository.find({
      where: { tenantId },
      order: { createdAt: 'DESC' },
      ...options,
    });

    // Cache for 5 minutes
    await this.cacheManager.set(cacheKey, applications, 300);
    
    return applications;
  }

  /**
   * Get single application with caching
   */
  async findOne(id: number, tenantId: number): Promise<Application> {
    const cacheKey = `app:${id}`;
    
    // Try cache
    const cached = await this.cacheManager.get<Application>(cacheKey);
    if (cached && cached.tenantId === tenantId) {
      return cached;
    }

    const application = await this.applicationRepository.findOne({
      where: { id, tenantId },
      relations: ['builds'],
    });

    if (!application) {
      throw new NotFoundException('Application not found');
    }

    // Cache for 10 minutes
    await this.cacheManager.set(cacheKey, application, 600);
    
    return application;
  }

  /**
   * Create new application
   */
  async create(createDto: CreateApplicationDto, tenantId: number, userId: number): Promise<Application> {
    // Check if slug already exists
    const existing = await this.applicationRepository.findOne({
      where: { slug: createDto.slug, tenantId },
    });

    if (existing) {
      throw new ForbiddenException('Application slug already exists');
    }

    const application = this.applicationRepository.create({
      ...createDto,
      tenantId,
      createdById: userId,
      envVars: createDto.envVars ? JSON.stringify(createDto.envVars) : null,
    });

    const saved = await this.applicationRepository.save(application);
    
    // Invalidate cache
    await this.cacheManager.del(`apps:tenant:${tenantId}`);
    
    return saved;
  }

  /**
   * Update application
   */
  async update(id: number, updateDto: UpdateApplicationDto, tenantId: number, user: User): Promise<Application> {
    // Check permissions
    if (user.role !== UserRole.ADMIN && user.role !== UserRole.DEVOPS) {
      throw new ForbiddenException('Insufficient permissions');
    }

    const application = await this.findOne(id, tenantId);
    
    // Update fields
    if (updateDto.name) application.name = updateDto.name;
    if (updateDto.description !== undefined) application.description = updateDto.description;
    if (updateDto.repositoryUrl !== undefined) application.repositoryUrl = updateDto.repositoryUrl;
    if (updateDto.repositoryBranch) application.repositoryBranch = updateDto.repositoryBranch;
    if (updateDto.buildCommand !== undefined) application.buildCommand = updateDto.buildCommand;
    if (updateDto.startCommand !== undefined) application.startCommand = updateDto.startCommand;
    if (updateDto.envVars) application.envVars = JSON.stringify(updateDto.envVars);
    if (updateDto.status) application.status = updateDto.status;

    const saved = await this.applicationRepository.save(application);
    
    // Invalidate caches
    await this.cacheManager.del(`app:${id}`);
    await this.cacheManager.del(`apps:tenant:${tenantId}`);
    
    return saved;
  }

  /**
   * Delete application
   */
  async remove(id: number, tenantId: number, user: User): Promise<void> {
    if (user.role !== UserRole.ADMIN) {
      throw new ForbiddenException('Only admins can delete applications');
    }

    const application = await this.findOne(id, tenantId);
    await this.applicationRepository.remove(application);
    
    // Invalidate caches
    await this.cacheManager.del(`app:${id}`);
    await this.cacheManager.del(`apps:tenant:${tenantId}`);
  }

  /**
   * Update application status
   */
  async updateStatus(id: number, status: ApplicationStatus, tenantId: number): Promise<Application> {
    const application = await this.findOne(id, tenantId);
    application.status = status;
    
    const saved = await this.applicationRepository.save(application);
    
    // Invalidate cache
    await this.cacheManager.del(`app:${id}`);
    await this.cacheManager.del(`apps:tenant:${tenantId}`);
    
    return saved;
  }

  /**
   * Get application statistics for dashboard
   */
  async getStats(tenantId: number): Promise<any> {
    const cacheKey = `apps:stats:${tenantId}`;
    
    const cached = await this.cacheManager.get(cacheKey);
    if (cached) return cached;

    const stats = await this.applicationRepository
      .createQueryBuilder('app')
      .select('app.status', 'status')
      .addSelect('COUNT(*)', 'count')
      .where('app.tenantId = :tenantId', { tenantId })
      .groupBy('app.status')
      .getRawMany();

    const result = {
      total: stats.reduce((sum, s) => sum + parseInt(s.count), 0),
      byStatus: stats,
    };

    // Cache for 2 minutes
    await this.cacheManager.set(cacheKey, result, 120);
    
    return result;
  }
}
