export type ErrorCode =
  | 'NO_LIVE'
  | 'SCHEDULED'
  | 'BLOCKED'
  | 'TIMEOUT'
  | 'YTDLP_FAIL'
  | 'HTTP_FAIL'
  | null;

export type CheckResult = 'ONLINE' | 'NO_LIVE' | 'SCHEDULED' | 'BLOCKED' | 'TIMEOUT' | 'YTDLP_FAIL' | 'HTTP_FAIL';
export type StreamFormat = 'HLS' | 'DASH' | null;

export interface ChannelRecord {
  id: number;
  name: string;
  category: string;
  channelUrl: string;
  liveUrl: string;
  enabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface ChannelStatusRecord {
  channelId: number;
  isLive: boolean;
  isOnline: boolean;
  liveVideoId: string | null;
  streamUrl: string | null;
  format: StreamFormat;
  lastHttpCode: number | null;
  lastCheckedAt: string;
  errorCode: ErrorCode;
  errorMessage: string | null;
}

export interface AggregatedChannelRecord extends ChannelRecord {
  status: ChannelStatusRecord | null;
}
