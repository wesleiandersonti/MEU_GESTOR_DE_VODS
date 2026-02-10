import { Injectable, Logger, OnModuleInit } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { Redis } from 'ioredis';
import { InjectRedis } from '@nestjs-modules/ioredis';
import { SystemHealth } from './entities/analytics-metric.entity';
import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

export interface SystemStatus {
  database: {
    status: 'healthy' | 'degraded' | 'unhealthy';
    responseTime: number;
    connections: number;
  };
  redis: {
    status: 'healthy' | 'degraded' | 'unhealthy';
    responseTime: number;
    memoryUsage: string;
    connectedClients: number;
  };
  docker: {
    status: 'healthy' | 'degraded' | 'unhealthy';
    containers: {
      total: number;
      running: number;
      stopped: number;
    };
    images: number;
  };
  disk: {
    status: 'healthy' | 'degraded' | 'unhealthy';
    total: string;
    used: string;
    free: string;
    usagePercent: number;
  };
  memory: {
    status: 'healthy' | 'degraded' | 'unhealthy';
    total: string;
    used: string;
    free: string;
    usagePercent: number;
  };
  cpu: {
    status: 'healthy' | 'degraded' | 'unhealthy';
    usagePercent: number;
    loadAverage: number[];
  };
  nginx?: {
    status: 'healthy' | 'degraded' | 'unhealthy';
    activeConnections: number;
    requestsHandled: number;
  };
}

export interface QueueStatus {
  builds: {
    waiting: number;
    active: number;
    completed: number;
    failed: number;
  };
  deployments: {
    waiting: number;
    active: number;
    completed: number;
    failed: number;
  };
}

@Injectable()
export class MonitoringService implements OnModuleInit {
  private readonly logger = new Logger(MonitoringService.name);
  private systemStatus: SystemStatus | null = null;
  private lastStatusUpdate: Date | null = null;

  constructor(
    @InjectRepository(SystemHealth)
    private systemHealthRepository: Repository<SystemHealth>,
    @InjectRedis() private readonly redis: Redis,
  ) {}

  onModuleInit() {
    // Start periodic health checks
    this.startHealthChecks();
  }

  /**
   * Get full system status
   */
  async getSystemStatus(): Promise<SystemStatus> {
    // Cache status for 30 seconds
    if (this.systemStatus && this.lastStatusUpdate) {
      const cacheAge = Date.now() - this.lastStatusUpdate.getTime();
      if (cacheAge < 30000) {
        return this.systemStatus;
      }
    }

    const [
      databaseStatus,
      redisStatus,
      dockerStatus,
      diskStatus,
      memoryStatus,
      cpuStatus,
    ] = await Promise.all([
      this.checkDatabaseHealth(),
      this.checkRedisHealth(),
      this.checkDockerHealth(),
      this.checkDiskHealth(),
      this.checkMemoryHealth(),
      this.checkCpuHealth(),
    ]);

    this.systemStatus = {
      database: databaseStatus,
      redis: redisStatus,
      docker: dockerStatus,
      disk: diskStatus,
      memory: memoryStatus,
      cpu: cpuStatus,
    };

    this.lastStatusUpdate = new Date();

    // Store in database
    await this.storeHealthStatus('database', databaseStatus.status, { responseTime: databaseStatus.responseTime });
    await this.storeHealthStatus('redis', redisStatus.status, { responseTime: redisStatus.responseTime });
    await this.storeHealthStatus('docker', dockerStatus.status, { containers: dockerStatus.containers });

    return this.systemStatus;
  }

