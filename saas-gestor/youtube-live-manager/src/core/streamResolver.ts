import { ChannelRecord } from '../db/repositories/types';
import { ResolveResult } from './types';
import { classifyYtDlpError, runYtDlp, YtDlpExecutionError } from './ytDlpRunner';

function pickFormat(streamUrl: string): 'HLS' | 'DASH' {
  if (streamUrl.includes('.mpd')) {
    return 'DASH';
  }

  return 'HLS';
}

export class StreamResolver {
  async resolve(channel: ChannelRecord, videoId?: string | null): Promise<ResolveResult> {
    const target = videoId ? `https://www.youtube.com/watch?v=${videoId}` : channel.liveUrl;

    const preferredArgs = [
      '-g',
      '--skip-download',
      '--no-warnings',
      '--format',
      'best[protocol*=m3u8]/best',
      target,
    ];

    try {
      const preferred = await runYtDlp(preferredArgs);
      const url = preferred
        .split('\n')
        .map((line) => line.replace(/\r/g, ''))
        .map((line) => line.trim())
        .find((line) => line.startsWith('http'));

      if (!url) {
        throw new Error('yt-dlp nao retornou stream URL');
      }

      return {
        ok: true,
        streamUrl: url,
        format: pickFormat(url),
        errorCode: null,
        errorMessage: null,
      };
    } catch (error) {
      if (error instanceof YtDlpExecutionError) {
        const combined = [error.stderr, error.stdout].filter(Boolean).join('\n').trim();
        const mapped = error.code === 'TIMEOUT' ? 'TIMEOUT' : classifyYtDlpError(combined);

        if (mapped === 'BLOCKED') {
          return {
            ok: false,
            streamUrl: null,
            format: null,
            errorCode: 'BLOCKED',
            errorMessage: combined || 'Bloqueio ao resolver stream.',
          };
        }

        if (mapped === 'TIMEOUT') {
          return {
            ok: false,
            streamUrl: null,
            format: null,
            errorCode: 'TIMEOUT',
            errorMessage: combined || 'Timeout ao resolver stream.',
          };
        }

        return {
          ok: false,
          streamUrl: null,
          format: null,
          errorCode: 'YTDLP_FAIL',
          errorMessage: combined || 'Falha no yt-dlp ao resolver stream.',
        };
      }

      return {
        ok: false,
        streamUrl: null,
        format: null,
        errorCode: 'YTDLP_FAIL',
        errorMessage: error instanceof Error ? error.message : String(error),
      };
    }
  }
}
