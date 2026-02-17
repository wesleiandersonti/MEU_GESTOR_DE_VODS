import { Router } from 'express';
import { ExportController } from '../controllers/exportController';

export function createExportRoutes(controller: ExportController): Router {
  const router = Router();

  router.post('/export/m3u', controller.exportM3U);
  router.get('/export/m3u/latest', controller.getLatestM3U);
  router.get('/export/m3u/latest/download', controller.downloadLatestM3U);

  return router;
}