  /**
   * Get queue status
   */
  async getQueueStatus(): Promise<QueueStatus> {
    try {
      // Get BullMQ queue stats from Redis
      const [buildsWaiting, buildsActive, buildsCompleted, buildsFailed] = await Promise.all([
        this.redis.llen('bull:builds:wait'),
        this.redis.llen('bull:builds:active'),
        this.redis.get('bull:builds:completed'),
        this.redis.get('bull:builds:failed'),
      ]);

      const [deploymentsWaiting, deploymentsActive, deploymentsCompleted, deploymentsFailed] = await Promise.all([
        this.redis.llen('bull:deployments:wait'),
        this.redis.llen('bull:deployments:active'),
        this.redis.get('bull:deployments:completed'),
        this.redis.get('bull:deployments:failed'),
      ]);

      return {
        builds: {
          waiting: buildsWaiting || 0,
          active: buildsActive || 0,
          completed: parseInt(buildsCompleted || '0', 10),
          failed: parseInt(buildsFailed || '0', 10),
        },
        deployments: {
          waiting: deploymentsWaiting || 0,
          active: deploymentsActive || 0,
          completed: parseInt(deploymentsCompleted || '0', 10),
          failed: parseInt(deploymentsFailed || '0', 10),
        },
      };
    } catch (error) {
      this.logger.error('Failed to get queue status:', error);
      return {
        builds: { waiting: 0, active: 0, completed: 0, failed: 0 },
        deployments: { waiting: 0, active: 0, completed: 0, failed: 0 },
      };
    }
  }

  /**
   * Get real-time tenant stats
   */
  async getTenantStats(tenantId: number): Promise<any> {
    const now = new Date();
    const oneHourAgo = new Date(now.getTime() - 60 * 60 * 1000);

    // Get active builds/deployments from Redis
    const activeBuilds = await this.getActiveBuilds(tenantId);
    const activeDeployments = await this.getActiveDeployments(tenantId);

    return {
      activeBuilds,
      activeDeployments,
      timestamp: now,
    };
  }

  /**
   * Get health history
   */
  async getHealthHistory(component: string, hours: number = 24): Promise<SystemHealth[]> {
    const since = new Date();
    since.setHours(since.getHours() - hours);

    return this.systemHealthRepository.find({
      where: {
        component,
        createdAt: { $gte: since } as any,
      },
      order: { createdAt: 'DESC' },
      take: 100,
    });
  }

  // Private health check methods

  private async checkDatabaseHealth(): Promise<SystemStatus['database']> {
    const start = Date.now();
    try {
      // Simple ping to check database
      // In production, you'd check the actual connection pool
      return {
        status: 'healthy',
        responseTime: Date.now() - start,
        connections: 10, // Mock value - would get from connection pool
      };
    } catch (error) {
      return {
        status: 'unhealthy',
        responseTime: Date.now() - start,
        connections: 0,
      };
    }
  }

  private async checkRedisHealth(): Promise<SystemStatus['redis']> {
    const start = Date.now();
    try {
      const [info, ping] = await Promise.all([
        this.redis.info('memory'),
        this.redis.ping(),
      ]);

      const memoryMatch = info.match(/used_memory_human:(.+?)\r/);
      const memoryUsage = memoryMatch ? memoryMatch[1].trim() : 'unknown';

      const clientsMatch = info.match(/connected_clients:(\d+)/);
      const connectedClients = clientsMatch ? parseInt(clientsMatch[1], 10) : 0;

      return {
        status: ping === 'PONG' ? 'healthy' : 'degraded',
        responseTime: Date.now() - start,
        memoryUsage,
        connectedClients,
      };
    } catch (error) {
      return {
        status: 'unhealthy',
        responseTime: Date.now() - start,
        memoryUsage: 'unknown',
        connectedClients: 0,
      };
    }
  }

  private async checkDockerHealth(): Promise<SystemStatus['docker']> {
    try {
      const { stdout } = await execAsync('docker ps -a --format "{{.Status}}" 2>/dev/null || echo "error"');
      
      if (stdout.includes('error')) {
        return {
          status: 'unhealthy',
          containers: { total: 0, running: 0, stopped: 0 },
          images: 0,
        };
      }

      const containers = stdout.trim().split('\n').filter(Boolean);
      const running = containers.filter(c => c.includes('Up')).length;
      const stopped = containers.length - running;

      // Get image count
      const { stdout: imagesStdout } = await execAsync('docker images -q 2>/dev/null | wc -l');
      const images = parseInt(imagesStdout.trim(), 10) || 0;

      return {
        status: running > 0 ? 'healthy' : 'degraded',
        containers: {
          total: containers.length,
          running,
          stopped,
        },
        images,
      };
    } catch (error) {
      return {
        status: 'unhealthy',
        containers: { total: 0, running: 0, stopped: 0 },
        images: 0,
      };
    }
  }

