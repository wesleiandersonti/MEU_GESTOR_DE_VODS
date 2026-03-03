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

  const serialized = `${JSON.stringify(payload)}\n`;

  if (level === 'ERROR') {
    process.stderr.write(serialized);
    return;
  }

  process.stdout.write(serialized);
}

export const logger = {
  info: (message: string, meta: LogMeta = {}) => log('INFO', message, meta),
  warn: (message: string, meta: LogMeta = {}) => log('WARN', message, meta),
  error: (message: string, meta: LogMeta = {}) => log('ERROR', message, meta),
};
