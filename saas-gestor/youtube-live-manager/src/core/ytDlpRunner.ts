import { spawn } from 'node:child_process';
import { env } from '../config/env';

export class YtDlpExecutionError extends Error {
  constructor(
    message: string,
    public readonly code: 'TIMEOUT' | 'FAILED',
    public readonly stderr: string,
    public readonly stdout: string,
  ) {
    super(message);
  }
}

export function classifyYtDlpError(output: string): 'BLOCKED' | 'TIMEOUT' | 'YTDLP_FAIL' {
  const lowered = output.toLowerCase();

  if (lowered.includes('429') || lowered.includes('captcha') || lowered.includes('sign in to confirm')) {
    return 'BLOCKED';
  }

  if (lowered.includes('timed out')) {
    return 'TIMEOUT';
  }

  return 'YTDLP_FAIL';
}

export async function runYtDlp(args: string[], timeoutSec: number = env.ytdlp.timeoutSec): Promise<string> {
  return new Promise((resolve, reject) => {
    const child = spawn(env.ytdlp.path, args, {
      stdio: ['ignore', 'pipe', 'pipe'],
    });

    let stdout = '';
    let stderr = '';
    let killedByTimeout = false;

    const timeout = setTimeout(() => {
      killedByTimeout = true;
      child.kill('SIGKILL');
    }, timeoutSec * 1000);

    child.stdout.on('data', (chunk: Buffer) => {
      stdout += chunk.toString();
    });

    child.stderr.on('data', (chunk: Buffer) => {
      stderr += chunk.toString();
    });

    child.on('error', (error) => {
      clearTimeout(timeout);
      reject(new YtDlpExecutionError(error.message, 'FAILED', stderr, stdout));
    });

    child.on('close', (code) => {
      clearTimeout(timeout);

      if (killedByTimeout) {
        reject(new YtDlpExecutionError('yt-dlp timeout', 'TIMEOUT', stderr, stdout));
        return;
      }

      if (code !== 0) {
        reject(new YtDlpExecutionError(`yt-dlp exited with code ${code}`, 'FAILED', stderr, stdout));
        return;
      }

      resolve(stdout.trim());
    });
  });
}
