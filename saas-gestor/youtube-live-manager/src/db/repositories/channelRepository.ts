import { RowDataPacket, ResultSetHeader } from 'mysql2';
import { getDb } from '../connection';
import { AggregatedChannelRecord, ChannelRecord, ChannelStatusRecord, StreamFormat } from './types';

interface ChannelRow extends RowDataPacket {
  id: number;
  name: string;
  category: string;
  channel_url: string;
  live_url: string;
  enabled: number;
  created_at: string;
  updated_at: string;
}

interface ChannelWithStatusRow extends ChannelRow {
  is_live: number | null;
  is_online: number | null;
  live_video_id: string | null;
  stream_url: string | null;
  format: StreamFormat;
  last_http_code: number | null;
  last_checked_at: string | null;
  error_code: string | null;
  error_message: string | null;
}

export interface ChannelFilters {
  category?: string;
  enabled?: boolean;
  isOnline?: boolean;
  isLive?: boolean;
  page: number;
  limit: number;
}

export class ChannelRepository {
  private mapChannel(row: ChannelRow): ChannelRecord {
    return {
      id: row.id,
      name: row.name,
      category: row.category,
      channelUrl: row.channel_url,
      liveUrl: row.live_url,
      enabled: row.enabled === 1,
      createdAt: row.created_at,
      updatedAt: row.updated_at,
    };
  }

  private mapStatus(row: ChannelWithStatusRow): ChannelStatusRecord | null {
    if (row.is_live === null && row.is_online === null && !row.last_checked_at) {
      return null;
    }

    return {
      channelId: row.id,
      isLive: row.is_live === 1,
      isOnline: row.is_online === 1,
      liveVideoId: row.live_video_id,
      streamUrl: row.stream_url,
      format: row.format,
      lastHttpCode: row.last_http_code,
      lastCheckedAt: row.last_checked_at ?? new Date(0).toISOString(),
      errorCode: (row.error_code as ChannelStatusRecord['errorCode']) ?? null,
      errorMessage: row.error_message,
    };
  }

  async getById(id: number): Promise<ChannelRecord | null> {
    const db = getDb();
    const [rows] = await db.query<ChannelRow[]>('SELECT * FROM yt_channels WHERE id = ? LIMIT 1', [id]);

    if (!rows.length) {
      return null;
    }

    return this.mapChannel(rows[0]);
  }

  async listEnabled(): Promise<ChannelRecord[]> {
    const db = getDb();
    const [rows] = await db.query<ChannelRow[]>('SELECT * FROM yt_channels WHERE enabled = 1 ORDER BY id ASC');
    return rows.map((row) => this.mapChannel(row));
  }

  async list(filters: ChannelFilters): Promise<{ items: AggregatedChannelRecord[]; total: number }> {
    const db = getDb();
    const where: string[] = ['1=1'];
    const params: Array<string | number> = [];

    if (filters.category) {
      where.push('c.category = ?');
      params.push(filters.category);
    }

    if (typeof filters.enabled === 'boolean') {
      where.push('c.enabled = ?');
      params.push(filters.enabled ? 1 : 0);
    }

    if (typeof filters.isOnline === 'boolean') {
      where.push('COALESCE(s.is_online, 0) = ?');
      params.push(filters.isOnline ? 1 : 0);
    }

    if (typeof filters.isLive === 'boolean') {
      where.push('COALESCE(s.is_live, 0) = ?');
      params.push(filters.isLive ? 1 : 0);
    }

    const whereClause = where.join(' AND ');
    const offset = (filters.page - 1) * filters.limit;

    const [countRows] = await db.query<RowDataPacket[]>(
      `SELECT COUNT(*) AS total
       FROM yt_channels c
       LEFT JOIN yt_channel_status s ON s.channel_id = c.id
       WHERE ${whereClause}`,
      params,
    );

    const [rows] = await db.query<ChannelWithStatusRow[]>(
      `SELECT c.*, s.is_live, s.is_online, s.live_video_id, s.stream_url, s.format,
              s.last_http_code, s.last_checked_at, s.error_code, s.error_message
       FROM yt_channels c
       LEFT JOIN yt_channel_status s ON s.channel_id = c.id
       WHERE ${whereClause}
       ORDER BY c.id DESC
       LIMIT ? OFFSET ?`,
      [...params, filters.limit, offset],
    );

    return {
      items: rows.map((row) => ({
        ...this.mapChannel(row),
        status: this.mapStatus(row),
      })),
      total: Number(countRows[0].total ?? 0),
    };
  }

