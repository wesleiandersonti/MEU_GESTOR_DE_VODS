import { Request, Response } from 'express';
import { z } from 'zod';
import { ChannelRepository } from '../../db/repositories/channelRepository';
import { HistoryRepository } from '../../db/repositories/historyRepository';
import { StatusRepository } from '../../db/repositories/statusRepository';

const historyQuerySchema = z.object({
  from: z.string().optional(),
  to: z.string().optional(),
  limit: z.string().optional(),
});

export class StatusController {
  constructor(
    private readonly channelRepository: ChannelRepository,
    private readonly statusRepository: StatusRepository,
    private readonly historyRepository: HistoryRepository,
  ) {}

  getStatus = async (req: Request, res: Response): Promise<void> => {
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

    const status = await this.statusRepository.getByChannelId(id);

    res.json({
      ok: true,
      channel,
      status,
    });
  };

  getHistory = async (req: Request, res: Response): Promise<void> => {
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

    const parsed = historyQuerySchema.safeParse(req.query);
    if (!parsed.success) {
      res.status(400).json({ ok: false, error: 'invalid_query', details: parsed.error.flatten() });
      return;
    }

    const limit = Math.min(500, Math.max(1, Number(parsed.data.limit ?? '100')));
    const history = await this.historyRepository.getByChannelId(id, {
      from: parsed.data.from,
      to: parsed.data.to,
      limit,
    });

    res.json({
      ok: true,
      channel,
      count: history.length,
      items: history,
    });
  };
}
