import { RowDataPacket, ResultSetHeader } from 'mysql2';
import { getDb } from '../connection';
import { CheckResult } from './types';

interface HistoryRow extends RowDataPacket {
  id: number;
  channel_id: number;
  checked_at: string;
  result: CheckResult;
  details: string;
  duration_ms: number;
}

export class HistoryRepository {
  async add(input: {
    channelId: number;
    result: CheckResult;
    details: unknown;
    durationMs: number;
  }): Promise<number> {
    const db = getDb();
    const [result] = await db.execute<ResultSetHeader>(
      `INSERT INTO yt_checks_history (channel_id, checked_at, result, details, duration_ms)
       VALUES (?, CURRENT_TIMESTAMP, ?, ?, ?)`,
      [input.channelId, input.result, JSON.stringify(input.details), input.durationMs],
    );

    return result.insertId;
  }

  async getByChannelId(
    channelId: number,
    options: { from?: string; to?: string; limit: number },
  ): Promise<HistoryRow[]> {
    const where: string[] = ['channel_id = ?'];
    const params: Array<string | number> = [channelId];

    if (options.from) {
      where.push('checked_at >= ?');
      params.push(options.from);
    }

    if (options.to) {
      where.push('checked_at <= ?');
      params.push(options.to);
    }

    const db = getDb();
    const [rows] = await db.query<HistoryRow[]>(
      `SELECT * FROM yt_checks_history
       WHERE ${where.join(' AND ')}
       ORDER BY checked_at DESC
       LIMIT ?`,
      [...params, options.limit],
    );

    return rows;
  }
}
