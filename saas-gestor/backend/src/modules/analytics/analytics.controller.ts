import {
  Controller,
  Get,
  Post,
  Query,
  Param,
  UseGuards,
  Request,
  ParseIntPipe,
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
import { AnalyticsService } from './analytics.service';
import { MetricType } from './entities/analytics-metric.entity';
import { RequestWithUser } from '../../auth/interfaces/request-with-user.interface';

@ApiTags('Analytics')
@Controller('analytics')
@UseGuards(JwtAuthGuard, RolesGuard)
@ApiBearerAuth()
export class AnalyticsController {
  constructor(private readonly analyticsService: AnalyticsService) {}

  @Get('dashboard')
  @ApiOperation({ summary: 'Get dashboard statistics' })
  @ApiQuery({ name: 'days', required: false, type: Number, description: 'Number of days for statistics' })
  @ApiResponse({ status: 200, description: 'Dashboard statistics' })
  async getDashboardStats(
    @Request() req: RequestWithUser,
    @Query('days') days?: number,
  ): Promise<any> {
    return this.analyticsService.getDashboardStats(req.user.tenantId, days || 30);
  }

  @Get('timeseries')
  @ApiOperation({ summary: 'Get time series data for charts' })
  @ApiQuery({ name: 'metricType', required: true, enum: MetricType })
  @ApiQuery({ name: 'days', required: false, type: Number })
  @ApiQuery({ name: 'applicationId', required: false, type: Number })
  @ApiResponse({ status: 200, description: 'Time series data' })
  async getTimeSeriesData(
    @Request() req: RequestWithUser,
    @Query('metricType') metricType: MetricType,
    @Query('days') days?: number,
    @Query('applicationId') applicationId?: number,
  ): Promise<any> {
    return this.analyticsService.getTimeSeriesData(
      req.user.tenantId,
      metricType,
      days || 7,
      applicationId,
    );
  }

  @Get('applications/:applicationId')
  @ApiOperation({ summary: 'Get analytics for specific application' })
  @ApiQuery({ name: 'days', required: false, type: Number })
  @ApiResponse({ status: 200, description: 'Application analytics' })
  async getApplicationAnalytics(
    @Param('applicationId', ParseIntPipe) applicationId: number,
    @Request() req: RequestWithUser,
    @Query('days') days?: number,
  ): Promise<any> {
    return this.analyticsService.getApplicationAnalytics(
      applicationId,
      req.user.tenantId,
      days || 30,
    );
  }

  @Get('activity')
  @ApiOperation({ summary: 'Get recent activity feed' })
  @ApiQuery({ name: 'limit', required: false, type: Number })
  @ApiQuery({ name: 'applicationId', required: false, type: Number })
  @ApiResponse({ status: 200, description: 'Recent activity' })
  async getRecentActivity(
    @Request() req: RequestWithUser,
    @Query('limit') limit?: number,
    @Query('applicationId') applicationId?: number,
  ): Promise<any[]> {
    return this.analyticsService.getRecentActivity(
      req.user.tenantId,
      limit || 50,
      applicationId,
    );
  }

  @Post('aggregate')
  @ApiOperation({ summary: 'Manually trigger daily aggregation (admin only)' })
  @ApiResponse({ status: 200, description: 'Aggregation completed' })
  async triggerAggregation(
    @Request() req: RequestWithUser,
    @Query('date') dateString?: string,
  ): Promise<{ message: string }> {
    const date = dateString ? new Date(dateString) : new Date();
    await this.analyticsService.aggregateDailySummary(date);
    return { message: `Aggregation completed for ${date.toISOString().split('T')[0]}` };
  }
}
