import { env } from '../config/env';
import { fetchWithTimeout } from '../utils/http';
import { ValidateResult } from './types';

function looksLikePlaylist(contentType: string, content: string): boolean {
  const loweredType = contentType.toLowerCase();
  const trimmed = content.trimStart();

  if (loweredType.includes('mpegurl') || loweredType.includes('dash+xml')) {
    return true;
  }

  if (trimmed.startsWith('#EXTM3U') || trimmed.includes('#EXTINF') || trimmed.startsWith('<MPD')) {
    return true;
  }

  return false;
}

export class StreamValidator {
  async validate(streamUrl: string): Promise<ValidateResult> {
    try {
      const head = await fetchWithTimeout(streamUrl, { method: 'HEAD' }, env.validator.timeoutSec * 1000);

      if (head.ok) {
        const contentType = head.headers.get('content-type') ?? '';
        if (contentType.toLowerCase().includes('mpegurl') || contentType.toLowerCase().includes('dash+xml')) {
          return {
            isOnline: true,
            httpCode: head.status,
            details: 'HEAD ok e content-type valido.',
          };
        }
      }

      const getResponse = await fetchWithTimeout(streamUrl, { method: 'GET' }, env.validator.timeoutSec * 1000);
      const body = await getResponse.text();

      if (!getResponse.ok) {
        return {
          isOnline: false,
          httpCode: getResponse.status,
          details: `HTTP ${getResponse.status} ao validar stream`,
        };
      }

      const contentType = getResponse.headers.get('content-type') ?? '';
      const valid = looksLikePlaylist(contentType, body);

      return {
        isOnline: valid,
        httpCode: getResponse.status,
        details: valid ? 'Playlist valida.' : 'Resposta sem assinatura de playlist.',
      };
    } catch (error) {
      return {
        isOnline: false,
        httpCode: null,
        details: error instanceof Error ? error.message : String(error),
      };
    }
  }
}
