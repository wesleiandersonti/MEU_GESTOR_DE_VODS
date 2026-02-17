import { CheckResult, ErrorCode, StreamFormat } from '../db/repositories/types';

export interface DetectionResult {
  state: 'LIVE' | 'NO_LIVE' | 'SCHEDULED' | 'BLOCKED' | 'ERROR';
  videoId: string | null;
  errorCode: ErrorCode;
  errorMessage: string | null;
}

export interface ResolveResult {
  ok: boolean;
  streamUrl: string | null;
  format: StreamFormat;
  errorCode: ErrorCode;
  errorMessage: string | null;
}

export interface ValidateResult {
  isOnline: boolean;
  httpCode: number | null;
  details: string | null;
}

export interface CheckOutcome {
  result: CheckResult;
  errorCode: ErrorCode;
  errorMessage: string | null;
  isLive: boolean;
  isOnline: boolean;
  liveVideoId: string | null;
  streamUrl: string | null;
  format: StreamFormat;
  httpCode: number | null;
  durationMs: number;
  cooldownSec?: number;
}
