import { ChannelRecord } from '../db/repositories/types';
import { DetectionResult } from './types';
import { classifyYtDlpError, runYtDlp, YtDlpExecutionError } from './ytDlpRunner';

interface YtLiveDump {
  id?: string;
  is_live?: boolean;
  live_status?: string;
}

export class LiveDetector {
  async detect(channel: ChannelRecord): Promise<DetectionResult> {
    const args = ['--dump-single-json', '--skip-download', '--no-warnings', channel.liveUrl];

    try {
      const raw = await runYtDlp(args);
      const data = JSON.parse(raw) as YtLiveDump;

      const liveStatus = (data.live_status ?? '').toLowerCase();
      const isLive = data.is_live === true || liveStatus === 'is_live';

      if (isLive) {
        return {
          state: 'LIVE',
          videoId: data.id ?? null,
          errorCode: null,
          errorMessage: null,
        };
      }

      if (liveStatus === 'is_upcoming') {
        return {
          state: 'SCHEDULED',
          videoId: data.id ?? null,
          errorCode: 'SCHEDULED',
          errorMessage: 'Live agendada no momento.',
        };
      }

      return {
        state: 'NO_LIVE',
        videoId: null,
        errorCode: 'NO_LIVE',
        errorMessage: 'Canal sem live ativa.',
      };
    } catch (error) {
      if (error instanceof YtDlpExecutionError) {
        const combined = [error.stderr, error.stdout].filter(Boolean).join('\n').trim();
        const mapped = error.code === 'TIMEOUT' ? 'TIMEOUT' : classifyYtDlpError(combined);

        if (mapped === 'BLOCKED') {
          return {
            state: 'BLOCKED',
            videoId: null,
            errorCode: 'BLOCKED',
            errorMessage: combined || 'Bloqueio temporario pelo YouTube.',
          };
        }

        if (mapped === 'TIMEOUT') {
          return {
            state: 'ERROR',
            videoId: null,
            errorCode: 'TIMEOUT',
            errorMessage: combined || 'Timeout ao consultar live.',
          };
        }

        return {
          state: 'ERROR',
          videoId: null,
          errorCode: 'YTDLP_FAIL',
          errorMessage: combined || 'Falha do yt-dlp no detector.',
        };
      }

      return {
        state: 'ERROR',
        videoId: null,
        errorCode: 'YTDLP_FAIL',
        errorMessage: error instanceof Error ? error.message : String(error),
      };
    }
  }
}