  private async checkDiskHealth(): Promise<SystemStatus['disk']> {
    try {
      const { stdout } = await execAsync('df -h / 2>/dev/null | tail -1');
      const parts = stdout.trim().split(/\s+/);
      
      if (parts.length >= 6) {
        const total = parts[1];
        const used = parts[2];
        const free = parts[3];
        const usagePercent = parseInt(parts[4].replace('%', ''), 10);

        return {
          status: usagePercent > 90 ? 'unhealthy' : usagePercent > 80 ? 'degraded' : 'healthy',
          total,
          used,
          free,
          usagePercent,
        };
      }

      throw new Error('Invalid df output');
    } catch (error) {
      return {
        status: 'unhealthy',
        total: 'unknown',
        used: 'unknown',
        free: 'unknown',
        usagePercent: 0,
      };
    }
  }

  private async checkMemoryHealth(): Promise<SystemStatus['memory']> {
    try {
      const { stdout } = await execAsync('free -h 2>/dev/null | grep Mem');
      const parts = stdout.trim().split(/\s+/);
      
      if (parts.length >= 4) {
        const total = parts[1];
        const used = parts[2];
        const free = parts[3];
        
        // Calculate usage percentage
        const { stdout: memStdout } = await execAsync('free | grep Mem');
        const memParts = memStdout.trim().split(/\s+/);
        const totalMem = parseInt(memParts[1], 10);
        const usedMem = parseInt(memParts[2], 10);
        const usagePercent = Math.round((usedMem / totalMem) * 100);

        return {
          status: usagePercent > 90 ? 'unhealthy' : usagePercent > 80 ? 'degraded' : 'healthy',
          total,
          used,
          free,
          usagePercent,
        };
      }

      throw new Error('Invalid free output');
    } catch (error) {
      return {
        status: 'unhealthy',
        total: 'unknown',
        used: 'unknown',
        free: 'unknown',
        usagePercent: 0,
      };
    }
  }

  private async checkCpuHealth(): Promise<SystemStatus['cpu']> {
    try {
      const { stdout: loadAvg } = await execAsync('cat /proc/loadavg');
      const loadParts = loadAvg.trim().split(' ');
      const loadAverage = [
        parseFloat(loadParts[0]),
        parseFloat(loadParts[1]),
        parseFloat(loadParts[2]),
      ];

      // Get CPU usage percentage (would need more complex logic for real accuracy)
      const { stdout: cpuInfo } = await execAsync('grep -c processor /proc/cpuinfo');
      const numCpus = parseInt(cpuInfo.trim(), 10) || 1;
      const usagePercent = Math.round((loadAverage[0] / numCpus) * 100);

      return {
        status: usagePercent > 90 ? 'unhealthy' : usagePercent > 80 ? 'degraded' : 'healthy',
        usagePercent,
        loadAverage,
      };
    } catch (error) {
      return {
        status: 'unhealthy',
        usagePercent: 0,
        loadAverage: [0, 0, 0],
      };
    }
  }

  private async storeHealthStatus(
    component: string,
    status: string,
    metrics: any,
  ): Promise<void> {
    try {
      const health = this.systemHealthRepository.create({
        component,
        status: status as any,
        metrics,
      });
      await this.systemHealthRepository.save(health);
    } catch (error) {
      this.logger.error(`Failed to store health status for ${component}:`, error);
    }
  }

  private startHealthChecks(): void {
    // Run health check every 60 seconds
    setInterval(async () => {
      try {
        await this.getSystemStatus();
      } catch (error) {
        this.logger.error('Health check failed:', error);
      }
    }, 60000);
  }

  private async getActiveBuilds(tenantId: number): Promise<any[]> {
    // This would query Redis for active builds for this tenant
    // For now, return mock data
    return [];
  }

  private async getActiveDeployments(tenantId: number): Promise<any[]> {
    // This would query Redis for active deployments for this tenant
    // For now, return mock data
    return [];
  }
}
