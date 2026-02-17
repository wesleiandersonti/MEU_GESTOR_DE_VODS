using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MeuGestorVODs.Repositories;

namespace MeuGestorVODs;

internal readonly record struct CheckerProgressSnapshot(
    int CheckedCount,
    int OnlineCount,
    int OfflineCount,
    int DuplicateCount);

internal sealed class CheckerRunResult
{
    public int TotalCount { get; init; }
    public int CheckedCount { get; init; }
    public int OnlineCount { get; init; }
    public int OfflineCount { get; init; }
    public int DuplicateCount { get; init; }
    public IReadOnlyList<StreamCheckLogEntry> Logs { get; init; } = Array.Empty<StreamCheckLogEntry>();
    public IReadOnlyList<ServerScoreResult> ServerScores { get; init; } = Array.Empty<ServerScoreResult>();
}

internal sealed class CheckerOrchestrator
{
    private readonly StreamCheckService _streamCheckService;
    private readonly DuplicateDetectionService _duplicateDetectionService;
    private readonly ServerScoreService _serverScoreService;

    public CheckerOrchestrator(
        StreamCheckService streamCheckService,
        DuplicateDetectionService duplicateDetectionService,
        ServerScoreService serverScoreService)
    {
        _streamCheckService = streamCheckService;
        _duplicateDetectionService = duplicateDetectionService;
        _serverScoreService = serverScoreService;
    }

    public async Task<CheckerRunResult> RunAsync(
        IReadOnlyList<M3UEntry> entries,
        StreamCheckOptions options,
        Action<StreamCheckItemResult, CheckerProgressSnapshot>? onProgress,
        CancellationToken cancellationToken)
    {
        var duplicateCount = _duplicateDetectionService.MarkDuplicates(entries);

        foreach (var entry in entries)
        {
            entry.CheckStatus = ItemStatus.Checking;
            entry.CheckDetails = "Aguardando";
            entry.ServerHost = string.Empty;
            entry.ResponseTimeMs = 0;
            entry.LastCheckedAt = null;
        }

        var checkedCount = 0;
        var onlineCount = 0;
        var offlineCount = 0;
        var logs = new ConcurrentQueue<StreamCheckLogEntry>();

        await _streamCheckService.AnalyzeAsync(
            entries,
            options,
            result =>
            {
                if (result.IsOnline)
                {
                    Interlocked.Increment(ref onlineCount);
                }
                else
                {
                    Interlocked.Increment(ref offlineCount);
                }

                var currentChecked = Interlocked.Increment(ref checkedCount);
                var snapshot = new CheckerProgressSnapshot(
                    currentChecked,
                    Volatile.Read(ref onlineCount),
                    Volatile.Read(ref offlineCount),
                    duplicateCount);

                logs.Enqueue(new StreamCheckLogEntry
                {
                    Url = result.Entry.Url,
                    NormalizedUrl = result.NormalizedUrl,
                    ServerHost = result.ServerHost,
                    Status = result.IsOnline ? "ONLINE" : "OFFLINE",
                    ResponseTimeMs = result.ResponseTimeMs,
                    IsDuplicate = result.Entry.IsDuplicate,
                    CheckedAt = result.CheckedAt,
                    Details = result.Details
                });

                onProgress?.Invoke(result, snapshot);
                return Task.CompletedTask;
            },
            cancellationToken);

        return new CheckerRunResult
        {
            TotalCount = entries.Count,
            CheckedCount = Volatile.Read(ref checkedCount),
            OnlineCount = Volatile.Read(ref onlineCount),
            OfflineCount = Volatile.Read(ref offlineCount),
            DuplicateCount = duplicateCount,
            Logs = logs.ToList(),
            ServerScores = _serverScoreService.Calculate(entries).ToList()
        };
    }
}
