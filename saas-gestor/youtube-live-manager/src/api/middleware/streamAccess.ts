import { NextFunction, Request, Response } from 'express';
import { env } from '../../config/env';
import { normalizeIp } from '../../utils/ip';

function fromWhitelist(ip: string): boolean {
  if (!env.stream.ipWhitelist.length) {
    return false;
  }

  return env.stream.ipWhitelist.includes(ip);
}

export function streamAccess(req: Request, res: Response, next: NextFunction): void {
  const provided = req.header('X-API-Key');
  const normalizedIp = normalizeIp(req.ip);

  if (env.apiKey && provided === env.apiKey) {
    next();
    return;
  }

  if (fromWhitelist(normalizedIp)) {
    next();
    return;
  }

  res.status(401).json({
    ok: false,
    error: 'unauthorized',
    message: 'Acesso ao stream requer API key ou IP permitido.',
  });
}
