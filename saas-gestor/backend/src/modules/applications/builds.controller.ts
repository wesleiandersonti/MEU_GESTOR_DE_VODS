import {
  Controller,
  Get,
  Post,
  Param,
  Query,
  Body,
  UseGuards,
  Request,
  ParseIntPipe,
  HttpCode,
  HttpStatus,
  Sse,
  MessageEvent,
} from '@nestjs/common';
import { ApiTags, ApiOperation, ApiResponse, ApiBearerAuth, ApiQuery } from '@nestjs/swagger';
import { Observable } from 'rxjs';
import { JwtAuthGuard } from '../../auth/guards/jwt-auth.guard';
import { RolesGuard } from '../../auth/guards/roles.guard';
import { Roles } from '../../auth/decorators/roles.decorator';
import { UserRole } from '../users/entities/user.entity';
import { BuildsService, CreateBuildDto } from './builds.service';
import { Build, BuildStatus } from './entities/build.entity';
import { RequestWithUser } from '../../auth/interfaces/request-with-user.interface';

@ApiTags('Builds')
@Controller('applications/:applicationId/builds')
@UseGuards(JwtAuthGuard, RolesGuard)
@ApiBearerAuth()
export class BuildsController {
  constructor(private readonly buildsService: BuildsService) {}

  @Get()
  @ApiOperation({ summary: 'Get all builds for application' })
  @ApiQuery({ name: 'status', required: false, enum: BuildStatus })
  @ApiResponse({ status: 200, description: 'List of builds', type: [Build] })
  async findAll(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Request() req: RequestWithUser,
    @Query('status') status?: BuildStatus,
  ): Promise<Build[]> {
    const builds = await this.buildsService.findAll(applicationId, req.user.tenantId);
    
    if (status) {
      return builds.filter(build => build.status === status);
    }
    
    return builds;
  }

  @Get('queue-status')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Get build queue status' })
  @ApiResponse({ status: 200, description: 'Queue statistics' })
  async getQueueStatus(): Promise<any> {
    return this.buildsService.getQueueStatus();
  }

  @Get(':id')
  @ApiOperation({ summary: 'Get build by ID' })
  @ApiResponse({ status: 200, description: 'Build details', type: Build })
  @ApiResponse({ status: 404, description: 'Build not found' })
  async findOne(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<Build> {
    return this.buildsService.findOne(id, applicationId, req.user.tenantId);
  }

  @Get(':id/logs')
  @ApiOperation({ summary: 'Get build logs' })
  @ApiResponse({ status: 200, description: 'Build logs' })
  @ApiResponse({ status: 404, description: 'Build not found' })
  async getLogs(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<{ logs: string }> {
    const logs = await this.buildsService.getLogs(id, applicationId, req.user.tenantId);
    return { logs };
  }

  @Sse(':id/logs/stream')
  @ApiOperation({ summary: 'Stream build logs in real-time' })
  @ApiResponse({ status: 200, description: 'SSE stream of build logs' })
  streamLogs(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Observable<MessageEvent> {
    return new Observable<MessageEvent>((subscriber) => {
      let lastLogLength = 0;
      let checkInterval: NodeJS.Timeout;

      const checkForNewLogs = async () => {
        try {
          const build = await this.buildsService.findOne(id, applicationId, req.user.tenantId);
          const currentLogs = build.logs || '';

          if (currentLogs.length > lastLogLength) {
            const newLogs = currentLogs.substring(lastLogLength);
            lastLogLength = currentLogs.length;
            
            subscriber.next({
              data: {
                logs: newLogs,
                status: build.status,
                completedAt: build.completedAt,
              },
            } as MessageEvent);
          }

          // Stop streaming if build is completed, failed, or cancelled
          if (
            build.status === BuildStatus.COMPLETED ||
            build.status === BuildStatus.FAILED ||
            build.status === BuildStatus.CANCELLED
          ) {
            subscriber.next({
              data: {
                logs: '',
                status: build.status,
                completedAt: build.completedAt,
                done: true,
              },
            } as MessageEvent);
            subscriber.complete();
            clearInterval(checkInterval);
          }
        } catch (error) {
          subscriber.error(error);
          clearInterval(checkInterval);
        }
      };

      // Check for new logs every 2 seconds
      checkInterval = setInterval(checkForNewLogs, 2000);

      // Initial check
      checkForNewLogs();

      // Cleanup on unsubscribe
      return () => {
        clearInterval(checkInterval);
      };
    });
  }

  @Post()
  @Roles(UserRole.ADMIN, UserRole.DEVOPS, UserRole.DEVELOPER)
  @ApiOperation({ summary: 'Trigger new build' })
  @ApiResponse({ status: 201, description: 'Build queued', type: Build })
  async create(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Body() createDto: CreateBuildDto,
    @Request() req: RequestWithUser,
  ): Promise<Build> {
    return this.buildsService.create(applicationId, createDto, req.user.tenantId, req.user.id);
  }

  @Post(':id/cancel')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Cancel running or queued build' })
  @ApiResponse({ status: 200, description: 'Build cancelled', type: Build })
  @ApiResponse({ status: 400, description: 'Cannot cancel build' })
  async cancel(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<Build> {
    return this.buildsService.cancel(id, applicationId, req.user.tenantId);
  }

  @Post(':id/retry')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS, UserRole.DEVELOPER)
  @ApiOperation({ summary: 'Retry failed or cancelled build' })
  @ApiResponse({ status: 201, description: 'New build queued', type: Build })
  @ApiResponse({ status: 400, description: 'Cannot retry build' })
  async retry(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<Build> {
    return this.buildsService.retry(id, applicationId, req.user.tenantId, req.user.id);
  }
}
