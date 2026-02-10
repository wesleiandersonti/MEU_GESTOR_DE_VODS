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
  Res,
} from '@nestjs/common';
import {
  ApiTags,
  ApiOperation,
  ApiResponse,
  ApiBearerAuth,
  ApiQuery,
} from '@nestjs/swagger';
import { Response } from 'express';
import { JwtAuthGuard } from '../../auth/guards/jwt-auth.guard';
import { RolesGuard } from '../../auth/guards/roles.guard';
import { Roles } from '../../auth/decorators/roles.decorator';
import { UserRole } from '../users/entities/user.entity';
import {
  EnvironmentVariablesService,
  CreateEnvironmentVariableDto,
  UpdateEnvironmentVariableDto,
} from './environment-variables.service';
import {
  EnvironmentVariable,
  EnvironmentVariableScope,
} from './entities/environment-variable.entity';
import { RequestWithUser } from '../../auth/interfaces/request-with-user.interface';

@ApiTags('Environment Variables')
@Controller('applications/:applicationId/environment-variables')
@UseGuards(JwtAuthGuard, RolesGuard)
@ApiBearerAuth()
export class EnvironmentVariablesController {
  constructor(
    private readonly envVarsService: EnvironmentVariablesService,
  ) {}

  @Get()
  @ApiOperation({ summary: 'Get all environment variables for application' })
  @ApiQuery({
    name: 'revealSecrets',
    required: false,
    type: Boolean,
    description: 'Reveal secret values (admin only)',
  })
  @ApiResponse({
    status: 200,
    description: 'List of environment variables',
    type: [EnvironmentVariable],
  })
  async findAll(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Request() req: RequestWithUser,
    @Query('revealSecrets') revealSecrets?: string,
  ): Promise<any[]> {
    const shouldReveal =
      revealSecrets === 'true' &&
      (req.user.role === UserRole.ADMIN || req.user.role === UserRole.DEVOPS);

    return this.envVarsService.findAll(
      applicationId,
      req.user.tenantId,
      shouldReveal,
    );
  }

  @Get(':id')
  @ApiOperation({ summary: 'Get environment variable by ID' })
  @ApiQuery({
    name: 'revealSecret',
    required: false,
    type: Boolean,
  })
  @ApiResponse({
    status: 200,
    description: 'Environment variable details',
    type: EnvironmentVariable,
  })
  @ApiResponse({ status: 404, description: 'Variable not found' })
  async findOne(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
    @Query('revealSecret') revealSecret?: string,
  ): Promise<any> {
    const shouldReveal =
      revealSecret === 'true' &&
      (req.user.role === UserRole.ADMIN || req.user.role === UserRole.DEVOPS);

    return this.envVarsService.findOne(
      id,
      applicationId,
      req.user.tenantId,
      shouldReveal,
    );
  }

  @Post()
  @Roles(UserRole.ADMIN, UserRole.DEVOPS, UserRole.DEVELOPER)
  @ApiOperation({ summary: 'Create new environment variable' })
  @ApiResponse({
    status: 201,
    description: 'Environment variable created',
    type: EnvironmentVariable,
  })
  async create(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Body() createDto: CreateEnvironmentVariableDto,
    @Request() req: RequestWithUser,
  ): Promise<any> {
    return this.envVarsService.create(
      applicationId,
      createDto,
      req.user.tenantId,
      req.user.id,
    );
  }

  @Post('bulk')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Create multiple environment variables' })
  @ApiResponse({
    status: 201,
    description: 'Environment variables created',
    type: [EnvironmentVariable],
  })
  async createMany(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Body() variables: CreateEnvironmentVariableDto[],
    @Request() req: RequestWithUser,
  ): Promise<any[]> {
    return this.envVarsService.createMany(
      applicationId,
      variables,
      req.user.tenantId,
      req.user.id,
    );
  }

  @Put(':id')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS, UserRole.DEVELOPER)
  @ApiOperation({ summary: 'Update environment variable' })
  @ApiResponse({
    status: 200,
    description: 'Environment variable updated',
    type: EnvironmentVariable,
  })
  async update(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Param('id', ParseIntPipe) id: number,
    @Body() updateDto: UpdateEnvironmentVariableDto,
    @Request() req: RequestWithUser,
  ): Promise<any> {
    return this.envVarsService.update(
      id,
      applicationId,
      updateDto,
      req.user.tenantId,
    );
  }

  @Delete(':id')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @HttpCode(HttpStatus.NO_CONTENT)
  @ApiOperation({ summary: 'Delete environment variable' })
  @ApiResponse({ status: 204, description: 'Environment variable deleted' })
  async remove(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<void> {
    return this.envVarsService.remove(id, applicationId, req.user.tenantId);
  }

  @Delete()
  @Roles(UserRole.ADMIN)
  @HttpCode(HttpStatus.NO_CONTENT)
  @ApiOperation({ summary: 'Delete all environment variables' })
  @ApiResponse({ status: 204, description: 'All environment variables deleted' })
  async removeAll(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Request() req: RequestWithUser,
  ): Promise<void> {
    return this.envVarsService.removeAll(applicationId, req.user.tenantId);
  }

  @Post('import')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Import from .env file' })
  @ApiResponse({ status: 200, description: 'Import results' })
  async importFromEnvFile(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Body()
    body: {
      envFileContent: string;
      markAsSecret?: boolean;
    },
    @Request() req: RequestWithUser,
  ): Promise<{ imported: number; failed: number; errors: string[] }> {
    return this.envVarsService.importFromEnvFile(
      applicationId,
      body.envFileContent,
      req.user.tenantId,
      req.user.id,
      body.markAsSecret,
    );
  }

  @Get('export')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Export to .env file' })
  @ApiQuery({
    name: 'includeSecrets',
    required: false,
    type: Boolean,
  })
  @ApiResponse({ status: 200, description: '.env file content' })
  async exportToEnvFile(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Request() req: RequestWithUser,
    @Query('includeSecrets') includeSecrets?: string,
    @Res() res?: Response,
  ): Promise<any> {
    const shouldIncludeSecrets =
      includeSecrets === 'true' && req.user.role === UserRole.ADMIN;

    const content = await this.envVarsService.exportToEnvFile(
      applicationId,
      req.user.tenantId,
      shouldIncludeSecrets,
    );

    if (res) {
      res.setHeader('Content-Type', 'text/plain');
      res.setHeader(
        'Content-Disposition',
        `attachment; filename=".env"`,
      );
      res.send(content);
    } else {
      return { content };
    }
  }

  @Post('clone/:sourceApplicationId')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Clone variables from another application' })
  @ApiResponse({ status: 200, description: 'Clone results' })
  async cloneVariables(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Param('sourceApplicationId', ParseIntPipe)
    sourceApplicationId: number,
    @Request() req: RequestWithUser,
  ): Promise<{ cloned: number; failed: number }> {
    return this.envVarsService.cloneVariables(
      sourceApplicationId,
      applicationId,
      req.user.tenantId,
      req.user.id,
    );
  }

  @Post('bulk-update')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Bulk update environment variables' })
  @ApiResponse({ status: 200, description: 'Bulk update results' })
  async bulkUpdate(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Body()
    body: {
      variables: Array<{
        id?: number;
        key: string;
        value: string;
        isSecret?: boolean;
      }>;
    },
    @Request() req: RequestWithUser,
  ): Promise<{ created: number; updated: number; deleted: number }> {
    return this.envVarsService.bulkUpdate(
      applicationId,
      body.variables,
      req.user.tenantId,
      req.user.id,
    );
  }
}
