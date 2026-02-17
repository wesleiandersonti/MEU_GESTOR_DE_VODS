import { Router } from 'express';
import { StreamController } from '../controllers/streamController';

export function createStreamRoutes(controller: StreamController): Router {
  const router = Router();

  router.get('/:id', controller.redirect);

  return router;
}
