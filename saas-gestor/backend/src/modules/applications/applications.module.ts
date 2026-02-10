import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { BullModule } from '@nestjs/bullmq';
import { ApplicationsService } from './applications.service';
import { ApplicationsController } from './applications.controller';
import { BuildsService } from './builds.service';
import { BuildsController } from './builds.controller';
import { DeploymentsService } from './deployments.service';
import { DeploymentsController } from './deployments.controller';
import { DatabaseService } from './database.service';
import { DatabaseController } from './database.controller';
import { XuiOneService } from './xui-one.service';
import { XuiOneController } from './xui-one.controller';
import { EnvironmentVariablesService } from './environment-variables.service';
import { EnvironmentVariablesController } from './environment-variables.controller';
import { Application } from './entities/application.entity';
import { Build } from './entities/build.entity';
import { Deployment } from './entities/deployment.entity';
import { DatabaseConnection } from './entities/database-connection.entity';
import { EnvironmentVariable } from './entities/environment-variable.entity';
import { BuildProcessor } from './processors/build.processor';
import { DeploymentProcessor } from './processors/deployment.processor';
import { AnalyticsModule } from '../analytics/analytics.module';

@Module({
  imports: [
    TypeOrmModule.forFeature([Application, Build, Deployment, DatabaseConnection, EnvironmentVariable]),
    BullModule.registerQueue({
      name: 'builds',
    }),
    BullModule.registerQueue({
      name: 'deployments',
    }),
    AnalyticsModule,
  ],
  controllers: [ApplicationsController, BuildsController, DeploymentsController, DatabaseController, XuiOneController, EnvironmentVariablesController],
  providers: [ApplicationsService, BuildsService, DeploymentsService, DatabaseService, XuiOneService, EnvironmentVariablesService, BuildProcessor, DeploymentProcessor],
  exports: [ApplicationsService, BuildsService, DeploymentsService, DatabaseService, XuiOneService, EnvironmentVariablesService],
})
export class ApplicationsModule {}
