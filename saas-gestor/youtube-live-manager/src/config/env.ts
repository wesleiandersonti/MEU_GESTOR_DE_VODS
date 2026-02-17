import dotenv from 'dotenv';

dotenv.config();

function toInt(value: string | undefined, fallback: number): number {
  if (!value) {
    return fallback;
  }

  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function toBool(value: string | undefined, fallback: boolean): boolean {
  if (value === undefined) {
    return fallback;
  }

  return ['1', 'true', 'yes', 'on'].includes(value.toLowerCase());
}

function toList(value: string | undefined): string[] {
  if (!value) {
    return [];
  }

  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
}

export const env = {
  nodeEnv: process.env.NODE_ENV ?? 'development',
  port: toInt(process.env.PORT, 8085),
  db: {
    host: process.env.DB_HOST ?? '127.0.0.1',
    port: toInt(process.env.DB_PORT, 3306),
    user: process.env.DB_USER ?? 'root',
    password: process.env.DB_PASS ?? '',
    name: process.env.DB_NAME ?? 'xui_tools',
    connectionLimit: toInt(process.env.DB_CONNECTION_LIMIT, 10),
  },
  apiKey: process.env.API_KEY ?? '',
  scan: {
    intervalSec: toInt(process.env.SCAN_INTERVAL_SEC, 60),
    concurrency: toInt(process.env.SCAN_CONCURRENCY, 4),
    maxRetries: toInt(process.env.SCAN_MAX_RETRIES, 3),
    retryBaseMs: toInt(process.env.SCAN_RETRY_BASE_MS, 1200),
    jobRefreshSec: toInt(process.env.SCAN_JOB_REFRESH_SEC, 30),
    autoStart: toBool(process.env.SCANNER_AUTOSTART, true),
  },
  ytdlp: {
    path: process.env.YTDLP_PATH ?? 'yt-dlp',
    timeoutSec: toInt(process.env.YTDLP_TIMEOUT_SEC, 25),
  },
  validator: {
    timeoutSec: toInt(process.env.VALIDATOR_TIMEOUT_SEC, 12),
  },
  breaker: {
    threshold: toInt(process.env.BREAKER_THRESHOLD, 3),
    blockedCooldownSec: toInt(process.env.BLOCKED_COOLDOWN_SEC, 300),
  },
  stream: {
    maxAgeSec: toInt(process.env.STREAM_URL_MAX_AGE_SEC, 120),
    ipWhitelist: toList(process.env.STREAM_IP_WHITELIST),
  },
  outputDir: process.env.OUTPUT_DIR ?? './output',
  publicBaseUrl: process.env.PUBLIC_BASE_URL ?? 'http://127.0.0.1:8085',
  exportUseProxy: toBool(process.env.EXPORT_USE_PROXY, true),
  corsOrigin: process.env.CORS_ORIGIN ?? '*',
  rateLimit: {
    windowMs: toInt(process.env.RATE_LIMIT_WINDOW_MS, 60000),
    max: toInt(process.env.RATE_LIMIT_MAX, 120),
  },
  autoMigrate: toBool(process.env.AUTO_MIGRATE, true),
  version: '1.0.0',
};
