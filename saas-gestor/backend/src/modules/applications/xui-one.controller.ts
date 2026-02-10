import {
  Controller,
  Get,
  Post,
  Param,
  Body,
  UseGuards,
  Request,
  ParseIntPipe,
} from '@nestjs/common';
import {
  ApiTags,
  ApiOperation,
  ApiResponse,
  ApiBearerAuth,
} from '@nestjs/swagger';
import { JwtAuthGuard } from '../../auth/guards/jwt-auth.guard';
import { RolesGuard } from '../../auth/guards/roles.guard';
import { Roles } from '../../auth/decorators/roles.decorator';
import { UserRole } from '../users/entities/user.entity';
import {
  XuiOneService,
  CreateXuiConnectionDto,
  XuiOneDatabaseInfo,
} from './xui-one.service';
import { DatabaseConnection } from './entities/database-connection.entity';
import { RequestWithUser } from '../../auth/interfaces/request-with-user.interface';

@ApiTags('XUI One Integration')
@Controller('xui-one')
@UseGuards(JwtAuthGuard, RolesGuard)
@ApiBearerAuth()
export class XuiOneController {
  constructor(private readonly xuiOneService: XuiOneService) {}

  @Post('validate')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Validate XUI One connection' })
  @ApiResponse({ status: 200, description: 'Validation result' })
  async validateConnection(
    @Body() validateDto: { xuiUrl: string; apiKey: string },
  ): Promise<{ valid: boolean; message: string; databases?: XuiOneDatabaseInfo[] }> {
    return this.xuiOneService.validateXuiConnection(validateDto.xuiUrl, validateDto.apiKey);
  }

  @Post('discover')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Discover XUI One databases' })
  @ApiResponse({ status: 200, description: 'List of available databases' })
  async discoverDatabases(
    @Body() discoverDto: { xuiUrl: string; apiKey: string },
  ): Promise<XuiOneDatabaseInfo[]> {
    return this.xuiOneService.discoverDatabases(discoverDto.xuiUrl, discoverDto.apiKey);
  }

  @Post('connections')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Create XUI One database connection (READ-ONLY)' })
  @ApiResponse({ status: 201, description: 'XUI One connection created', type: DatabaseConnection })
  async createXuiConnection(
    @Body() createDto: CreateXuiConnectionDto & { dbInfo: XuiOneDatabaseInfo },
    @Request() req: RequestWithUser,
  ): Promise<DatabaseConnection> {
    return this.xuiOneService.createXuiConnection(
      createDto,
      createDto.dbInfo,
      req.user.tenantId,
      req.user.id,
    );
  }

  @Get('connections')
  @ApiOperation({ summary: 'Get all XUI One connections for tenant' })
  @ApiResponse({ status: 200, description: 'List of XUI One connections', type: [DatabaseConnection] })
  async findAllXuiConnections(
    @Request() req: RequestWithUser,
  ): Promise<DatabaseConnection[]> {
    return this.xuiOneService.findAllXuiConnections(req.user.tenantId);
  }

  @Post('connections/:id/sync')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Sync XUI One database schema' })
  @ApiResponse({ status: 200, description: 'Schema synced successfully' })
  async syncSchema(
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<any> {
    return this.xuiOneService.syncSchema(id, req.user.tenantId);
  }

  @Get('connections/:id/stats')
  @ApiOperation({ summary: 'Get XUI One connection statistics' })
  @ApiResponse({ status: 200, description: 'XUI One statistics' })
  async getXuiStats(
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<any> {
    return this.xuiOneService.getXuiStats(id, req.user.tenantId);
  }

  @Post('connections/:id/query')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS, UserRole.DEVELOPER)
  @ApiOperation({ summary: 'Execute read-only query on XUI One (SELECT only)' })
  @ApiResponse({ status: 200, description: 'Query results' })
  @ApiResponse({ status: 400, description: 'Invalid query - only SELECT allowed' })
  async executeReadOnlyQuery(
    @Param('id', ParseIntPipe) id: number,
    @Body() queryDto: { query: string },
    @Request() req: RequestWithUser,
  ): Promise<any> {
    return this.xuiOneService.executeReadOnlyQuery(
      id,
      queryDto.query,
      req.user.tenantId,
      req.user.id,
    );
  }
}
