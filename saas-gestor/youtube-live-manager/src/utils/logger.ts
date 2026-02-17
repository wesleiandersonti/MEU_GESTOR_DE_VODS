type LogLevel = 'INFO' | 'WARN' | 'ERROR';

interface LogMeta {
  [key: string]: unknown;
}

function log(level: LogLevel, message: string, meta: LogMeta = {}): void {
  const payload = {
    ts: new Date().toISOString(),
    level,
    message,
    ...meta,
  };

  console.log(JSON.stringify(payload));
}

export const logger = {
  info: (message: string, meta: LogMeta = {}) => log('INFO', message, meta),
  warn: (message: string, meta: LogMeta = {}) => log('WARN', message, meta),
  error: (message: string, meta: LogMeta = {}) => log('ERROR', message, meta),
};
