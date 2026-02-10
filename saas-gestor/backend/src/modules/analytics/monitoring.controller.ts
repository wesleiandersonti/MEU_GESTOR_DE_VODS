import {
  Controller,
  Get,
  Sse,
  Query,
  Param,
  UseGuards,
  Request,
  ParseIntPipe,
  MessageEvent,
} from '@nestjs/common';
import {
  ApiTags,
  ApiOperation,
  ApiResponse,
  ApiBearerAuth,
  ApiQuery,
} from '@nestjs/swagger';
import { Observable, interval } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import { JwtAuthGuard } from '../../auth/guards/jwt-auth.guard';
import { RolesGuard } from '../../auth/guards/roles.guard';
import { MonitoringService } from './monitoring.service';
import { RequestWithUser } from '../../auth/interfaces/request-with-user.interface';

@ApiTags('Monitoring')
@Controller('monitoring')
@UseGuards(JwtAuthGuard, RolesGuard)
@ApiBearerAuth()
export class MonitoringController {
  constructor(private readonly monitoringService: MonitoringService) {}

  @Get('status')
  @ApiOperation({ summary: 'Get system health status' })
  @ApiResponse({ status: 200, description: 'System status' })
  async getSystemStatus(@Request() req: RequestWithUser): Promise<any> {
    return this.monitoringService.getSystemStatus();
  }

  @Get('queues')
  @ApiOperation({ summary: 'Get queue status (builds & deployments)' })
  @ApiResponse({ status: 200, description: 'Queue status' })
  async getQueueStatus(@Request() req: RequestWithUser): Promise<any> {
    return this.monitoringService.getQueueStatus();
  }

  @Get('tenant-stats')
  @ApiOperation({ summary: 'Get tenant-specific real-time stats' })
  @ApiResponse({ status: 200, description: 'Tenant stats' })
  async getTenantStats(@Request() req: RequestWithUser): Promise<any> {
    return this.monitoringService.getTenantStats(req.user.tenantId);
  }

  @Get('health-history/:component')
  @ApiOperation({ summary: 'Get health check history' })
  @ApiQuery({ name: 'hours', required: false, type: Number })
  @ApiResponse({ status: 200, description: 'Health history' })
  async getHealthHistory(
    @Param('component') component: string,
    @Query('hours') hours?: number,
  ): Promise<any> {
    return this.monitoringService.getHealthHistory(component, hours || 24);
  }

  @Sse('stream')
  @ApiOperation({ summary: 'Stream real-time system status (SSE)' })
  streamSystemStatus(@Request() req: RequestWithUser): Observable<MessageEvent> {
    return interval(5000).pipe(
      switchMap(async () => {
        const [systemStatus, queueStatus] = await Promise.all([
          this.monitoringService.getSystemStatus(),
          this.monitoringService.getQueueStatus(),
        ]);

        return {
          data: {
            timestamp: new Date().toISOString(),
            system: systemStatus,
            queues: queueStatus,
          },
        } as MessageEvent;
      }),
    );
  }

  @Sse('tenant-stream')
  @ApiOperation({ summary: 'Stream real-time tenant stats (SSE)' })
  streamTenantStats(@Request() req: RequestWithUser): Observable<MessageEvent> {
    return interval(3000).pipe(
      switchMap(async () => {
        const stats = await this.monitoringService.getTenantStats(req.user.tenantId);

        return {
          data: {
            timestamp: new Date().toISOString(),
            tenantId: req.user.tenantId,
            stats,
          },
        } as MessageEvent;
      }),
    );
  }

  @Sse('queues/stream')
  @ApiOperation({ summary: 'Stream real-time queue updates (SSE)' })
  streamQueueUpdates(@Request() req: RequestWithUser): Observable<MessageEvent> {
    return interval(2000).pipe(
      switchMap(async () => {
        const queueStatus = await this.monitoringService.getQueueStatus();

        return {
          data: {
            timestamp: new Date().toISOString(),
            queues: queueStatus,
          },
        } as MessageEvent;
      }),
    );
  }
}
