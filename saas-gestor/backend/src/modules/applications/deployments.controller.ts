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
import { ApiTags, ApiOperation, ApiResponse, ApiBearerAuth } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../auth/guards/jwt-auth.guard';
import { RolesGuard } from '../../auth/guards/roles.guard';
import { Roles } from '../../auth/decorators/roles.decorator';
import { UserRole } from '../users/entities/user.entity';
import { DeploymentsService, CreateDeploymentDto } from './deployments.service';
import { Deployment, DeploymentStatus } from './entities/deployment.entity';
import { RequestWithUser } from '../../auth/interfaces/request-with-user.interface';

@ApiTags('Deployments')
@Controller('applications/:applicationId/deployments')
@UseGuards(JwtAuthGuard, RolesGuard)
@ApiBearerAuth()
export class DeploymentsController {
  constructor(private readonly deploymentsService: DeploymentsService) {}

  @Get()
  @ApiOperation({ summary: 'Get all deployments for application' })
  @ApiResponse({ status: 200, description: 'List of deployments', type: [Deployment] })
  async findAll(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Request() req: RequestWithUser,
  ): Promise<Deployment[]> {
    return this.deploymentsService.findAll(applicationId, req.user.tenantId);
  }

  @Get('current')
  @ApiOperation({ summary: 'Get current deployment' })
  @ApiResponse({ status: 200, description: 'Current deployment', type: Deployment })
  async getCurrent(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Request() req: RequestWithUser,
  ): Promise<Deployment | null> {
    return this.deploymentsService.getCurrent(applicationId, req.user.tenantId);
  }

  @Get('stats')
  @ApiOperation({ summary: 'Get deployment statistics' })
  @ApiResponse({ status: 200, description: 'Deployment statistics' })
  async getStats(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Request() req: RequestWithUser,
  ): Promise<any> {
    return this.deploymentsService.getStats(applicationId, req.user.tenantId);
  }

  @Get(':id')
  @ApiOperation({ summary: 'Get deployment by ID' })
  @ApiResponse({ status: 200, description: 'Deployment details', type: Deployment })
  @ApiResponse({ status: 404, description: 'Deployment not found' })
  async findOne(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<Deployment> {
    return this.deploymentsService.findOne(id, applicationId, req.user.tenantId);
  }

  @Get(':id/logs')
  @ApiOperation({ summary: 'Get deployment logs' })
  @ApiResponse({ status: 200, description: 'Deployment logs' })
  @ApiResponse({ status: 404, description: 'Deployment not found' })
  async getLogs(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<{ logs: string }> {
    const logs = await this.deploymentsService.getLogs(id, applicationId, req.user.tenantId);
    return { logs };
  }

  @Post()
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Create new deployment' })
  @ApiResponse({ status: 201, description: 'Deployment queued', type: Deployment })
  async create(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Body() createDto: CreateDeploymentDto,
    @Request() req: RequestWithUser,
  ): Promise<Deployment> {
    return this.deploymentsService.create(applicationId, createDto, req.user.tenantId, req.user.id);
  }

  @Post(':id/cancel')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Cancel pending deployment' })
  @ApiResponse({ status: 200, description: 'Deployment cancelled', type: Deployment })
  @ApiResponse({ status: 400, description: 'Cannot cancel deployment' })
  async cancel(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<Deployment> {
    return this.deploymentsService.cancel(id, applicationId, req.user.tenantId);
  }

  @Post(':id/rollback')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Rollback deployment' })
  @ApiResponse({ status: 200, description: 'Rollback initiated', type: Deployment })
  @ApiResponse({ status: 400, description: 'Cannot rollback deployment' })
  async rollback(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<Deployment> {
    return this.deploymentsService.rollback(id, applicationId, req.user.tenantId, req.user.id);
  }

  @Post(':id/retry')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Retry failed deployment' })
  @ApiResponse({ status: 201, description: 'New deployment queued', type: Deployment })
  @ApiResponse({ status: 400, description: 'Cannot retry deployment' })
  async retry(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<Deployment> {
    return this.deploymentsService.retry(id, applicationId, req.user.tenantId, req.user.id);
  }
}
