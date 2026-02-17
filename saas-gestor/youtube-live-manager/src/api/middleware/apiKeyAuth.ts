import { NextFunction, Request, Response } from 'express';
import { env } from '../../config/env';

export function apiKeyAuth(req: Request, res: Response, next: NextFunction): void {
  if (!env.apiKey) {
    next();
    return;
  }

  const provided = req.header('X-API-Key');
  if (!provided || provided !== env.apiKey) {
    res.status(401).json({
      ok: false,
      error: 'unauthorized',
      message: 'API key invalida ou ausente.',
    });
    return;
  }

  next();
}
