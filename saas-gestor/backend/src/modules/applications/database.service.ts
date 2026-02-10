import {
  Injectable,
  NotFoundException,
  BadRequestException,
  Inject,
} from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { CACHE_MANAGER } from '@nestjs/cache-manager';
import { Cache } from 'cache-manager';
import { createConnection, Connection } from 'mysql2/promise';
import { Client as PgClient } from 'pg';
import { MongoClient } from 'mongodb';
import * as crypto from 'crypto';
import {
  DatabaseConnection,
  DatabaseType,
  ConnectionStatus,
} from './entities/database-connection.entity';
import { ApplicationsService } from './applications.service';

// Encryption key should be in environment variables
const ENCRYPTION_KEY = process.env.DB_ENCRYPTION_KEY || 'default-key-32chars-long!!!!!12345';

export interface CreateDatabaseConnectionDto {
  name: string;
  description?: string;
  type: DatabaseType;
  host: string;
  port: number;
  database: string;
  username: string;
  password: string;
  connectionOptions?: Record<string, any>;
  applicationId?: number;
  maxRowsPerQuery?: number;
  queryTimeout?: number;
}

export interface UpdateDatabaseConnectionDto {
  name?: string;
  description?: string;
  host?: string;
  port?: number;
  database?: string;
  username?: string;
  password?: string;
  connectionOptions?: Record<string, any>;
  maxRowsPerQuery?: number;
  queryTimeout?: number;
}

export interface ExecuteQueryDto {
  query: string;
  params?: any[];
}

export interface QueryResult {
  columns: string[];
  rows: any[];
  rowCount: number;
  executionTime: number;
  query: string;
}

@Injectable()
export class DatabaseService {
  private connections: Map<number, Connection> = new Map();

  constructor(
    @InjectRepository(DatabaseConnection)
    private dbConnectionRepository: Repository<DatabaseConnection>,
    @Inject(CACHE_MANAGER) private cacheManager: Cache,
    private applicationsService: ApplicationsService,
  ) {}

  /**
   * Encrypt sensitive data
   */
  private encrypt(text: string): string {
    const iv = crypto.randomBytes(16);
    const cipher = crypto.createCipheriv(
      'aes-256-cbc',
      Buffer.from(ENCRYPTION_KEY.padEnd(32).slice(0, 32)),
      iv,
    );
    let encrypted = cipher.update(text, 'utf8', 'hex');
    encrypted += cipher.final('hex');
    return iv.toString('hex') + ':' + encrypted;
  }

  /**
   * Decrypt sensitive data
   */
  private decrypt(encryptedText: string): string {
    const parts = encryptedText.split(':');
    const iv = Buffer.from(parts[0], 'hex');
    const encrypted = parts[1];
    const decipher = crypto.createDecipheriv(
      'aes-256-cbc',
      Buffer.from(ENCRYPTION_KEY.padEnd(32).slice(0, 32)),
      iv,
    );
    let decrypted = decipher.update(encrypted, 'hex', 'utf8');
    decrypted += decipher.final('utf8');
    return decrypted;
  }

  /**
   * Get all database connections for tenant
   */
  async findAll(tenantId: number, applicationId?: number): Promise<DatabaseConnection[]> {
    const where: any = { tenantId };
    if (applicationId) {
      where.applicationId = applicationId;
    }

    return this.dbConnectionRepository.find({
      where,
      order: { createdAt: 'DESC' },
      relations: ['application'],
    });
  }

  /**
   * Get database connection by ID
   */
  async findOne(id: number, tenantId: number): Promise<DatabaseConnection> {
    const connection = await this.dbConnectionRepository.findOne({
      where: { id, tenantId },
      relations: ['application'],
    });

    if (!connection) {
      throw new NotFoundException('Database connection not found');
    }

    return connection;
  }

