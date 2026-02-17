import { Router } from 'express';
import { ScannerController } from '../controllers/scannerController';

export function createScannerRoutes(controller: ScannerController): Router {
  const router = Router();

  router.get('/scanner/status', controller.getStatus);
  router.post('/scanner/start', controller.start);
  router.post('/scanner/stop', controller.stop);
  router.post('/scanner/run-once', controller.runOnce);

  return router;
}
