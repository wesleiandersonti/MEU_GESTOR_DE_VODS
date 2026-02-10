import {
  Entity,
  PrimaryGeneratedColumn,
  Column,
  CreateDateColumn,
  UpdateDateColumn,
  ManyToOne,
  JoinColumn,
  OneToMany,
  Index,
} from 'typeorm';
import { Tenant } from '../../tenants/entities/tenant.entity';
import { User } from '../../users/entities/user.entity';
import { Build } from './build.entity';
import { Deployment } from './deployment.entity';
import { DatabaseConnection } from './database-connection.entity';
import { EnvironmentVariable } from './environment-variable.entity';

export enum ApplicationType {
  NODE = 'node',
  PHP = 'php',
  PYTHON = 'python',
  STATIC = 'static',
  DOCKER = 'docker',
}

export enum ApplicationEnvironment {
  DEVELOPMENT = 'development',
  STAGING = 'staging',
  PRODUCTION = 'production',
}

export enum ApplicationStatus {
  ACTIVE = 'active',
  INACTIVE = 'inactive',
  ERROR = 'error',
  DEPLOYING = 'deploying',
  BUILDING = 'building',
}

@Entity('applications')
@Index(['tenantId', 'slug'], { unique: true })
@Index(['tenantId'])
@Index(['status'])
@Index(['environment'])
export class Application {
  @PrimaryGeneratedColumn()
  id: number;

  @Column({ type: 'int' })
  tenantId: number;

  @ManyToOne(() => Tenant, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'tenantId' })
  tenant: Tenant;

  @Column({ type: 'int', nullable: true })
  createdById: number;

  @ManyToOne(() => User)
  @JoinColumn({ name: 'createdById' })
  createdBy: User;

  @Column({ type: 'varchar', length: 255 })
  name: string;

  @Column({ type: 'varchar', length: 100 })
  slug: string;

  @Column({ type: 'text', nullable: true })
  description: string;

  @Column({
    type: 'enum',
    enum: ApplicationType,
    default: ApplicationType.NODE,
  })
  type: ApplicationType;

  @Column({
    type: 'enum',
    enum: ApplicationEnvironment,
    default: ApplicationEnvironment.DEVELOPMENT,
  })
  environment: ApplicationEnvironment;

  @Column({
    type: 'enum',
    enum: ApplicationStatus,
    default: ApplicationStatus.INACTIVE,
  })
  status: ApplicationStatus;

  @Column({ type: 'varchar', length: 500, nullable: true })
  repositoryUrl: string;

  @Column({ type: 'varchar', length: 100, default: 'main' })
  repositoryBranch: string;

  @Column({ type: 'varchar', length: 255, nullable: true })
  domain: string;

  @Column({ type: 'int', nullable: true })
  port: number;

  @Column({ type: 'varchar', length: 255, nullable: true })
  dockerImage: string;

  @Column({ type: 'text', nullable: true })
  envVars: string; // JSON string

  @Column({ type: 'text', nullable: true })
  buildCommand: string;

  @Column({ type: 'text', nullable: true })
  startCommand: string;

  @Column({ type: 'varchar', length: 500, nullable: true })
  healthCheckUrl: string;

  @Column({ type: 'varchar', length: 50, nullable: true })
  currentVersion: string;

  @Column({ type: 'int', nullable: true })
  currentBuildId: number;

  @Column({ type: 'timestamp', nullable: true })
  lastDeployedAt: Date;

  @Column({ type: 'timestamp', nullable: true })
  lastBuildAt: Date;

  @OneToMany(() => Build, (build) => build.application)
  builds: Build[];

  @OneToMany(() => Deployment, (deployment) => deployment.application)
  deployments: Deployment[];

  @OneToMany(() => DatabaseConnection, (db) => db.application)
  databaseConnections: DatabaseConnection[];

  @OneToMany(() => EnvironmentVariable, (env) => env.application)
  environmentVariables: EnvironmentVariable[];

  @CreateDateColumn()
  createdAt: Date;

  @UpdateDateColumn()
  updatedAt: Date;
}
