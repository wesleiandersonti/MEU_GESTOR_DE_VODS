import { NextFunction, Request, Response } from 'express';
import { logger } from '../../utils/logger';

export function errorHandler(error: unknown, req: Request, res: Response, _next: NextFunction): void {
  const message = error instanceof Error ? error.message : String(error);

  logger.error('Unhandled API error', {
    method: req.method,
    path: req.originalUrl,
    error: message,
  });

  res.status(500).json({
    ok: false,
    error: 'internal_error',
    message,
  });
}
