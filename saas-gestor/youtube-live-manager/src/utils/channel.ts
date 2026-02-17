export function isYouTubeChannelUrl(url: string): boolean {
  try {
    const parsed = new URL(url.trim());
    const host = parsed.hostname.replace(/^www\./i, '').toLowerCase();
    return host === 'youtube.com' || host === 'youtu.be';
  } catch {
    return false;
  }
}

export function normalizeYouTubeChannelUrl(url: string): string {
  const parsed = new URL(url.trim());
  parsed.hash = '';
  parsed.search = '';

  const path = parsed.pathname.replace(/\/+$/, '');
  if (!path) {
    throw new Error('channel_url invalida');
  }

  parsed.pathname = path;
  return parsed.toString();
}

export function buildLiveUrl(channelUrl: string): string {
  const normalized = normalizeYouTubeChannelUrl(channelUrl);
  const parsed = new URL(normalized);
  const basePath = parsed.pathname.replace(/\/+$/, '');

  if (basePath.endsWith('/live')) {
    return parsed.toString();
  }

  parsed.pathname = `${basePath}/live`;
  return parsed.toString();
}

export function sanitizeGroupTitle(value: string): string {
  return value.replace(/"/g, "'").trim();
}
