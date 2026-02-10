import {
  Entity,
  PrimaryGeneratedColumn,
  Column,
  CreateDateColumn,
  Index,
} from 'typeorm';

export enum MetricType {
  BUILD_STARTED = 'build_started',
  BUILD_COMPLETED = 'build_completed',
  BUILD_FAILED = 'build_failed',
  DEPLOYMENT_STARTED = 'deployment_started',
  DEPLOYMENT_COMPLETED = 'deployment_completed',
  DEPLOYMENT_FAILED = 'deployment_failed',
  DEPLOYMENT_ROLLED_BACK = 'deployment_rolled_back',
  QUERY_EXECUTED = 'query_executed',
  DB_CONNECTION_TEST = 'db_connection_test',
}

@Entity('analytics_metrics')
@Index(['tenantId', 'metricType', 'createdAt'])
@Index(['applicationId', 'createdAt'])
@Index(['createdAt'])
export class AnalyticsMetric {
  @PrimaryGeneratedColumn()
  id: number;

  @Column({ type: 'int' })
  tenantId: number;

  @Column({ type: 'int', nullable: true })
  applicationId: number;

  @Column({
    type: 'enum',
    enum: MetricType,
  })
  metricType: MetricType;

  @Column({ type: 'json', nullable: true })
  metadata: {
    buildId?: number;
    deploymentId?: number;
    duration?: number;
    errorMessage?: string;
    commitHash?: string;
    userId?: number;
    databaseConnectionId?: number;
    rowCount?: number;
    [key: string]: any;
  };

  @Column({ type: 'int', default: 1 })
  value: number;

  @CreateDateColumn()
  createdAt: Date;
}

@Entity('analytics_daily_summary')
@Index(['tenantId', 'date'])
@Index(['applicationId', 'date'])
export class AnalyticsDailySummary {
  @PrimaryGeneratedColumn()
  id: number;

  @Column({ type: 'int' })
  tenantId: number;

  @Column({ type: 'int', nullable: true })
  applicationId: number;

  @Column({ type: 'date' })
  date: Date;

  // Build metrics
  @Column({ type: 'int', default: 0 })
  buildsStarted: number;

  @Column({ type: 'int', default: 0 })
  buildsCompleted: number;

  @Column({ type: 'int', default: 0 })
  buildsFailed: number;

  @Column({ type: 'int', default: 0 })
  totalBuildDuration: number; // in seconds

  // Deployment metrics
  @Column({ type: 'int', default: 0 })
  deploymentsStarted: number;

  @Column({ type: 'int', default: 0 })
  deploymentsCompleted: number;

  @Column({ type: 'int', default: 0 })
  deploymentsFailed: number;

  @Column({ type: 'int', default: 0 })
  deploymentsRolledBack: number;

  @Column({ type: 'int', default: 0 })
  totalDeploymentDuration: number; // in seconds

  // Database metrics
  @Column({ type: 'int', default: 0 })
  queriesExecuted: number;

  @Column({ type: 'bigint', default: 0 })
  totalRowsReturned: number;

  @CreateDateColumn()
  createdAt: Date;

  @Column({ type: 'timestamp', default: () => 'CURRENT_TIMESTAMP', onUpdate: 'CURRENT_TIMESTAMP' })
  updatedAt: Date;
}

@Entity('system_health')
export class SystemHealth {
  @PrimaryGeneratedColumn()
  id: number;

  @Column({ type: 'varchar', length: 50 })
  component: string; // 'database', 'redis', 'docker', 'nginx', etc.

  @Column({ type: 'varchar', length: 20 })
  status: 'healthy' | 'degraded' | 'unhealthy';

  @Column({ type: 'text', nullable: true })
  message: string;

  @Column({ type: 'json', nullable: true })
  metrics: {
    responseTime?: number;
    cpuUsage?: number;
    memoryUsage?: number;
    diskUsage?: number;
    connections?: number;
    [key: string]: any;
  };

  @CreateDateColumn()
  createdAt: Date;
}
