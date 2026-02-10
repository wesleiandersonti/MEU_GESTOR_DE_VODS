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
import { Tenant } from '../../tenants/entities/tenant.entity';
import { User } from '../../users/entities/user.entity';
import { Application } from './application.entity';

export enum DatabaseType {
  MYSQL = 'mysql',
  MARIADB = 'mariadb',
  POSTGRESQL = 'postgresql',
  MONGODB = 'mongodb',
  REDIS = 'redis',
  SQLITE = 'sqlite',
}

export enum ConnectionStatus {
  ACTIVE = 'active',
  INACTIVE = 'inactive',
  ERROR = 'error',
  TESTING = 'testing',
}

@Entity('database_connections')
@Index(['tenantId'])
@Index(['applicationId'])
@Index(['status'])
@Index(['type'])
export class DatabaseConnection {
  @PrimaryGeneratedColumn()
  id: number;

  @Column({ type: 'int' })
  tenantId: number;

  @ManyToOne(() => Tenant, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'tenantId' })
  tenant: Tenant;

  @Column({ type: 'int', nullable: true })
  applicationId: number;

  @ManyToOne(() => Application, (app) => app.databaseConnections, {
    onDelete: 'SET NULL',
    nullable: true,
  })
  @JoinColumn({ name: 'applicationId' })
  application: Application;

  @Column({ type: 'int', nullable: true })
  createdById: number;

  @ManyToOne(() => User)
  @JoinColumn({ name: 'createdById' })
  createdBy: User;

  @Column({ type: 'varchar', length: 255 })
  name: string;

  @Column({ type: 'text', nullable: true })
  description: string;

  @Column({
    type: 'enum',
    enum: DatabaseType,
    default: DatabaseType.MARIADB,
  })
  type: DatabaseType;

  @Column({
    type: 'enum',
    enum: ConnectionStatus,
    default: ConnectionStatus.INACTIVE,
  })
  status: ConnectionStatus;

  @Column({ type: 'varchar', length: 255 })
  host: string;

  @Column({ type: 'int', default: 3306 })
  port: number;

  @Column({ type: 'varchar', length: 255 })
  database: string;

  @Column({ type: 'varchar', length: 255 })
  username: string;

  @Column({ type: 'text' }) // Encrypted
  password: string;

  @Column({ type: 'text', nullable: true })
  connectionOptions: string; // JSON string for additional options

  @Column({ type: 'boolean', default: false })
  isXuiOne: boolean; // XUI One read-only connection

  @Column({ type: 'varchar', length: 500, nullable: true })
  xuiOneUrl: string; // XUI One API URL if applicable

  @Column({ type: 'varchar', length: 255, nullable: true })
  xuiOneApiKey: string; // Encrypted API key

  @Column({ type: 'int', default: 0 })
  queryCount: number; // Statistics

  @Column({ type: 'bigint', default: 0 })
  totalRowsReturned: number; // Statistics

  @Column({ type: 'timestamp', nullable: true })
  lastConnectedAt: Date;

  @Column({ type: 'text', nullable: true })
  lastError: string;

  @Column({ type: 'boolean', default: true })
  allowWrite: boolean; // For XUI One connections, always false

  @Column({ type: 'int', default: 1000 })
  maxRowsPerQuery: number; // Safety limit

  @Column({ type: 'int', default: 30 })
  queryTimeout: number; // In seconds

  @CreateDateColumn()
  createdAt: Date;

  @UpdateDateColumn()
  updatedAt: Date;
}