  /**
   * Create new database connection
   */
  async create(
    createDto: CreateDatabaseConnectionDto,
    tenantId: number,
    userId: number,
  ): Promise<DatabaseConnection> {
    // Verify application if provided
    if (createDto.applicationId) {
      await this.applicationsService.findOne(createDto.applicationId, tenantId);
    }

    const connection = this.dbConnectionRepository.create({
      ...createDto,
      password: this.encrypt(createDto.password),
      connectionOptions: createDto.connectionOptions
        ? JSON.stringify(createDto.connectionOptions)
        : null,
      tenantId,
      createdById: userId,
      status: ConnectionStatus.INACTIVE,
    });

    const saved = await this.dbConnectionRepository.save(connection);

    // Test connection
    await this.testConnection(saved.id, tenantId);

    // Invalidate cache
    await this.cacheManager.del(`dbs:tenant:${tenantId}`);

    return this.findOne(saved.id, tenantId);
  }

  /**
   * Update database connection
   */
  async update(
    id: number,
    updateDto: UpdateDatabaseConnectionDto,
    tenantId: number,
  ): Promise<DatabaseConnection> {
    const connection = await this.findOne(id, tenantId);

    if (connection.isXuiOne) {
      throw new BadRequestException('Cannot modify XUI One connections');
    }

    if (updateDto.password) {
      updateDto.password = this.encrypt(updateDto.password);
    }

    if (updateDto.connectionOptions) {
      (updateDto as any).connectionOptions = JSON.stringify(updateDto.connectionOptions);
    }

    await this.dbConnectionRepository.update(id, updateDto);

    // Close existing connection if open
    await this.closeConnection(id);

    // Test new settings
    await this.testConnection(id, tenantId);

    // Invalidate cache
    await this.cacheManager.del(`dbs:tenant:${tenantId}`);
    await this.cacheManager.del(`db:${id}`);

    return this.findOne(id, tenantId);
  }

  /**
   * Delete database connection
   */
  async remove(id: number, tenantId: number): Promise<void> {
    const connection = await this.findOne(id, tenantId);

    if (connection.isXuiOne) {
      throw new BadRequestException('Cannot delete XUI One connections');
    }

    // Close connection if open
    await this.closeConnection(id);

    await this.dbConnectionRepository.remove(connection);

    // Invalidate cache
    await this.cacheManager.del(`dbs:tenant:${tenantId}`);
    await this.cacheManager.del(`db:${id}`);
  }

  /**
   * Test database connection
   */
  async testConnection(id: number, tenantId: number): Promise<{ success: boolean; message: string }> {
    const connection = await this.findOne(id, tenantId);

    // Update status to testing
    connection.status = ConnectionStatus.TESTING;
    await this.dbConnectionRepository.save(connection);

    try {
      let testResult: boolean;

      switch (connection.type) {
        case DatabaseType.MYSQL:
        case DatabaseType.MARIADB:
          testResult = await this.testMySQLConnection(connection);
          break;
        case DatabaseType.POSTGRESQL:
          testResult = await this.testPostgresConnection(connection);
          break;
        case DatabaseType.MONGODB:
          testResult = await this.testMongoConnection(connection);
          break;
        default:
          throw new BadRequestException(`Unsupported database type: ${connection.type}`);
      }

      if (testResult) {
        connection.status = ConnectionStatus.ACTIVE;
        connection.lastConnectedAt = new Date();
        connection.lastError = null;
        await this.dbConnectionRepository.save(connection);

        return { success: true, message: 'Connection successful' };
      } else {
        throw new Error('Connection test returned false');
      }
    } catch (error) {
      connection.status = ConnectionStatus.ERROR;
      connection.lastError = error.message;
      await this.dbConnectionRepository.save(connection);

      return { success: false, message: error.message };
    }
  }

