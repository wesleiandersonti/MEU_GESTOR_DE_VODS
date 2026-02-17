import fs from 'node:fs/promises';
import { Request, Response } from 'express';
import { ExportRepository } from '../../db/repositories/exportRepository';
import { M3UExporter } from '../../core/exporter';

export class ExportController {
  constructor(
    private readonly exporter: M3UExporter,
    private readonly exportRepository: ExportRepository,
  ) {}

  exportM3U = async (_req: Request, res: Response): Promise<void> => {
    const result = await this.exporter.exportActiveChannels();

    res.status(201).json({
      ok: true,
      export_id: result.exportId,
      file_path: result.filePath,
      channels_count: result.channelsCount,
    });
  };

  getLatestM3U = async (_req: Request, res: Response): Promise<void> => {
    const latest = await this.exportRepository.getLatestM3U();

    if (!latest) {
      res.status(404).json({ ok: false, error: 'not_found', message: 'Nenhuma exportacao M3U encontrada.' });
      return;
    }

    const content = await fs.readFile(latest.file_path, 'utf-8');

    res.json({
      ok: true,
      export: {
        id: latest.id,
        created_at: latest.created_at,
        type: latest.type,
        file_path: latest.file_path,
        channels_count: latest.channels_count,
      },
      download_url: '/export/m3u/latest/download',
      content,
    });
  };

  downloadLatestM3U = async (_req: Request, res: Response): Promise<void> => {
    const latest = await this.exportRepository.getLatestM3U();

    if (!latest) {
      res.status(404).json({ ok: false, error: 'not_found', message: 'Nenhuma exportacao M3U encontrada.' });
      return;
    }

    res.download(latest.file_path, 'active_channels.m3u');
  };
}
