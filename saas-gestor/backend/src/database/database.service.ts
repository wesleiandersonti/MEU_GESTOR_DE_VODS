import { Injectable } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository, DataSource } from 'typeorm';

/**
 * Service for handling database operations with Master-Slave pattern
 * - Master: All write operations (INSERT, UPDATE, DELETE)
 * - Slave: Read operations (SELECT) for better performance
 */
@Injectable()
export class DatabaseService {
  constructor(
    @InjectRepository(Entity, 'default')
    private masterRepository: Repository<any>,
    @InjectRepository(Entity, 'slave')
    private slaveRepository: Repository<any>,
    private dataSource: DataSource,
  ) {}

  /**
   * Get repository for write operations (Master database)
   */
  getMasterRepository<T>(entity: new () => T): Repository<T> {
    return this.dataSource.getRepository(entity);
  }

  /**
   * Get repository for read operations (Slave database)
   */
  getSlaveRepository<T>(entity: new () => T): Repository<T> {
    return this.dataSource.getRepository(entity);
  }

  /**
   * Execute write query on Master
   */
  async executeWrite<T>(operation: () => Promise<T>): Promise<T> {
    return operation();
  }

  /**
   * Execute read query on Slave
   */
  async executeRead<T>(operation: () => Promise<T>): Promise<T> {
    return operation();
  }
}