  /**
   * Execute query
   */
  async executeQuery(
    id: number,
    queryDto: ExecuteQueryDto,
    tenantId: number,
    userId: number,
  ): Promise<QueryResult> {
    const connection = await this.findOne(id, tenantId);

    if (connection.status !== ConnectionStatus.ACTIVE) {
      throw new BadRequestException('Database connection is not active');
    }

    if (!connection.allowWrite && !this.isReadOnlyQuery(queryDto.query)) {
      throw new BadRequestException('Write operations not allowed on this connection');
    }

    const startTime = Date.now();

    try {
      let result: QueryResult;

      switch (connection.type) {
        case DatabaseType.MYSQL:
        case DatabaseType.MARIADB:
          result = await this.executeMySQLQuery(connection, queryDto);
          break;
        case DatabaseType.POSTGRESQL:
          result = await this.executePostgresQuery(connection, queryDto);
          break;
        default:
          throw new BadRequestException(`Query execution not supported for ${connection.type}`);
      }

      // Update statistics
      connection.queryCount++;
      connection.totalRowsReturned += result.rowCount;
      await this.dbConnectionRepository.save(connection);

      // Log query (in production, use proper audit logging)
      console.log(`[DB Query] User ${userId} executed: ${queryDto.query.substring(0, 100)}...`);

      return result;
    } catch (error) {
      throw new BadRequestException(`Query failed: ${error.message}`);
    }
  }

  /**
   * Get database schema
   */
  async getSchema(id: number, tenantId: number): Promise<any> {
    const connection = await this.findOne(id, tenantId);

    if (connection.status !== ConnectionStatus.ACTIVE) {
      throw new BadRequestException('Database connection is not active');
    }

    try {
      switch (connection.type) {
        case DatabaseType.MYSQL:
        case DatabaseType.MARIADB:
          return await this.getMySQLSchema(connection);
        case DatabaseType.POSTGRESQL:
          return await this.getPostgresSchema(connection);
        default:
          throw new BadRequestException(`Schema retrieval not supported for ${connection.type}`);
      }
    } catch (error) {
      throw new BadRequestException(`Failed to get schema: ${error.message}`);
    }
  }

  /**
   * Get database statistics
   */
  async getStats(id: number, tenantId: number): Promise<any> {
    const connection = await this.findOne(id, tenantId);

    return {
      id: connection.id,
      name: connection.name,
      status: connection.status,
      type: connection.type,
      queryCount: connection.queryCount,
      totalRowsReturned: connection.totalRowsReturned,
      lastConnectedAt: connection.lastConnectedAt,
      createdAt: connection.createdAt,
    };
  }

  /**
   * Close connection
   */
  async closeConnection(id: number): Promise<void> {
    const conn = this.connections.get(id);
    if (conn) {
      await conn.end();
      this.connections.delete(id);
    }
  }

  // Private helper methods

  private async testMySQLConnection(connection: DatabaseConnection): Promise<boolean> {
    const conn = await createConnection({
      host: connection.host,
      port: connection.port,
      user: connection.username,
      password: this.decrypt(connection.password),
      database: connection.database,
      connectTimeout: 10000,
    });

    await conn.ping();
    await conn.end();
    return true;
  }

  private async testPostgresConnection(connection: DatabaseConnection): Promise<boolean> {
    const client = new PgClient({
      host: connection.host,
      port: connection.port,
      user: connection.username,
      password: this.decrypt(connection.password),
      database: connection.database,
      connectionTimeoutMillis: 10000,
    });

    await client.connect();
    await client.query('SELECT 1');
    await client.end();
    return true;
  }

  private async testMongoConnection(connection: DatabaseConnection): Promise<boolean> {
    const uri = `mongodb://${connection.username}:${this.decrypt(connection.password)}@${connection.host}:${connection.port}/${connection.database}`;
    const client = new MongoClient(uri, { serverSelectionTimeoutMS: 10000 });
    await client.connect();
    await client.db().admin().ping();
    await client.close();
    return true;
  }

