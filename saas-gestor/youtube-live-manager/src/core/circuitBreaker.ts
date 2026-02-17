export class CircuitBreaker {
  private blockedCount = 0;
  private openedUntil = 0;

  constructor(
    private readonly threshold: number,
    private readonly cooldownMs: number,
  ) {}

  isOpen(now = Date.now()): boolean {
    return now < this.openedUntil;
  }

  recordBlocked(now = Date.now()): void {
    this.blockedCount += 1;

    if (this.blockedCount >= this.threshold) {
      this.openedUntil = now + this.cooldownMs;
      this.blockedCount = 0;
    }
  }

  recordSuccess(): void {
    this.blockedCount = 0;
    this.openedUntil = 0;
  }

  status(): { isOpen: boolean; openedUntil: number; blockedCount: number } {
    const now = Date.now();
    return {
      isOpen: this.isOpen(now),
      openedUntil: this.openedUntil,
      blockedCount: this.blockedCount,
    };
  }
}
