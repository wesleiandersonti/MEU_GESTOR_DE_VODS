import {
  Entity,
  PrimaryGeneratedColumn,
  Column,
  CreateDateColumn,
  UpdateDateColumn,
  ManyToOne,
  JoinColumn,
  Index,
} from 'typeorm';
import { Application } from './application.entity';
import { Build } from './build.entity';

export enum DeploymentStatus {
  PENDING = 'pending',
  QUEUED = 'queued',
  RUNNING = 'running',
  SUCCESS = 'success',
  FAILED = 'failed',
  ROLLING_BACK = 'rolling_back',
  ROLLED_BACK = 'rolled_back',
}

export enum DeploymentType {
  BLUE_GREEN = 'blue_green',
  ROLLING = 'rolling',
  RECREATE = 'recreate',
}

@Entity('deployments')
@Index(['applicationId'])
@Index(['status'])
@Index(['createdAt'])
export class Deployment {
  @PrimaryGeneratedColumn()
  id: number;

  @Column({ type: 'int' })
  applicationId: number;

  @ManyToOne(() => Application, (app) => app.deployments, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'applicationId' })
  application: Application;

  @Column({ type: 'int' })
  buildId: number;

  @ManyToOne(() => Build, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'buildId' })
  build: Build;

  @Column({ type: 'int' })
  deploymentNumber: number;

  @Column({
    type: 'enum',
    enum: DeploymentStatus,
    default: DeploymentStatus.PENDING,
  })
  status: DeploymentStatus;

  @Column({
    type: 'enum',
    enum: DeploymentType,
    default: DeploymentType.BLUE_GREEN,
  })
  deploymentType: DeploymentType;

  @Column({ type: 'varchar', length: 50, nullable: true })
  stage: string; // 'preparing', 'deploying', 'health_check', 'completing'

  @Column({ type: 'int', default: 0 })
  progress: number; // 0-100

  @Column({ type: 'text', nullable: true })
  logs: string;

  @Column({ type: 'text', nullable: true })
  errorMessage: string;

  @Column({ type: 'varchar', length: 100, nullable: true })
  containerId: string; // Docker container ID

  @Column({ type: 'int', nullable: true })
  containerPort: number; // Port where container is exposed

  @Column({ type: 'varchar', length: 50, nullable: true })
  deployedColor: string; // 'blue' or 'green' for blue-green deployments

  @Column({ type: 'varchar', length: 255, nullable: true })
  previousDeploymentId: string; // For rollback

  @Column({ type: 'int', nullable: true })
  duration: number; // in seconds

  @Column({ type: 'timestamp', nullable: true })
  startedAt: Date;

  @Column({ type: 'timestamp', nullable: true })
  completedAt: Date;

  @Column({ type: 'timestamp', nullable: true })
  rolledBackAt: Date;

  @Column({ type: 'int', nullable: true })
  triggeredById: number;

  @Column({ type: 'varchar', length: 50, nullable: true })
  triggerType: string; // 'manual', 'auto_after_build', 'schedule'

  @CreateDateColumn()
  createdAt: Date;

  @UpdateDateColumn()
  updatedAt: Date;
}
