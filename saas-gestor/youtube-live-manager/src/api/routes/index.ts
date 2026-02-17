import { Router } from 'express';
import { ChannelsController } from '../controllers/channelsController';
import { ExportController } from '../controllers/exportController';
import { ScannerController } from '../controllers/scannerController';
import { StatusController } from '../controllers/statusController';
import { createChannelRoutes } from './channelRoutes';
import { createExportRoutes } from './exportRoutes';
import { createScannerRoutes } from './scannerRoutes';
import { createStatusRoutes } from './statusRoutes';

export function createApiRoutes(controllers: {
  channels: ChannelsController;
  status: StatusController;
  exportController: ExportController;
  scanner: ScannerController;
}): Router {
  const router = Router();

  router.use(createChannelRoutes(controllers.channels));
  router.use(createStatusRoutes(controllers.status));
  router.use(createExportRoutes(controllers.exportController));
  router.use(createScannerRoutes(controllers.scanner));

  return router;
}