  private async executeMySQLQuery(
    connection: DatabaseConnection,
    queryDto: ExecuteQueryDto,
  ): Promise<QueryResult> {
    // Limit max rows
    const limitedQuery = this.addRowLimit(queryDto.query, connection.maxRowsPerQuery);

    const conn = await createConnection({
      host: connection.host,
      port: connection.port,
      user: connection.username,
      password: this.decrypt(connection.password),
      database: connection.database,
      connectTimeout: connection.queryTimeout * 1000,
    });

    try {
      const [rows] = await conn.execute(limitedQuery, queryDto.params || []);

      const columns = Array.isArray(rows) && rows.length > 0 ? Object.keys(rows[0]) : [];

      return {
        columns,
        rows: rows as any[],
        rowCount: Array.isArray(rows) ? rows.length : 0,
        executionTime: Date.now() - performance.now(),
        query: queryDto.query,
      };
    } finally {
      await conn.end();
    }
  }

  private async executePostgresQuery(
    connection: DatabaseConnection,
    queryDto: ExecuteQueryDto,
  ): Promise<QueryResult> {
    const limitedQuery = this.addRowLimit(queryDto.query, connection.maxRowsPerQuery);

    const client = new PgClient({
      host: connection.host,
      port: connection.port,
      user: connection.username,
      password: this.decrypt(connection.password),
      database: connection.database,
      connectionTimeoutMillis: connection.queryTimeout * 1000,
    });

    try {
      await client.connect();
      const result = await client.query(limitedQuery, queryDto.params || []);

      return {
        columns: result.fields.map((f) => f.name),
        rows: result.rows,
        rowCount: result.rowCount || 0,
        executionTime: Date.now() - performance.now(),
        query: queryDto.query,
      };
    } finally {
      await client.end();
    }
  }

  private async getMySQLSchema(connection: DatabaseConnection): Promise<any> {
    const conn = await createConnection({
      host: connection.host,
      port: connection.port,
      user: connection.username,
      password: this.decrypt(connection.password),
      database: connection.database,
    });

    try {
      // Get tables
      const [tables] = await conn.execute(
        'SELECT TABLE_NAME, TABLE_TYPE, ENGINE, TABLE_ROWS FROM information_schema.TABLES WHERE TABLE_SCHEMA = ?',
        [connection.database],
      );

      // Get columns for each table
      const schema = {};
      for (const table of tables as any[]) {
        const [columns] = await conn.execute(
          'SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_COMMENT FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = ? AND TABLE_NAME = ?',
          [connection.database, table.TABLE_NAME],
        );

        schema[table.TABLE_NAME] = {
          type: table.TABLE_TYPE,
          engine: table.ENGINE,
          estimatedRows: table.TABLE_ROWS,
          columns: columns,
        };
      }

      return schema;
    } finally {
      await conn.end();
    }
  }

  private async getPostgresSchema(connection: DatabaseConnection): Promise<any> {
    const client = new PgClient({
      host: connection.host,
      port: connection.port,
      user: connection.username,
      password: this.decrypt(connection.password),
      database: connection.database,
    });

    try {
      await client.connect();

      // Get tables
      const tablesResult = await client.query(
        `SELECT table_name, table_type 
         FROM information_schema.tables 
         WHERE table_schema = 'public'`,
      );

      const schema = {};
      for (const table of tablesResult.rows) {
        const columnsResult = await client.query(
          `SELECT column_name, data_type, is_nullable, column_default 
           FROM information_schema.columns 
           WHERE table_schema = 'public' AND table_name = $1`,
          [table.table_name],
        );

        schema[table.table_name] = {
          type: table.table_type,
          columns: columnsResult.rows,
        };
      }

      return schema;
    } finally {
      await client.end();
    }
  }

  private isReadOnlyQuery(query: string): boolean {
    const normalized = query.trim().toLowerCase();
    const writeOperations = ['insert', 'update', 'delete', 'drop', 'create', 'alter', 'truncate'];
    return !writeOperations.some((op) => normalized.startsWith(op));
  }

  private addRowLimit(query: string, maxRows: number): string {
    // Simple LIMIT appending - in production, use proper SQL parsing
    const normalized = query.trim().toLowerCase();
    if (normalized.startsWith('select') && !normalized.includes('limit')) {
      return `${query} LIMIT ${maxRows}`;
    }
    return query;
  }
}
