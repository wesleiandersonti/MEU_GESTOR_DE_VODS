import mysql, { Pool } from 'mysql2/promise';
import { env } from '../../config/env';
import { logger } from '../../utils/logger';

let pool: Pool | null = null;

async function ensureDatabaseExists(): Promise<void> {
  const connection = await mysql.createConnection({
    host: env.db.host,
    port: env.db.port,
    user: env.db.user,
    password: env.db.password,
    multipleStatements: true,
  });

  try {
    await connection.query(
      `CREATE DATABASE IF NOT EXISTS \`${env.db.name}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci`,
    );
  } finally {
    await connection.end();
  }
}

export async function initDb(): Promise<void> {
  await ensureDatabaseExists();

  pool = mysql.createPool({
    host: env.db.host,
    port: env.db.port,
    user: env.db.user,
    password: env.db.password,
    database: env.db.name,
    waitForConnections: true,
    connectionLimit: env.db.connectionLimit,
    queueLimit: 0,
    namedPlaceholders: false,
  });

  await pool.query('SELECT 1');
  logger.info('Database connected', { dbHost: env.db.host, dbName: env.db.name });
}

export function getDb(): Pool {
  if (!pool) {
    throw new Error('Database pool not initialized');
  }

  return pool;
}
