import { Request, Response } from 'express';
import { env } from '../../config/env';
import { getDb } from '../../db/connection';
import { ScannerService } from '../../core/scanner';

export class HealthController {
  constructor(private readonly scanner: ScannerService) {}

  getHealth = async (_req: Request, res: Response): Promise<void> => {
    let dbOk = false;

    try {
      const db = getDb();
      await db.query('SELECT 1');
      dbOk = true;
    } catch {
      dbOk = false;
    }

    const scannerStatus = await this.scanner.getStatus();

    res.json({
      ok: true,
      service: 'youtube-live-manager',
      version: env.version,
      db: {
        connected: dbOk,
        host: env.db.host,
        name: env.db.name,
      },
      scanner: {
        running: scannerStatus.running,
        jobsTotal: scannerStatus.jobsTotal,
      },
      now: new Date().toISOString(),
    });
  };
}
