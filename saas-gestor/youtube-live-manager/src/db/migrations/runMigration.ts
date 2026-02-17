import fs from 'node:fs/promises';
import path from 'node:path';
import mysql from 'mysql2/promise';
import { env } from '../../config/env';
import { logger } from '../../utils/logger';

export async function runInitMigration(): Promise<void> {
  const distPath = path.resolve(__dirname, '001_init.sql');
  const sourcePath = path.resolve(process.cwd(), 'src/db/migrations/001_init.sql');
  const filePath = await fs
    .access(distPath)
    .then(() => distPath)
    .catch(() => sourcePath);
  const template = await fs.readFile(filePath, 'utf-8');
  const sql = template.replace(/{{DB_NAME}}/g, `\`${env.db.name}\``);

  const connection = await mysql.createConnection({
    host: env.db.host,
    port: env.db.port,
    user: env.db.user,
    password: env.db.password,
    multipleStatements: true,
  });

  try {
    await connection.query(sql);
    logger.info('Migration executed', { migration: '001_init.sql', dbName: env.db.name });
  } finally {
    await connection.end();
  }
}

if (require.main === module) {
  runInitMigration().catch((error) => {
    logger.error('Migration failed', { error: error instanceof Error ? error.message : String(error) });
    process.exit(1);
  });
}
