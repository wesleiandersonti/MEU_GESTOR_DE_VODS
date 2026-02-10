import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository, Between } from 'typeorm';
import {
  AnalyticsMetric,
  AnalyticsDailySummary,
  MetricType,
} from './entities/analytics-metric.entity';

export interface MetricFilter {
  tenantId: number;
  applicationId?: number;
  startDate?: Date;
  endDate?: Date;
  metricTypes?: MetricType[];
}

export interface DashboardStats {
  builds: {
    total: number;
    success: number;
    failed: number;
    successRate: number;
    avgDuration: number;
    trend: 'up' | 'down' | 'stable';
  };
  deployments: {
    total: number;
    success: number;
    failed: number;
    rolledBack: number;
    successRate: number;
    avgDuration: number;
    trend: 'up' | 'down' | 'stable';
  };
  databases: {
    totalConnections: number;
    activeConnections: number;
    totalQueries: number;
    avgQueryTime: number;
  };
  applications: {
    total: number;
    active: number;
    deploying: number;
    error: number;
  };
}

export interface TimeSeriesData {
  labels: string[];
  datasets: {
    label: string;
    data: number[];
    color?: string;
  }[];
}

@Injectable()
export class AnalyticsService {
  private readonly logger = new Logger(AnalyticsService.name);

  constructor(
    @InjectRepository(AnalyticsMetric)
    private metricRepository: Repository<AnalyticsMetric>,
    @InjectRepository(AnalyticsDailySummary)
    private summaryRepository: Repository<AnalyticsDailySummary>,
  ) {}

  /**
   * Track a metric event
   */
  async trackMetric(
    tenantId: number,
    metricType: MetricType,
    metadata: any = {},
    applicationId?: number,
    value: number = 1,
  ): Promise<void> {
    try {
      const metric = this.metricRepository.create({
        tenantId,
        applicationId,
        metricType,
        metadata,
        value,
      });

      await this.metricRepository.save(metric);

      // Update daily summary
      await this.updateDailySummary(tenantId, applicationId, metricType, metadata);
    } catch (error) {
      this.logger.error('Failed to track metric:', error);
    }
  }

  /**
   * Get dashboard statistics
   */
  async getDashboardStats(tenantId: number, days: number = 30): Promise<DashboardStats> {
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - days);

    // Get previous period for trend calculation
    const prevStartDate = new Date(startDate);
    prevStartDate.setDate(prevStartDate.getDate() - days);
    const prevEndDate = new Date(startDate);

    // Current period metrics
    const currentMetrics = await this.getMetricsForPeriod(tenantId, startDate, endDate);
    
    // Previous period metrics for trend
    const prevMetrics = await this.getMetricsForPeriod(tenantId, prevStartDate, prevEndDate);

