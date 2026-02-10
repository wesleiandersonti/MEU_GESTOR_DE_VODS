import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { AnalyticsService } from './analytics.service';
import { AnalyticsController } from './analytics.controller';
import { MonitoringService } from './monitoring.service';
import { MonitoringController } from './monitoring.controller';
import {
  AnalyticsMetric,
  AnalyticsDailySummary,
  SystemHealth,
} from './entities/analytics-metric.entity';

@Module({
  imports: [
    TypeOrmModule.forFeature([AnalyticsMetric, AnalyticsDailySummary, SystemHealth]),
  ],
  controllers: [AnalyticsController, MonitoringController],
  providers: [AnalyticsService, MonitoringService],
  exports: [AnalyticsService, MonitoringService],
})
export class AnalyticsModule {}
