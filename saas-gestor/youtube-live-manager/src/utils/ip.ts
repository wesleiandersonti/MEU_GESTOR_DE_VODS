export function normalizeIp(rawIp: string | undefined): string {
  if (!rawIp) {
    return '';
  }

  if (rawIp.startsWith('::ffff:')) {
    return rawIp.slice(7);
  }

  return rawIp;
}
