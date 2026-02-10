import {
  Controller,
  Get,
  Post,
  Put,
  Delete,
  Param,
  Body,
  Query,
  UseGuards,
  Request,
  ParseIntPipe,
  HttpCode,
  HttpStatus,
} from '@nestjs/common';
import {
  ApiTags,
  ApiOperation,
  ApiResponse,
  ApiBearerAuth,
  ApiQuery,
} from '@nestjs/swagger';
import { JwtAuthGuard } from '../../auth/guards/jwt-auth.guard';
import { RolesGuard } from '../../auth/guards/roles.guard';
import { Roles } from '../../auth/decorators/roles.decorator';
import { UserRole } from '../users/entities/user.entity';
import {
  DatabaseService,
  CreateDatabaseConnectionDto,
  UpdateDatabaseConnectionDto,
  ExecuteQueryDto,
} from './database.service';
import { DatabaseConnection, ConnectionStatus } from './entities/database-connection.entity';
import { RequestWithUser } from '../../auth/interfaces/request-with-user.interface';

@ApiTags('Database Connections')
@Controller('database-connections')
@UseGuards(JwtAuthGuard, RolesGuard)
@ApiBearerAuth()
export class DatabaseController {
  constructor(private readonly databaseService: DatabaseService) {}

  @Get()
  @ApiOperation({ summary: 'Get all database connections for tenant' })
  @ApiQuery({ name: 'applicationId', required: false, type: Number })
  @ApiResponse({ status: 200, description: 'List of connections', type: [DatabaseConnection] })
  async findAll(
    @Request() req: RequestWithUser,
    @Query('applicationId') applicationId?: number,
  ): Promise<DatabaseConnection[]> {
    return this.databaseService.findAll(req.user.tenantId, applicationId);
  }

  @Get(':id')
  @ApiOperation({ summary: 'Get database connection by ID' })
  @ApiResponse({ status: 200, description: 'Connection details', type: DatabaseConnection })
  @ApiResponse({ status: 404, description: 'Connection not found' })
  async findOne(
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<DatabaseConnection> {
    return this.databaseService.findOne(id, req.user.tenantId);
  }

  @Get(':id/schema')
  @ApiOperation({ summary: 'Get database schema' })
  @ApiResponse({ status: 200, description: 'Database schema' })
  @ApiResponse({ status: 404, description: 'Connection not found' })
  async getSchema(
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<any> {
    return this.databaseService.getSchema(id, req.user.tenantId);
  }

  @Get(':id/stats')
  @ApiOperation({ summary: 'Get database connection statistics' })
  @ApiResponse({ status: 200, description: 'Connection statistics' })
  async getStats(
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<any> {
    return this.databaseService.getStats(id, req.user.tenantId);
  }

  @Post()
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Create new database connection' })
  @ApiResponse({ status: 201, description: 'Connection created', type: DatabaseConnection })
  async create(
    @Body() createDto: CreateDatabaseConnectionDto,
    @Request() req: RequestWithUser,
  ): Promise<DatabaseConnection> {
    return this.databaseService.create(createDto, req.user.tenantId, req.user.id);
  }

  @Put(':id')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Update database connection' })
  @ApiResponse({ status: 200, description: 'Connection updated', type: DatabaseConnection })
  @ApiResponse({ status: 404, description: 'Connection not found' })
  async update(
    @Param('id', ParseIntPipe) id: number,
    @Body() updateDto: UpdateDatabaseConnectionDto,
    @Request() req: RequestWithUser,
  ): Promise<DatabaseConnection> {
    return this.databaseService.update(id, updateDto, req.user.tenantId);
  }

  @Delete(':id')
  @Roles(UserRole.ADMIN)
  @HttpCode(HttpStatus.NO_CONTENT)
  @ApiOperation({ summary: 'Delete database connection' })
  @ApiResponse({ status: 204, description: 'Connection deleted' })
  async remove(
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<void> {
    return this.databaseService.remove(id, req.user.tenantId);
  }

  @Post(':id/test')
  @ApiOperation({ summary: 'Test database connection' })
  @ApiResponse({ status: 200, description: 'Connection test result' })
  async testConnection(
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<{ success: boolean; message: string }> {
    return this.databaseService.testConnection(id, req.user.tenantId);
  }

  @Post(':id/query')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS, UserRole.DEVELOPER)
  @ApiOperation({ summary: 'Execute SQL query' })
  @ApiResponse({ status: 200, description: 'Query results' })
  @ApiResponse({ status: 400, description: 'Invalid query or connection error' })
  async executeQuery(
    @Param('id', ParseIntPipe) id: number,
    @Body() queryDto: ExecuteQueryDto,
    @Request() req: RequestWithUser,
  ): Promise<any> {
    return this.databaseService.executeQuery(id, queryDto, req.user.tenantId, req.user.id);
  }
}
