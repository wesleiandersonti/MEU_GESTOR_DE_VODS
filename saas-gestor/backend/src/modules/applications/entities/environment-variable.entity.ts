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

export enum EnvironmentVariableScope {
  APPLICATION = 'application',
  BUILD = 'build',
  RUNTIME = 'runtime',
}

@Entity('environment_variables')
@Index(['applicationId'])
@Index(['key'])
@Index(['scope'])
@Index(['isSecret'])
export class EnvironmentVariable {
  @PrimaryGeneratedColumn()
  id: number;

  @Column({ type: 'int' })
  applicationId: number;

  @ManyToOne(() => Application, (app) => app.environmentVariables, {
    onDelete: 'CASCADE',
  })
  @JoinColumn({ name: 'applicationId' })
  application: Application;

  @Column({ type: 'varchar', length: 255 })
  key: string;

  @Column({ type: 'text' })
  // Encrypted if isSecret=true, plain text otherwise
  value: string;

  @Column({
    type: 'enum',
    enum: EnvironmentVariableScope,
    default: EnvironmentVariableScope.APPLICATION,
  })
  scope: EnvironmentVariableScope;

  @Column({ type: 'boolean', default: false })
  isSecret: boolean; // If true, value is encrypted

  @Column({ type: 'text', nullable: true })
  description: string;

  @Column({ type: 'boolean', default: true })
  isActive: boolean;

  @Column({ type: 'int', nullable: true })
  createdById: number;

  @CreateDateColumn()
  createdAt: Date;

  @UpdateDateColumn()
  updatedAt: Date;
}
