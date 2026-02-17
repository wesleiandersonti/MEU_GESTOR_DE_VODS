import { Router } from 'express';
import { HealthController } from '../controllers/healthController';

export function createHealthRoutes(controller: HealthController): Router {
  const router = Router();
  router.get('/', controller.getHealth);
  return router;
}
