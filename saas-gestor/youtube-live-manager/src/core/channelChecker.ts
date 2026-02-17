import { env } from '../config/env';
import { ChannelRecord, CheckResult, ErrorCode } from '../db/repositories/types';
import { HistoryRepository } from '../db/repositories/historyRepository';
import { StatusRepository } from '../db/repositories/statusRepository';
import { sleep } from '../utils/sleep';
import { CircuitBreaker } from './circuitBreaker';
import { LiveDetector } from './liveDetector';
import { StreamResolver } from './streamResolver';
import { CheckOutcome } from './types';
import { StreamValidator } from './validator';

const RETRYABLE_ERRORS = new Set<ErrorCode>(['TIMEOUT', 'YTDLP_FAIL', 'HTTP_FAIL']);

export class ChannelChecker {
  constructor(
    private readonly detector: LiveDetector,
    private readonly resolver: StreamResolver,
    private readonly validator: StreamValidator,
    private readonly statusRepository: StatusRepository,
    private readonly historyRepository: HistoryRepository,
    private readonly breaker: CircuitBreaker,
  ) {}

  async run(channel: ChannelRecord, retries: number): Promise<CheckOutcome> {
    const startedAt = Date.now();
    let lastOutcome = await this.checkOnce(channel);

    for (let attempt = 1; attempt <= retries; attempt += 1) {
      if (!RETRYABLE_ERRORS.has(lastOutcome.errorCode)) {
        break;
      }

      if (attempt >= retries) {
        break;
      }

      const backoffMs = env.scan.retryBaseMs * (2 ** (attempt - 1));
      await sleep(backoffMs);
      lastOutcome = await this.checkOnce(channel);
    }

    const durationMs = Date.now() - startedAt;
    const outcome: CheckOutcome = {
      ...lastOutcome,
      durationMs,
    };

    await this.statusRepository.upsert({
      channelId: channel.id,
      isLive: outcome.isLive,
      isOnline: outcome.isOnline,
      liveVideoId: outcome.liveVideoId,
      streamUrl: outcome.streamUrl,
      format: outcome.format,
      lastHttpCode: outcome.httpCode,
      errorCode: outcome.errorCode,
      errorMessage: outcome.errorMessage,
    });

    await this.historyRepository.add({
      channelId: channel.id,
      result: outcome.result,
      details: {
        result: outcome.result,
        isLive: outcome.isLive,
        isOnline: outcome.isOnline,
        liveVideoId: outcome.liveVideoId,
        streamUrl: outcome.streamUrl,
        format: outcome.format,
        httpCode: outcome.httpCode,
        errorCode: outcome.errorCode,
        errorMessage: outcome.errorMessage,
      },
      durationMs,
    });

    return outcome;
  }

  private async checkOnce(channel: ChannelRecord): Promise<Omit<CheckOutcome, 'durationMs'>> {
    if (this.breaker.isOpen()) {
      return {
        result: 'BLOCKED',
        errorCode: 'BLOCKED',
        errorMessage: 'Circuit breaker aberto por bloqueio recente.',
        isLive: false,
        isOnline: false,
        liveVideoId: null,
        streamUrl: null,
        format: null,
        httpCode: null,
        cooldownSec: env.breaker.blockedCooldownSec,
      };
    }

    const detection = await this.detector.detect(channel);

    if (detection.state === 'BLOCKED') {
      this.breaker.recordBlocked();
      return {
        result: 'BLOCKED',
        errorCode: 'BLOCKED',
        errorMessage: detection.errorMessage,
        isLive: false,
        isOnline: false,
        liveVideoId: null,
        streamUrl: null,
        format: null,
        httpCode: null,
        cooldownSec: env.breaker.blockedCooldownSec,
      };
    }

    if (detection.state === 'SCHEDULED') {
      this.breaker.recordSuccess();
      return {
        result: 'SCHEDULED',
        errorCode: 'SCHEDULED',
        errorMessage: detection.errorMessage,
        isLive: false,
        isOnline: false,
        liveVideoId: detection.videoId,
        streamUrl: null,
        format: null,
        httpCode: null,
      };
    }

    if (detection.state === 'NO_LIVE') {
      this.breaker.recordSuccess();
      return {
        result: 'NO_LIVE',
        errorCode: 'NO_LIVE',
        errorMessage: detection.errorMessage,
        isLive: false,
        isOnline: false,
        liveVideoId: null,
        streamUrl: null,
        format: null,
        httpCode: null,
      };
    }

    if (detection.state === 'ERROR') {
      return {
        result: detection.errorCode === 'TIMEOUT' ? 'TIMEOUT' : 'YTDLP_FAIL',
        errorCode: detection.errorCode,
        errorMessage: detection.errorMessage,
        isLive: false,
        isOnline: false,
        liveVideoId: null,
        streamUrl: null,
        format: null,
        httpCode: null,
      };
    }

    const resolved = await this.resolver.resolve(channel, detection.videoId);

    if (!resolved.ok || !resolved.streamUrl) {
      if (resolved.errorCode === 'BLOCKED') {
        this.breaker.recordBlocked();
        return {
          result: 'BLOCKED',
          errorCode: 'BLOCKED',
          errorMessage: resolved.errorMessage,
          isLive: true,
          isOnline: false,
          liveVideoId: detection.videoId,
          streamUrl: null,
          format: null,
          httpCode: null,
          cooldownSec: env.breaker.blockedCooldownSec,
        };
      }

      return {
        result: resolved.errorCode === 'TIMEOUT' ? 'TIMEOUT' : 'YTDLP_FAIL',
        errorCode: resolved.errorCode,
        errorMessage: resolved.errorMessage,
        isLive: true,
        isOnline: false,
        liveVideoId: detection.videoId,
        streamUrl: null,
        format: null,
        httpCode: null,
      };
    }

    const validation = await this.validator.validate(resolved.streamUrl);

    if (!validation.isOnline) {
      return {
        result: 'HTTP_FAIL',
        errorCode: 'HTTP_FAIL',
        errorMessage: validation.details,
        isLive: true,
        isOnline: false,
        liveVideoId: detection.videoId,
        streamUrl: resolved.streamUrl,
        format: resolved.format,
        httpCode: validation.httpCode,
      };
    }

    this.breaker.recordSuccess();

    return {
      result: 'ONLINE',
      errorCode: null,
      errorMessage: null,
      isLive: true,
      isOnline: true,
      liveVideoId: detection.videoId,
      streamUrl: resolved.streamUrl,
      format: resolved.format,
      httpCode: validation.httpCode,
    };
  }
}
