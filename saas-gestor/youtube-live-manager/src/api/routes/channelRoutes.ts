import { Router } from 'express';
import { ChannelsController } from '../controllers/channelsController';

export function createChannelRoutes(controller: ChannelsController): Router {
  const router = Router();

  router.get('/channels/catalog', controller.getCatalog);
  router.post('/channels/catalog/import', controller.importCatalog);
  router.get('/channels', controller.list);
  router.post('/channels', controller.create);
  router.put('/channels/:id', controller.update);
  router.delete('/channels/:id', controller.softDelete);

  return router;
}
