import { Request, Response } from 'express';
import { z } from 'zod';
import { defaultYouTubeLiveCatalog, seedDefaultYouTubeChannels } from '../../core/channelCatalog';
import { ChannelRepository } from '../../db/repositories/channelRepository';
import { buildLiveUrl, isYouTubeChannelUrl, normalizeYouTubeChannelUrl } from '../../utils/channel';

const listQuerySchema = z.object({
  category: z.string().optional(),
  enabled: z.enum(['true', 'false']).optional(),
  is_online: z.enum(['true', 'false']).optional(),
  is_live: z.enum(['true', 'false']).optional(),
  page: z.string().optional(),
  limit: z.string().optional(),
});

const createSchema = z.object({
  name: z.string().min(1).max(255),
  category: z.string().min(1).max(120),
  channel_url: z.string().url(),
  enabled: z.boolean().optional().default(true),
});

const updateSchema = z.object({
  name: z.string().min(1).max(255).optional(),
  category: z.string().min(1).max(120).optional(),
  channel_url: z.string().url().optional(),
  enabled: z.boolean().optional(),
});

export class ChannelsController {
  constructor(private readonly channelRepository: ChannelRepository) {}

  getCatalog = async (_req: Request, res: Response): Promise<void> => {
    res.json({
      ok: true,
      source: defaultYouTubeLiveCatalog.source,
      categories: defaultYouTubeLiveCatalog.categories,
      bouquets: defaultYouTubeLiveCatalog.bouquets,
      users: defaultYouTubeLiveCatalog.users,
      channels_count: defaultYouTubeLiveCatalog.channels.length,
    });
  };

  importCatalog = async (_req: Request, res: Response): Promise<void> => {
    const result = await seedDefaultYouTubeChannels(this.channelRepository);

    res.json({
      ok: true,
      message: 'Catalogo YouTube Live importado com sucesso.',
      imported: result.imported,
      categories: result.categories,
      channels: result.channels,
    });
  };

  list = async (req: Request, res: Response): Promise<void> => {
    const parsed = listQuerySchema.safeParse(req.query);
    if (!parsed.success) {
      res.status(400).json({ ok: false, error: 'invalid_query', details: parsed.error.flatten() });
      return;
    }

    const page = Math.max(1, Number(parsed.data.page ?? '1'));
    const limit = Math.min(100, Math.max(1, Number(parsed.data.limit ?? '20')));

    const result = await this.channelRepository.list({
      category: parsed.data.category,
      enabled: parsed.data.enabled ? parsed.data.enabled === 'true' : undefined,
      isOnline: parsed.data.is_online ? parsed.data.is_online === 'true' : undefined,
      isLive: parsed.data.is_live ? parsed.data.is_live === 'true' : undefined,
      page,
      limit,
    });

    res.json({
      ok: true,
      page,
      limit,
      total: result.total,
      items: result.items,
    });
  };

  create = async (req: Request, res: Response): Promise<void> => {
    const parsed = createSchema.safeParse(req.body);
    if (!parsed.success) {
      res.status(400).json({ ok: false, error: 'invalid_body', details: parsed.error.flatten() });
      return;
    }

    if (!isYouTubeChannelUrl(parsed.data.channel_url)) {
      res.status(400).json({ ok: false, error: 'invalid_channel_url', message: 'URL nao parece canal do YouTube.' });
      return;
    }

    const normalizedChannelUrl = normalizeYouTubeChannelUrl(parsed.data.channel_url);
    const liveUrl = buildLiveUrl(normalizedChannelUrl);

    const created = await this.channelRepository.create({
      name: parsed.data.name,
      category: parsed.data.category,
      channelUrl: normalizedChannelUrl,
      liveUrl,
      enabled: parsed.data.enabled,
    });

    res.status(201).json({
      ok: true,
      item: created,
    });
  };

  update = async (req: Request, res: Response): Promise<void> => {
    const id = Number.parseInt(req.params.id, 10);
    if (!Number.isFinite(id) || id <= 0) {
      res.status(400).json({ ok: false, error: 'invalid_id' });
      return;
    }

    const parsed = updateSchema.safeParse(req.body);
    if (!parsed.success) {
      res.status(400).json({ ok: false, error: 'invalid_body', details: parsed.error.flatten() });
      return;
    }

    let channelUrl = parsed.data.channel_url;
    let liveUrl: string | undefined;

    if (channelUrl) {
      if (!isYouTubeChannelUrl(channelUrl)) {
        res.status(400).json({ ok: false, error: 'invalid_channel_url', message: 'URL nao parece canal do YouTube.' });
        return;
      }

      channelUrl = normalizeYouTubeChannelUrl(channelUrl);
      liveUrl = buildLiveUrl(channelUrl);
    }

    const updated = await this.channelRepository.update(id, {
      name: parsed.data.name,
      category: parsed.data.category,
      channelUrl,
      liveUrl,
      enabled: parsed.data.enabled,
    });

    if (!updated) {
      res.status(404).json({ ok: false, error: 'not_found' });
      return;
    }

    res.json({ ok: true, item: updated });
  };

  softDelete = async (req: Request, res: Response): Promise<void> => {
    const id = Number.parseInt(req.params.id, 10);
    if (!Number.isFinite(id) || id <= 0) {
      res.status(400).json({ ok: false, error: 'invalid_id' });
      return;
    }

    const channel = await this.channelRepository.getById(id);
    if (!channel) {
      res.status(404).json({ ok: false, error: 'not_found' });
      return;
    }

    await this.channelRepository.softDelete(id);
    const updated = await this.channelRepository.getById(id);

    res.json({
      ok: true,
      message: 'Canal desativado com sucesso.',
      item: updated,
    });
  };
}
