import { Router } from 'express';
import { StatusController } from '../controllers/statusController';

export function createStatusRoutes(controller: StatusController): Router {
  const router = Router();

  router.get('/channels/:id/status', controller.getStatus);
  router.get('/channels/:id/history', controller.getHistory);

  return router;
}
