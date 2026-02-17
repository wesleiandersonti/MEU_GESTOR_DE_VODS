import { Request, Response } from 'express';
import { ScannerService } from '../../core/scanner';

export class ScannerController {
  constructor(private readonly scanner: ScannerService) {}

  getStatus = async (_req: Request, res: Response): Promise<void> => {
    const status = await this.scanner.getStatus();
    res.json({ ok: true, scanner: status });
  };

  start = async (_req: Request, res: Response): Promise<void> => {
    await this.scanner.start();
    const status = await this.scanner.getStatus();
    res.json({ ok: true, scanner: status });
  };

  stop = (_req: Request, res: Response): void => {
    this.scanner.stop();
    res.json({ ok: true, message: 'Scanner parado.' });
  };

  runOnce = async (_req: Request, res: Response): Promise<void> => {
    const result = await this.scanner.runOnce();
    res.json({ ok: true, ...result });
  };
}
