import { Injectable } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { TypeOrmModuleOptions, TypeOrmOptionsFactory } from '@nestjs/typeorm';

@Injectable()
export class TypeOrmConfigService implements TypeOrmOptionsFactory {
  constructor(private configService: ConfigService) {}

  createTypeOrmOptions(): TypeOrmModuleOptions {
    const nodeEnv = this.configService.get<string>('NODE_ENV', 'development');
    const isProduction = nodeEnv === 'production';

    return {
      type: 'mysql',
      name: 'default', // Master connection for writes
      host: this.configService.get<string>('DB_MASTER_HOST', 'localhost'),
      port: this.configService.get<number>('DB_MASTER_PORT', 3306),
      username: this.configService.get<string>('DB_USER', 'root'),
      password: this.configService.get<string>('DB_PASSWORD', ''),
      database: this.configService.get<string>('DB_NAME', 'saas_gestor'),
      entities: [__dirname + '/../**/*.entity{.ts,.js}'],
      synchronize: !isProduction, // Only sync in development
      logging: !isProduction,
      logger: 'advanced-console',
      migrations: [__dirname + '/migrations/**/*{.ts,.js}'],
      migrationsRun: true,
      extra: {
        connectionLimit: 20,
        queueLimit: 0,
        waitForConnections: true,
      },
    };
  }

  createSlaveOptions(): TypeOrmModuleOptions {
    const nodeEnv = this.configService.get<string>('NODE_ENV', 'development');

    return {
      type: 'mysql',
      name: 'slave', // Slave connection for reads
      host: this.configService.get<string>('DB_SLAVE_HOST', 'localhost'),
      port: this.configService.get<number>('DB_SLAVE_PORT', 3307),
      username: this.configService.get<string>('DB_USER', 'root'),
      password: this.configService.get<string>('DB_PASSWORD', ''),
      database: this.configService.get<string>('DB_NAME', 'saas_gestor'),
      entities: [__dirname + '/../**/*.entity{.ts,.js}'],
      synchronize: false, // Never sync on slave
      logging: nodeEnv === 'development',
      extra: {
        connectionLimit: 30, // More connections for reads
        queueLimit: 0,
        waitForConnections: true,
      },
    };
  }
}
