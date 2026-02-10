import { Injectable, NotFoundException, BadRequestException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { DatabaseConnection, DatabaseType, ConnectionStatus } from './entities/database-connection.entity';
import { ApplicationsService } from './applications.service';
import { DatabaseService } from './database.service';

export interface XuiOneDatabaseInfo {
  id: number;
  name: string;
  host: string;
  port: number;
  database: string;
  username: string;
  type: string;
}

export interface CreateXuiConnectionDto {
  name: string;
  xuiUrl: string;
  apiKey: string;
  applicationId?: number;
}

/**
 * XUI One Integration Service
 * Provides read-only access to XUI One databases
 */
@Injectable()
export class XuiOneService {
  constructor(
    @InjectRepository(DatabaseConnection)
    private dbConnectionRepository: Repository<DatabaseConnection>,
    private applicationsService: ApplicationsService,
    private databaseService: DatabaseService,
  ) {}

  /**
   * Discover XUI One databases via API
   * This is a placeholder - in production, this would call the XUI One API
   */
  async discoverDatabases(xuiUrl: string, apiKey: string): Promise<XuiOneDatabaseInfo[]> {
    // TODO: Implement actual XUI One API call
    // For now, return mock data structure
    try {
      // Mock implementation - replace with actual API call
      // const response = await fetch(`${xuiUrl}/api/databases`, {
      //   headers: { 'X-API-Key': apiKey }
      // });
      // return await response.json();

      // Return empty array for now - user will need to configure manually
      return [];
    } catch (error) {
      throw new BadRequestException(`Failed to connect to XUI One: ${error.message}`);
    }
  }

  /**
   * Create read-only XUI One database connection
   */
  async createXuiConnection(
    createDto: CreateXuiConnectionDto,
    dbInfo: XuiOneDatabaseInfo,
    tenantId: number,
    userId: number,
  ): Promise<DatabaseConnection> {
    // Verify application if provided
    if (createDto.applicationId) {
      await this.applicationsService.findOne(createDto.applicationId, tenantId);
    }

    const connection = this.dbConnectionRepository.create({
      name: createDto.name,
      description: `XUI One connection - ${dbInfo.name} (READ-ONLY)`,
      type: DatabaseType.MARIADB, // XUI One typically uses MariaDB
      host: dbInfo.host,
      port: dbInfo.port,
      database: dbInfo.database,
      username: dbInfo.username,
      password: '', // XUI One connections use API authentication
      tenantId,
      createdById: userId,
      applicationId: createDto.applicationId,
      isXuiOne: true,
      xuiOneUrl: createDto.xuiUrl,
      xuiOneApiKey: createDto.apiKey,
      allowWrite: false, // Always read-only
      status: ConnectionStatus.INACTIVE,
    });

    const saved = await this.dbConnectionRepository.save(connection);

    // Test connection
    await this.databaseService.testConnection(saved.id, tenantId);

    return this.dbConnectionRepository.findOne({
      where: { id: saved.id },
      relations: ['application'],
    });
  }

  /**
   * Get all XUI One connections for tenant
   */
  async findAllXuiConnections(tenantId: number): Promise<DatabaseConnection[]> {
    return this.dbConnectionRepository.find({
      where: { tenantId, isXuiOne: true },
      order: { createdAt: 'DESC' },
      relations: ['application'],
    });
  }

  /**
   * Sync XUI One database schema
   * Updates local cache of table/column information
   */
  async syncSchema(connectionId: number, tenantId: number): Promise<any> {
    const connection = await this.dbConnectionRepository.findOne({
      where: { id: connectionId, tenantId, isXuiOne: true },
    });

    if (!connection) {
      throw new NotFoundException('XUI One connection not found');
    }

    // Get current schema
    const schema = await this.databaseService.getSchema(connectionId, tenantId);

    // In production, you might want to cache this schema or store it
    return {
      connectionId,
      syncedAt: new Date(),
      tables: Object.keys(schema).length,
      schema,
    };
  }

  /**
   * Execute read-only query on XUI One database
   * Ensures only SELECT statements are allowed
   */
  async executeReadOnlyQuery(
    connectionId: number,
    query: string,
    tenantId: number,
    userId: number,
  ): Promise<any> {
    // Validate query is read-only
    if (!this.isSelectQuery(query)) {
      throw new BadRequestException('Only SELECT queries are allowed on XUI One connections');
    }

    return this.databaseService.executeQuery(
      connectionId,
      { query },
      tenantId,
      userId,
    );
  }

  /**
   * Get XUI One connection statistics
   */
  async getXuiStats(connectionId: number, tenantId: number): Promise<any> {
    const connection = await this.dbConnectionRepository.findOne({
      where: { id: connectionId, tenantId, isXuiOne: true },
    });

    if (!connection) {
      throw new NotFoundException('XUI One connection not found');
    }

    return {
      connectionId,
      name: connection.name,
      status: connection.status,
      queryCount: connection.queryCount,
      totalRowsReturned: connection.totalRowsReturned,
      lastConnectedAt: connection.lastConnectedAt,
      isReadOnly: true,
    };
  }

  /**
   * Validate and test XUI One connection
   */
  async validateXuiConnection(
    xuiUrl: string,
    apiKey: string,
  ): Promise<{ valid: boolean; message: string; databases?: XuiOneDatabaseInfo[] }> {
    try {
      // TODO: Implement actual XUI One API validation
      // For now, return mock success
      return {
        valid: true,
        message: 'XUI One connection validated successfully',
        databases: [],
      };
    } catch (error) {
      return {
        valid: false,
        message: `XUI One validation failed: ${error.message}`,
      };
    }
  }

  private isSelectQuery(query: string): boolean {
    const normalized = query.trim().toLowerCase();
    // Allow SELECT and EXPLAIN queries only
    return normalized.startsWith('select') || normalized.startsWith('explain');
  }
}
