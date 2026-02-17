import rateLimit from 'express-rate-limit';
import { env } from '../../config/env';

export const apiRateLimit = rateLimit({
  windowMs: env.rateLimit.windowMs,
  max: env.rateLimit.max,
  standardHeaders: true,
  legacyHeaders: false,
  message: {
    ok: false,
    error: 'too_many_requests',
    message: 'Limite de requisicoes excedido. Tente novamente em instantes.',
  },
});
