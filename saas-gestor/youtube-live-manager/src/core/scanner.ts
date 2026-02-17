import { env } from '../config/env';
import { ChannelRepository } from '../db/repositories/channelRepository';
import { ChannelRecord, CheckResult } from '../db/repositories/types';
import { StatusRepository } from '../db/repositories/statusRepository';
import { logger } from '../utils/logger';
import { ChannelChecker } from './channelChecker';
import { CircuitBreaker } from './circuitBreaker';

interface ScannerJob {
  channel: ChannelRecord;
  nextRunAt: number;
}

interface ScannerMetrics {
  totalChecks: number;
  totalDurationMs: number;
  results: Record<string, number>;
  lastCheckAt: string | null;
}

export class ScannerService {
  private running = false;
  private timer: NodeJS.Timeout | null = null;
  private jobs = new Map<number, ScannerJob>();
  private processing = new Set<number>();
  private lastRefreshAt = 0;
  private readonly metrics: ScannerMetrics = {
    totalChecks: 0,
    totalDurationMs: 0,
    results: {},
    lastCheckAt: null,
  };

  constructor(
    private readonly channelRepository: ChannelRepository,
    private readonly statusRepository: StatusRepository,
    private readonly checker: ChannelChecker,
    private readonly breaker: CircuitBreaker,
  ) {}

  async start(): Promise<void> {
    if (this.running) {
      return;
    }

    this.running = true;
    await this.refreshJobs(true);

    this.timer = setInterval(() => {
      void this.tick();
    }, 1000);

    logger.info('Scanner started', {
      intervalSec: env.scan.intervalSec,
      concurrency: env.scan.concurrency,
    });
  }

  stop(): void {
    if (!this.running) {
      return;
    }

    this.running = false;

    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }

    logger.info('Scanner stopped');
  }

  async runOnce(): Promise<{ processed: number }> {
    await this.refreshJobs(true);
    const items = Array.from(this.jobs.values()).map((job) => job.channel);
    await this.runWithConcurrency(items, env.scan.concurrency, async (channel) => {
      await this.processChannel(channel);
    });

    return { processed: items.length };
  }

  private async tick(): Promise<void> {
    if (!this.running) {
      return;
    }

    const now = Date.now();
    if (now - this.lastRefreshAt >= env.scan.jobRefreshSec * 1000) {
      await this.refreshJobs();
      this.lastRefreshAt = now;
    }

    const dueJobs = Array.from(this.jobs.values())
      .filter((job) => job.nextRunAt <= now && !this.processing.has(job.channel.id))
      .sort((a, b) => a.nextRunAt - b.nextRunAt);

    for (const job of dueJobs) {
      if (this.processing.size >= env.scan.concurrency) {
        break;
      }

      this.processing.add(job.channel.id);
      void this.processChannel(job.channel)
        .catch((error) => {
          logger.error('Scanner channel processing failed', {
            channelId: job.channel.id,
            error: error instanceof Error ? error.message : String(error),
          });
        })
        .finally(() => {
          this.processing.delete(job.channel.id);
        });
    }
  }

  private async refreshJobs(forceImmediate = false): Promise<void> {
    const channels = await this.channelRepository.listEnabled();
    const channelIds = new Set(channels.map((channel) => channel.id));

    for (const channel of channels) {
      const existing = this.jobs.get(channel.id);
      if (!existing) {
        this.jobs.set(channel.id, {
          channel,
          nextRunAt: forceImmediate ? Date.now() : Date.now() + env.scan.intervalSec * 1000,
        });
      } else {
        existing.channel = channel;
      }
    }

    for (const channelId of this.jobs.keys()) {
      if (!channelIds.has(channelId)) {
        this.jobs.delete(channelId);
      }
    }
  }

  private async processChannel(channel: ChannelRecord): Promise<void> {
    const outcome = await this.checker.run(channel, env.scan.maxRetries);
    this.trackMetrics(outcome.result, outcome.durationMs);

    const nextRunAt = Date.now() + ((outcome.cooldownSec ?? env.scan.intervalSec) * 1000);
    const job = this.jobs.get(channel.id);
    if (job) {
      job.nextRunAt = nextRunAt;
    }

    logger.info('Channel check completed', {
      channelId: channel.id,
      result: outcome.result,
      isLive: outcome.isLive,
      isOnline: outcome.isOnline,
      durationMs: outcome.durationMs,
    });
  }

  private trackMetrics(result: CheckResult, durationMs: number): void {
    this.metrics.totalChecks += 1;
    this.metrics.totalDurationMs += durationMs;
    this.metrics.lastCheckAt = new Date().toISOString();
    this.metrics.results[result] = (this.metrics.results[result] ?? 0) + 1;
  }

  async getStatus(): Promise<Record<string, unknown>> {
    const now = Date.now();
    const queueDepth = Array.from(this.jobs.values()).filter((job) => job.nextRunAt <= now).length;
    const onlineNow = await this.statusRepository.countOnlineNow();

    return {
      running: this.running,
      concurrency: env.scan.concurrency,
      scanIntervalSec: env.scan.intervalSec,
      jobsTotal: this.jobs.size,
      queueDepth,
      processingNow: this.processing.size,
      onlineNow,
      averageDurationMs:
        this.metrics.totalChecks > 0 ? Math.round(this.metrics.totalDurationMs / this.metrics.totalChecks) : 0,
      checksTotal: this.metrics.totalChecks,
      results: this.metrics.results,
      errorRateByType: this.computeErrorRates(),
      lastCheckAt: this.metrics.lastCheckAt,
      breaker: this.breaker.status(),
    };
  }

  private computeErrorRates(): Record<string, number> {
    if (this.metrics.totalChecks === 0) {
      return {};
    }

    const rates: Record<string, number> = {};
    for (const [key, value] of Object.entries(this.metrics.results)) {
      if (key === 'ONLINE') {
        continue;
      }

      rates[key] = Number(((value / this.metrics.totalChecks) * 100).toFixed(2));
    }

    return rates;
  }

  private async runWithConcurrency<T>(
    items: T[],
    concurrency: number,
    worker: (item: T) => Promise<void>,
  ): Promise<void> {
    let index = 0;

    const runWorker = async (): Promise<void> => {
      while (index < items.length) {
        const currentIndex = index;
        index += 1;
        await worker(items[currentIndex]);
      }
    };

    const workers = Array.from({ length: Math.max(1, concurrency) }, () => runWorker());
    await Promise.all(workers);
  }
}
