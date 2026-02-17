import { RowDataPacket } from 'mysql2';
import { getDb } from '../connection';
import { ChannelStatusRecord, ErrorCode, StreamFormat } from './types';

interface StatusRow extends RowDataPacket {
  channel_id: number;
  is_live: number;
  is_online: number;
  live_video_id: string | null;
  stream_url: string | null;
  format: StreamFormat;
  last_http_code: number | null;
  last_checked_at: string;
  error_code: ErrorCode;
  error_message: string | null;
}

export class StatusRepository {
  private map(row: StatusRow): ChannelStatusRecord {
    return {
      channelId: row.channel_id,
      isLive: row.is_live === 1,
      isOnline: row.is_online === 1,
      liveVideoId: row.live_video_id,
      streamUrl: row.stream_url,
      format: row.format,
      lastHttpCode: row.last_http_code,
      lastCheckedAt: row.last_checked_at,
      errorCode: row.error_code,
      errorMessage: row.error_message,
    };
  }

  async getByChannelId(channelId: number): Promise<ChannelStatusRecord | null> {
    const db = getDb();
    const [rows] = await db.query<StatusRow[]>(
      'SELECT * FROM yt_channel_status WHERE channel_id = ? LIMIT 1',
      [channelId],
    );

    if (!rows.length) {
      return null;
    }

    return this.map(rows[0]);
  }

  async upsert(input: {
    channelId: number;
    isLive: boolean;
    isOnline: boolean;
    liveVideoId: string | null;
    streamUrl: string | null;
    format: StreamFormat;
    lastHttpCode: number | null;
    errorCode: ErrorCode;
    errorMessage: string | null;
  }): Promise<void> {
    const db = getDb();
    await db.execute(
      `INSERT INTO yt_channel_status (
          channel_id, is_live, is_online, live_video_id, stream_url, format,
          last_http_code, last_checked_at, error_code, error_message
        )
        VALUES (?, ?, ?, ?, ?, ?, ?, CURRENT_TIMESTAMP, ?, ?)
        ON DUPLICATE KEY UPDATE
          is_live = VALUES(is_live),
          is_online = VALUES(is_online),
          live_video_id = VALUES(live_video_id),
          stream_url = VALUES(stream_url),
          format = VALUES(format),
          last_http_code = VALUES(last_http_code),
          last_checked_at = CURRENT_TIMESTAMP,
          error_code = VALUES(error_code),
          error_message = VALUES(error_message)`,
      [
        input.channelId,
        input.isLive ? 1 : 0,
        input.isOnline ? 1 : 0,
        input.liveVideoId,
        input.streamUrl,
        input.format,
        input.lastHttpCode,
        input.errorCode,
        input.errorMessage,
      ],
    );
  }

  async countOnlineNow(): Promise<number> {
    const db = getDb();
    const [rows] = await db.query<RowDataPacket[]>('SELECT COUNT(*) AS total FROM yt_channel_status WHERE is_online = 1');
    return Number(rows[0].total ?? 0);
  }
}
