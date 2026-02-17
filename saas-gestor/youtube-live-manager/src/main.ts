import cors from 'cors';
import express from 'express';
import helmet from 'helmet';
import { createApiRoutes } from './api/routes';
import { createHealthRoutes } from './api/routes/healthRoutes';
import { createStreamRoutes } from './api/routes/streamRoutes';
import { ChannelsController } from './api/controllers/channelsController';
import { ExportController } from './api/controllers/exportController';
import { HealthController } from './api/controllers/healthController';
import { ScannerController } from './api/controllers/scannerController';
import { StatusController } from './api/controllers/statusController';
import { StreamController } from './api/controllers/streamController';
import { apiKeyAuth } from './api/middleware/apiKeyAuth';
import { errorHandler } from './api/middleware/errorHandler';
import { apiRateLimit } from './api/middleware/rateLimit';
import { requestLogger } from './api/middleware/requestLogger';
import { streamAccess } from './api/middleware/streamAccess';
import { env } from './config/env';
import { ChannelChecker } from './core/channelChecker';
import { seedDefaultYouTubeChannels } from './core/channelCatalog';
import { CircuitBreaker } from './core/circuitBreaker';
import { M3UExporter } from './core/exporter';
import { LiveDetector } from './core/liveDetector';
import { ScannerService } from './core/scanner';
import { StreamResolver } from './core/streamResolver';
import { StreamValidator } from './core/validator';
import { initDb } from './db/connection';
import { runInitMigration } from './db/migrations/runMigration';
import { ChannelRepository } from './db/repositories/channelRepository';
import { ExportRepository } from './db/repositories/exportRepository';
import { HistoryRepository } from './db/repositories/historyRepository';
import { StatusRepository } from './db/repositories/statusRepository';
import { logger } from './utils/logger';

function parseCorsOrigin(value: string): string[] | boolean {
  if (value === '*') {
    return true;
  }

  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
}

async function bootstrap(): Promise<void> {
  await initDb();

  if (env.autoMigrate) {
    await runInitMigration();
  }

  const channelRepository = new ChannelRepository();
  const statusRepository = new StatusRepository();
  const historyRepository = new HistoryRepository();
  const exportRepository = new ExportRepository();

  if (env.seedDefaultChannels) {
    const seeded = await seedDefaultYouTubeChannels(channelRepository);
    logger.info('Default YouTube catalog imported', seeded);
  }

  const breaker = new CircuitBreaker(env.breaker.threshold, env.breaker.blockedCooldownSec * 1000);
  const detector = new LiveDetector();
  const resolver = new StreamResolver();
  const validator = new StreamValidator();
  const checker = new ChannelChecker(detector, resolver, validator, statusRepository, historyRepository, breaker);
  const scanner = new ScannerService(channelRepository, statusRepository, checker, breaker);
  const exporter = new M3UExporter(channelRepository, exportRepository);

  const healthController = new HealthController(scanner);
  const channelsController = new ChannelsController(channelRepository);
  const statusController = new StatusController(channelRepository, statusRepository, historyRepository);
  const exportController = new ExportController(exporter, exportRepository);
  const scannerController = new ScannerController(scanner);
  const streamController = new StreamController(channelRepository, statusRepository, checker);

  const app = express();
  app.set('trust proxy', true);

  app.use(helmet());
  app.use(
    cors({
      origin: parseCorsOrigin(env.corsOrigin),
      credentials: true,
    }),
  );
  app.use(express.json({ limit: '1mb' }));
  app.use(requestLogger);
  app.use(apiRateLimit);

  app.use('/health', createHealthRoutes(healthController));
  app.use('/stream', streamAccess, createStreamRoutes(streamController));

  app.use(apiKeyAuth);
  app.use(
    '/',
    createApiRoutes({
      channels: channelsController,
      status: statusController,
      exportController,
      scanner: scannerController,
    }),
  );

  app.use(errorHandler);

  if (env.scan.autoStart) {
    await scanner.start();
  }

  app.listen(env.port, () => {
    logger.info('Service started', {
      port: env.port,
      env: env.nodeEnv,
      autoStartScanner: env.scan.autoStart,
    });
  });
}

bootstrap().catch((error) => {
  logger.error('Startup failed', {
    error: error instanceof Error ? error.message : String(error),
  });
  process.exit(1);
});
