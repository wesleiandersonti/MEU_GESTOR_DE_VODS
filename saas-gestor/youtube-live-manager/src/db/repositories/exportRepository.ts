import { RowDataPacket, ResultSetHeader } from 'mysql2';
import { getDb } from '../connection';

interface ExportRow extends RowDataPacket {
  id: number;
  created_at: string;
  type: string;
  file_path: string;
  channels_count: number;
}

export class ExportRepository {
  async createM3U(filePath: string, channelsCount: number): Promise<number> {
    const db = getDb();
    const [result] = await db.execute<ResultSetHeader>(
      `INSERT INTO yt_exports (created_at, type, file_path, channels_count)
       VALUES (CURRENT_TIMESTAMP, 'M3U', ?, ?)`,
      [filePath, channelsCount],
    );

    return result.insertId;
  }

  async getLatestM3U(): Promise<ExportRow | null> {
    const db = getDb();
    const [rows] = await db.query<ExportRow[]>(
      `SELECT * FROM yt_exports
       WHERE type = 'M3U'
       ORDER BY created_at DESC, id DESC
       LIMIT 1`,
    );

    return rows.length ? rows[0] : null;
  }
}