    return {
      builds: {
        total: currentMetrics.builds.total,
        success: currentMetrics.builds.success,
        failed: currentMetrics.builds.failed,
        successRate: this.calculateRate(currentMetrics.builds.success, currentMetrics.builds.total),
        avgDuration: currentMetrics.builds.avgDuration,
        trend: this.calculateTrend(
          this.calculateRate(currentMetrics.builds.success, currentMetrics.builds.total),
          this.calculateRate(prevMetrics.builds.success, prevMetrics.builds.total),
        ),
      },
      deployments: {
        total: currentMetrics.deployments.total,
        success: currentMetrics.deployments.success,
        failed: currentMetrics.deployments.failed,
        rolledBack: currentMetrics.deployments.rolledBack,
        successRate: this.calculateRate(currentMetrics.deployments.success, currentMetrics.deployments.total),
        avgDuration: currentMetrics.deployments.avgDuration,
        trend: this.calculateTrend(
          this.calculateRate(currentMetrics.deployments.success, currentMetrics.deployments.total),
          this.calculateRate(prevMetrics.deployments.success, prevMetrics.deployments.total),
        ),
      },
      databases: {
        totalConnections: currentMetrics.databases.totalConnections,
        activeConnections: currentMetrics.databases.activeConnections,
        totalQueries: currentMetrics.databases.totalQueries,
        avgQueryTime: currentMetrics.databases.avgQueryTime,
      },
      applications: {
        total: currentMetrics.applications.total,
        active: currentMetrics.applications.active,
        deploying: currentMetrics.applications.deploying,
        error: currentMetrics.applications.error,
      },
    };
  }

  /**
   * Get time series data for charts
   */
  async getTimeSeriesData(
    tenantId: number,
    metricType: MetricType,
    days: number = 7,
    applicationId?: number,
  ): Promise<TimeSeriesData> {
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - days);

    const where: any = {
      tenantId,
      metricType,
      createdAt: Between(startDate, endDate),
    };

    if (applicationId) {
      where.applicationId = applicationId;
    }

    const metrics = await this.metricRepository.find({
      where,
      order: { createdAt: 'ASC' },
    });

    // Group by day
    const dailyData = new Map<string, number>();
    
    for (let i = 0; i <= days; i++) {
      const date = new Date(startDate);
      date.setDate(date.getDate() + i);
      const dateKey = date.toISOString().split('T')[0];
      dailyData.set(dateKey, 0);
    }

    metrics.forEach((metric) => {
      const dateKey = metric.createdAt.toISOString().split('T')[0];
      dailyData.set(dateKey, (dailyData.get(dateKey) || 0) + metric.value);
    });

    return {
      labels: Array.from(dailyData.keys()),
      datasets: [
        {
          label: this.getMetricLabel(metricType),
          data: Array.from(dailyData.values()),
          color: this.getMetricColor(metricType),
        },
      ],
    };
  }

  /**
   * Get application-specific analytics
   */
  async getApplicationAnalytics(
    applicationId: number,
    tenantId: number,
    days: number = 30,
  ): Promise<any> {
    const endDate = new Date();
    const startDate = new Date();
    startDate.setDate(startDate.getDate() - days);

    const [builds, deployments] = await Promise.all([
      this.metricRepository.find({
        where: {
          tenantId,
          applicationId,
          metricType: [
            MetricType.BUILD_STARTED,
            MetricType.BUILD_COMPLETED,
            MetricType.BUILD_FAILED,
          ],
          createdAt: Between(startDate, endDate),
        },
        order: { createdAt: 'DESC' },
      }),
      this.metricRepository.find({
        where: {
          tenantId,
          applicationId,
          metricType: [
            MetricType.DEPLOYMENT_STARTED,
            MetricType.DEPLOYMENT_COMPLETED,
            MetricType.DEPLOYMENT_FAILED,
            MetricType.DEPLOYMENT_ROLLED_BACK,
          ],
          createdAt: Between(startDate, endDate),
        },
        order: { createdAt: 'DESC' },
      }),
    ]);

    const buildStats = this.calculateBuildStats(builds);
    const deploymentStats = this.calculateDeploymentStats(deployments);

    return {
      builds: buildStats,
      deployments: deploymentStats,
      timeline: this.generateTimeline(builds, deployments),
    };
  }

  /**
   * Get recent activity feed
   */
  async getRecentActivity(
    tenantId: number,
    limit: number = 50,
    applicationId?: number,
  ): Promise<any[]> {
    const where: any = { tenantId };
    if (applicationId) {
      where.applicationId = applicationId;
    }

    const metrics = await this.metricRepository.find({
      where,
      order: { createdAt: 'DESC' },
      take: limit,
    });

    return metrics.map((metric) => ({
      id: metric.id,
      type: metric.metricType,
      applicationId: metric.applicationId,
      timestamp: metric.createdAt,
      details: this.formatActivityDetails(metric),
    }));
  }

  /**
   * Aggregate daily summaries
   */
  async aggregateDailySummary(date: Date): Promise<void> {
    const startOfDay = new Date(date);
    startOfDay.setHours(0, 0, 0, 0);
    
    const endOfDay = new Date(date);
    endOfDay.setHours(23, 59, 59, 999);

    // Get all metrics for the day
    const metrics = await this.metricRepository.find({
      where: {
        createdAt: Between(startOfDay, endOfDay),
      },
    });

    // Group by tenant and application
    const grouped = new Map<string, AnalyticsDailySummary>();

    metrics.forEach((metric) => {
      const key = `${metric.tenantId}-${metric.applicationId || 'null'}`;
      
      if (!grouped.has(key)) {
        grouped.set(key, this.summaryRepository.create({
          tenantId: metric.tenantId,
          applicationId: metric.applicationId,
          date: startOfDay,
        }));
      }

      const summary = grouped.get(key)!;
      this.aggregateMetricToSummary(summary, metric);
    });

    // Save all summaries
    for (const summary of grouped.values()) {
      await this.summaryRepository.save(summary);
    }
  }

  // Private helper methods

  private async updateDailySummary(
    tenantId: number,
    applicationId: number | undefined,
    metricType: MetricType,
    metadata: any,
  ): Promise<void> {
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    let summary = await this.summaryRepository.findOne({
      where: {
        tenantId,
        applicationId: applicationId || null,
        date: today,
      },
    });

    if (!summary) {
      summary = this.summaryRepository.create({
        tenantId,
        applicationId: applicationId || null,
        date: today,
      });
    }

    this.aggregateMetricToSummary(summary, { metricType, metadata } as AnalyticsMetric);
    await this.summaryRepository.save(summary);
  }

  private aggregateMetricToSummary(summary: AnalyticsDailySummary, metric: Partial<AnalyticsMetric>): void {
    switch (metric.metricType) {
      case MetricType.BUILD_STARTED:
        summary.buildsStarted++;
        break;
      case MetricType.BUILD_COMPLETED:
        summary.buildsCompleted++;
        if (metric.metadata?.duration) {
          summary.totalBuildDuration += metric.metadata.duration;
        }
        break;
      case MetricType.BUILD_FAILED:
        summary.buildsFailed++;
        break;
      case MetricType.DEPLOYMENT_STARTED:
        summary.deploymentsStarted++;
        break;
      case MetricType.DEPLOYMENT_COMPLETED:
        summary.deploymentsCompleted++;
        if (metric.metadata?.duration) {
          summary.totalDeploymentDuration += metric.metadata.duration;
        }
        break;
      case MetricType.DEPLOYMENT_FAILED:
        summary.deploymentsFailed++;
        break;
      case MetricType.DEPLOYMENT_ROLLED_BACK:
        summary.deploymentsRolledBack++;
        break;
      case MetricType.QUERY_EXECUTED:
        summary.queriesExecuted++;
        if (metric.metadata?.rowCount) {
          summary.totalRowsReturned += metric.metadata.rowCount;
        }
        break;
    }
  }

  private async getMetricsForPeriod(
    tenantId: number,
    startDate: Date,
    endDate: Date,
  ): Promise<any> {
    const metrics = await this.metricRepository.find({
      where: {
        tenantId,
        createdAt: Between(startDate, endDate),
      },
    });

    return {
      builds: {
        total: metrics.filter((m) => m.metricType === MetricType.BUILD_STARTED).length,
        success: metrics.filter((m) => m.metricType === MetricType.BUILD_COMPLETED).length,
        failed: metrics.filter((m) => m.metricType === MetricType.BUILD_FAILED).length,
        avgDuration: this.calculateAvgDuration(
          metrics.filter((m) => m.metricType === MetricType.BUILD_COMPLETED),
        ),
      },
      deployments: {
        total: metrics.filter((m) => m.metricType === MetricType.DEPLOYMENT_STARTED).length,
        success: metrics.filter((m) => m.metricType === MetricType.DEPLOYMENT_COMPLETED).length,
        failed: metrics.filter((m) => m.metricType === MetricType.DEPLOYMENT_FAILED).length,
        rolledBack: metrics.filter((m) => m.metricType === MetricType.DEPLOYMENT_ROLLED_BACK).length,
        avgDuration: this.calculateAvgDuration(
          metrics.filter((m) => m.metricType === MetricType.DEPLOYMENT_COMPLETED),
        ),
      },
      databases: {
        totalConnections: 0, // Would need to query DatabaseConnection entity
        activeConnections: 0,
        totalQueries: metrics.filter((m) => m.metricType === MetricType.QUERY_EXECUTED).length,
        avgQueryTime: 0,
      },
      applications: {
        total: 0,
        active: 0,
        deploying: 0,
        error: 0,
      },
    };
  }

  private calculateRate(success: number, total: number): number {
    return total > 0 ? Math.round((success / total) * 100) : 0;
  }

  private calculateAvgDuration(metrics: AnalyticsMetric[]): number {
    if (metrics.length === 0) return 0;
    const total = metrics.reduce((sum, m) => sum + (m.metadata?.duration || 0), 0);
    return Math.round(total / metrics.length);
  }

  private calculateTrend(current: number, previous: number): 'up' | 'down' | 'stable' {
    const diff = current - previous;
    const threshold = 5; // 5% threshold
    
    if (diff > threshold) return 'up';
    if (diff < -threshold) return 'down';
    return 'stable';
  }

  private getMetricLabel(metricType: MetricType): string {
    const labels = {
      [MetricType.BUILD_STARTED]: 'Builds Started',
      [MetricType.BUILD_COMPLETED]: 'Builds Completed',
      [MetricType.BUILD_FAILED]: 'Builds Failed',
      [MetricType.DEPLOYMENT_STARTED]: 'Deployments Started',
      [MetricType.DEPLOYMENT_COMPLETED]: 'Deployments Completed',
      [MetricType.DEPLOYMENT_FAILED]: 'Deployments Failed',
      [MetricType.DEPLOYMENT_ROLLED_BACK]: 'Deployments Rolled Back',
      [MetricType.QUERY_EXECUTED]: 'Queries Executed',
      [MetricType.DB_CONNECTION_TEST]: 'DB Connection Tests',
    };
    return labels[metricType] || metricType;
  }

  private getMetricColor(metricType: MetricType): string {
    const colors = {
      [MetricType.BUILD_COMPLETED]: '#10B981',
      [MetricType.BUILD_FAILED]: '#EF4444',
      [MetricType.DEPLOYMENT_COMPLETED]: '#10B981',
      [MetricType.DEPLOYMENT_FAILED]: '#EF4444',
      [MetricType.DEPLOYMENT_ROLLED_BACK]: '#F59E0B',
    };
    return colors[metricType] || '#6B7280';
  }

  private formatActivityDetails(metric: AnalyticsMetric): string {
    switch (metric.metricType) {
      case MetricType.BUILD_COMPLETED:
        return `Build completed in ${metric.metadata?.duration}s`;
      case MetricType.BUILD_FAILED:
        return `Build failed: ${metric.metadata?.errorMessage || 'Unknown error'}`;
      case MetricType.DEPLOYMENT_COMPLETED:
        return `Deployment completed in ${metric.metadata?.duration}s`;
      case MetricType.DEPLOYMENT_FAILED:
        return `Deployment failed: ${metric.metadata?.errorMessage || 'Unknown error'}`;
      case MetricType.DEPLOYMENT_ROLLED_BACK:
        return 'Deployment rolled back';
      case MetricType.QUERY_EXECUTED:
        return `Query executed (${metric.metadata?.rowCount || 0} rows)`;
      default:
        return metric.metricType;
    }
  }

  private calculateBuildStats(metrics: AnalyticsMetric[]): any {
    const started = metrics.filter((m) => m.metricType === MetricType.BUILD_STARTED).length;
    const completed = metrics.filter((m) => m.metricType === MetricType.BUILD_COMPLETED).length;
    const failed = metrics.filter((m) => m.metricType === MetricType.BUILD_FAILED).length;

    return {
      total: started,
      success: completed,
      failed,
      successRate: this.calculateRate(completed, started),
      avgDuration: this.calculateAvgDuration(
        metrics.filter((m) => m.metricType === MetricType.BUILD_COMPLETED),
      ),
    };
  }

  private calculateDeploymentStats(metrics: AnalyticsMetric[]): any {
    const started = metrics.filter((m) => m.metricType === MetricType.DEPLOYMENT_STARTED).length;
    const completed = metrics.filter((m) => m.metricType === MetricType.DEPLOYMENT_COMPLETED).length;
    const failed = metrics.filter((m) => m.metricType === MetricType.DEPLOYMENT_FAILED).length;
    const rolledBack = metrics.filter((m) => m.metricType === MetricType.DEPLOYMENT_ROLLED_BACK).length;

    return {
      total: started,
      success: completed,
      failed,
      rolledBack,
      successRate: this.calculateRate(completed, started),
      avgDuration: this.calculateAvgDuration(
        metrics.filter((m) => m.metricType === MetricType.DEPLOYMENT_COMPLETED),
      ),
    };
  }

  private generateTimeline(builds: AnalyticsMetric[], deployments: AnalyticsMetric[]): any[] {
    const allEvents = [...builds, ...deployments];
    allEvents.sort((a, b) => b.createdAt.getTime() - a.createdAt.getTime());

    return allEvents.slice(0, 20).map((event) => ({
      timestamp: event.createdAt,
      type: event.metricType,
      applicationId: event.applicationId,
      metadata: event.metadata,
    }));
  }
}