  async create(input: {
    name: string;
    category: string;
    channelUrl: string;
    liveUrl: string;
    enabled: boolean;
  }): Promise<ChannelRecord> {
    const db = getDb();
    const [result] = await db.execute<ResultSetHeader>(
      `INSERT INTO yt_channels (name, category, channel_url, live_url, enabled)
       VALUES (?, ?, ?, ?, ?)`,
      [input.name, input.category, input.channelUrl, input.liveUrl, input.enabled ? 1 : 0],
    );

    const created = await this.getById(result.insertId);
    if (!created) {
      throw new Error('Falha ao criar canal');
    }

    return created;
  }

  async upsertByChannelUrl(input: {
    name: string;
    category: string;
    channelUrl: string;
    liveUrl: string;
    enabled: boolean;
  }): Promise<ChannelRecord> {
    const db = getDb();
    const [result] = await db.execute<ResultSetHeader>(
      `INSERT INTO yt_channels (name, category, channel_url, live_url, enabled)
       VALUES (?, ?, ?, ?, ?)
       ON DUPLICATE KEY UPDATE
         id = LAST_INSERT_ID(id),
         name = VALUES(name),
         category = VALUES(category),
         live_url = VALUES(live_url),
         enabled = VALUES(enabled),
         updated_at = CURRENT_TIMESTAMP`,
      [input.name, input.category, input.channelUrl, input.liveUrl, input.enabled ? 1 : 0],
    );

    const upserted = await this.getById(result.insertId);
    if (!upserted) {
      throw new Error('Falha ao atualizar ou criar canal');
    }

    return upserted;
  }

  async update(
    id: number,
    input: Partial<Pick<ChannelRecord, 'name' | 'category' | 'channelUrl' | 'liveUrl' | 'enabled'>>,
  ): Promise<ChannelRecord | null> {
    const fields: string[] = [];
    const params: Array<string | number> = [];

    if (input.name !== undefined) {
      fields.push('name = ?');
      params.push(input.name);
    }

    if (input.category !== undefined) {
      fields.push('category = ?');
      params.push(input.category);
    }

    if (input.channelUrl !== undefined) {
      fields.push('channel_url = ?');
      params.push(input.channelUrl);
    }

    if (input.liveUrl !== undefined) {
      fields.push('live_url = ?');
      params.push(input.liveUrl);
    }

    if (input.enabled !== undefined) {
      fields.push('enabled = ?');
      params.push(input.enabled ? 1 : 0);
    }

    if (!fields.length) {
      return this.getById(id);
    }

    const db = getDb();
    await db.execute(`UPDATE yt_channels SET ${fields.join(', ')}, updated_at = CURRENT_TIMESTAMP WHERE id = ?`, [...params, id]);

    return this.getById(id);
  }

  async softDelete(id: number): Promise<void> {
    const db = getDb();
    await db.execute('UPDATE yt_channels SET enabled = 0, updated_at = CURRENT_TIMESTAMP WHERE id = ?', [id]);
  }

  async listOnlineForExport(): Promise<Array<{ id: number; name: string; category: string; streamUrl: string }>> {
    const db = getDb();
    const [rows] = await db.query<RowDataPacket[]>(
      `SELECT c.id, c.name, c.category, s.stream_url
       FROM yt_channels c
       INNER JOIN yt_channel_status s ON s.channel_id = c.id
       WHERE c.enabled = 1
         AND s.is_online = 1
         AND s.stream_url IS NOT NULL
       ORDER BY c.category ASC, c.name ASC`,
    );

    return rows.map((row) => ({
      id: Number(row.id),
      name: String(row.name),
      category: String(row.category),
      streamUrl: String(row.stream_url),
    }));
  }
}
