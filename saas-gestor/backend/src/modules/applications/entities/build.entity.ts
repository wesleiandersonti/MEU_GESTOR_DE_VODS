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

export enum BuildStatus {
  PENDING = 'pending',
  QUEUED = 'queued',
  RUNNING = 'running',
  SUCCESS = 'success',
  FAILED = 'failed',
  CANCELLED = 'cancelled',
}

@Entity('builds')
@Index(['applicationId'])
@Index(['status'])
@Index(['createdAt'])
export class Build {
  @PrimaryGeneratedColumn()
  id: number;

  @Column({ type: 'int' })
  applicationId: number;

  @ManyToOne(() => Application, (app) => app.builds, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'applicationId' })
  application: Application;

  @Column({ type: 'int' })
  buildNumber: number;

  @Column({ type: 'varchar', length: 100, nullable: true })
  commitHash: string;

  @Column({ type: 'text', nullable: true })
  commitMessage: string;

  @Column({ type: 'varchar', length: 255, nullable: true })
  commitAuthor: string;

  @Column({
    type: 'enum',
    enum: BuildStatus,
    default: BuildStatus.PENDING,
  })
  status: BuildStatus;

  @Column({ type: 'varchar', length: 50, nullable: true })
  stage: string; // 'cloning', 'installing', 'building', 'testing', 'deploying'

  @Column({ type: 'int', default: 0 })
  progress: number; // 0-100

  @Column({ type: 'text', nullable: true })
  logs: string;

  @Column({ type: 'text', nullable: true })
  errorMessage: string;

  @Column({ type: 'varchar', length: 500, nullable: true })
  artifactPath: string;

  @Column({ type: 'int', nullable: true })
  duration: number; // in seconds

  @Column({ type: 'timestamp', nullable: true })
  startedAt: Date;

  @Column({ type: 'timestamp', nullable: true })
  completedAt: Date;

  @Column({ type: 'int', nullable: true })
  triggeredById: number;

  @Column({ type: 'varchar', length: 50, nullable: true })
  triggerType: string; // 'manual', 'git_webhook', 'schedule'

  @CreateDateColumn()
  createdAt: Date;

  @UpdateDateColumn()
  updatedAt: Date;
}
