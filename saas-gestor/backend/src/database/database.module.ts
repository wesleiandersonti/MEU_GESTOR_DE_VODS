import { Module } from '@nestjs/common';
import { ConfigModule, ConfigService } from '@nestjs/config';
import { TypeOrmModule } from '@nestjs/typeorm';
import { TypeOrmConfigService } from './typeorm-config.service';
import { DatabaseService } from './database.service';

@Module({
  imports: [
    ConfigModule,
    TypeOrmModule.forRootAsync({
      imports: [ConfigModule],
      useClass: TypeOrmConfigService,
    }),
    // Slave connection for read operations
    TypeOrmModule.forRootAsync({
      name: 'slave',
      imports: [ConfigModule],
      useFactory: (configService: ConfigService) => ({
        type: 'mysql',
        host: configService.get<string>('DB_SLAVE_HOST', 'localhost'),
        port: configService.get<number>('DB_SLAVE_PORT', 3307),
        username: configService.get<string>('DB_USER', 'root'),
        password: configService.get<string>('DB_PASSWORD', ''),
        database: configService.get<string>('DB_NAME', 'saas_gestor'),
        entities: [__dirname + '/../**/*.entity{.ts,.js}'],
        synchronize: false,
        logging: false,
      }),
      inject: [ConfigService],
    }),
  ],
  providers: [TypeOrmConfigService, DatabaseService],
  exports: [TypeOrmConfigService, DatabaseService],
})
export class DatabaseModule {}
