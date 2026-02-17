import { Request, Response } from 'express';
import { env } from '../../config/env';
import { ChannelChecker } from '../../core/channelChecker';
import { ChannelRepository } from '../../db/repositories/channelRepository';
import { StatusRepository } from '../../db/repositories/statusRepository';

export class StreamController {
  constructor(
    private readonly channelRepository: ChannelRepository,
    private readonly statusRepository: StatusRepository,
    private readonly checker: ChannelChecker,
  ) {}

  redirect = async (req: Request, res: Response): Promise<void> => {
    const id = Number.parseInt(req.params.id, 10);
    if (!Number.isFinite(id) || id <= 0) {
      res.status(400).json({ ok: false, error: 'invalid_id' });
      return;
    }

    const channel = await this.channelRepository.getById(id);
    if (!channel || !channel.enabled) {
      res.status(404).json({ ok: false, error: 'not_found' });
      return;
    }

    const currentStatus = await this.statusRepository.getByChannelId(id);
    const now = Date.now();

    if (currentStatus?.streamUrl && currentStatus.isOnline) {
      const ageMs = now - new Date(currentStatus.lastCheckedAt).getTime();
      if (ageMs <= env.stream.maxAgeSec * 1000) {
        res.redirect(302, currentStatus.streamUrl);
        return;
      }
    }

    const outcome = await this.checker.run(channel, 1);
    if (outcome.isOnline && outcome.streamUrl) {
      res.redirect(302, outcome.streamUrl);
      return;
    }

    res.status(503).json({
      ok: false,
      error: 'stream_unavailable',
      result: outcome.result,
      message: outcome.errorMessage ?? 'Canal sem stream disponivel neste momento.',
    });
  };
}
