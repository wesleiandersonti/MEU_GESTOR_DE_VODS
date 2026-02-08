using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MeuGestorVODs;

public sealed class StreamCheckOptions
{
    public int TimeoutSeconds { get; set; } = 6;
    public int MaxParallelism { get; set; } = 48;
}

public sealed class StreamCheckItemResult
{
    public M3UEntry Entry { get; init; } = new M3UEntry();
    public string NormalizedUrl { get; init; } = string.Empty;
    public string ServerHost { get; init; } = string.Empty;
    public ItemStatus Status { get; init; }
    public bool IsOnline { get; init; }
    public double ResponseTimeMs { get; init; }
    public string Details { get; init; } = string.Empty;
    public DateTime CheckedAt { get; init; } = DateTime.Now;
}

public class StreamCheckService
{
    private readonly HttpClient _httpClient;

    public StreamCheckService()
    {
        _httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MeuGestorVODs-StreamChecker/1.0");
    }

    public async Task AnalyzeAsync(
        IReadOnlyList<M3UEntry> entries,
        StreamCheckOptions options,
        Func<StreamCheckItemResult, Task> onResult,
        CancellationToken cancellationToken)
    {
        var parallelism = Math.Max(4, options.MaxParallelism);
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = parallelism,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(entries, parallelOptions, async (entry, token) =>
        {
            var result = await CheckOneAsync(entry, options.TimeoutSeconds, token);
            await onResult(result);
        });
    }

    private async Task<StreamCheckItemResult> CheckOneAsync(M3UEntry entry, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var normalized = DuplicateDetectionService.NormalizeUrl(entry.Url);
        var host = TryGetHost(entry.Url);
        var sw = Stopwatch.StartNew();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(2, timeoutSeconds)));
            var requestToken = timeoutCts.Token;

            using var head = new HttpRequestMessage(HttpMethod.Head, entry.Url);
            using var headResponse = await _httpClient.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, requestToken);
            sw.Stop();

            if ((int)headResponse.StatusCode < 400)
            {
                return new StreamCheckItemResult
                {
                    Entry = entry,
                    NormalizedUrl = normalized,
                    ServerHost = host,
                    Status = ItemStatus.Ok,
                    IsOnline = true,
                    ResponseTimeMs = sw.Elapsed.TotalMilliseconds,
                    Details = $"HEAD {(int)headResponse.StatusCode}",
                    CheckedAt = DateTime.Now
                };
            }

            if (headResponse.StatusCode == HttpStatusCode.MethodNotAllowed || headResponse.StatusCode == HttpStatusCode.NotImplemented)
            {
                sw.Restart();
                using var get = new HttpRequestMessage(HttpMethod.Get, entry.Url);
                get.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 2048);
                using var getResponse = await _httpClient.SendAsync(get, HttpCompletionOption.ResponseHeadersRead, requestToken);
                sw.Stop();

                var ok = (int)getResponse.StatusCode < 400;
                return new StreamCheckItemResult
                {
                    Entry = entry,
                    NormalizedUrl = normalized,
                    ServerHost = host,
                    Status = ok ? ItemStatus.Ok : ItemStatus.Error,
                    IsOnline = ok,
                    ResponseTimeMs = sw.Elapsed.TotalMilliseconds,
                    Details = $"GET {(int)getResponse.StatusCode}",
                    CheckedAt = DateTime.Now
                };
            }

            return new StreamCheckItemResult
            {
                Entry = entry,
                NormalizedUrl = normalized,
                ServerHost = host,
                Status = ItemStatus.Error,
                IsOnline = false,
                ResponseTimeMs = sw.Elapsed.TotalMilliseconds,
                Details = $"HEAD {(int)headResponse.StatusCode}",
                CheckedAt = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new StreamCheckItemResult
            {
                Entry = entry,
                NormalizedUrl = normalized,
                ServerHost = host,
                Status = ItemStatus.Error,
                IsOnline = false,
                ResponseTimeMs = sw.Elapsed.TotalMilliseconds,
                Details = ex.Message,
                CheckedAt = DateTime.Now
            };
        }
    }

    private static string TryGetHost(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        return "desconhecido";
    }
}

public class DuplicateDetectionService
{
    private static readonly HashSet<string> VolatileQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "token", "auth", "expires", "exp", "sig", "signature", "session", "sessionid", "userid", "uid", "ts"
    };

    public int MarkDuplicates(IReadOnlyList<M3UEntry> entries)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = 0;

        foreach (var entry in entries)
        {
            var normalized = NormalizeUrl(entry.Url);
            entry.NormalizedUrl = normalized;

            if (!seen.Add(normalized))
            {
                entry.IsDuplicate = true;
                duplicates++;
            }
            else
            {
                entry.IsDuplicate = false;
            }
        }

        return duplicates;
    }

    public static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url.Trim();
        }

        var basePart = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(uri.Query))
        {
            return basePart.ToLowerInvariant();
        }

        var query = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('=', 2))
            .Where(parts => parts.Length > 0 && !VolatileQueryKeys.Contains(parts[0]))
            .Select(parts => parts.Length == 2 ? $"{parts[0]}={parts[1]}" : parts[0])
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (query.Length == 0)
        {
            return basePart.ToLowerInvariant();
        }

        return $"{basePart.ToLowerInvariant()}?{string.Join("&", query)}";
    }
}

public class ServerScoreService
{
    public List<ServerScoreResult> Calculate(IReadOnlyList<M3UEntry> entries)
    {
        var result = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.ServerHost) && !string.Equals(e.ServerHost, "desconhecido", StringComparison.OrdinalIgnoreCase))
            .GroupBy(e => e.ServerHost, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var total = group.Count();
                var online = group.Count(x => x.CheckStatus == ItemStatus.Ok);
                var offline = total - online;
                var avg = group.Where(x => x.ResponseTimeMs > 0).Select(x => x.ResponseTimeMs).DefaultIfEmpty(0).Average();
                var successRate = total == 0 ? 0 : (double)online / total * 100.0;
                var latencyScore = avg <= 0 ? 0 : Math.Max(0, 100 - (avg / 6.0));
                var score = Math.Round(successRate * 0.75 + latencyScore * 0.25, 1);

                return new ServerScoreResult
                {
                    Host = group.Key,
                    TotalLinks = total,
                    OnlineLinks = online,
                    OfflineLinks = offline,
                    SuccessRate = Math.Round(successRate, 1),
                    AverageResponseMs = Math.Round(avg, 1),
                    Score = score,
                    Quality = ToQuality(score)
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.TotalLinks)
            .ToList();

        return result;
    }

    private static ServerQuality ToQuality(double score)
    {
        if (score >= 85) return ServerQuality.Excelente;
        if (score >= 70) return ServerQuality.Bom;
        if (score >= 50) return ServerQuality.Regular;
        return ServerQuality.Ruim;
    }
}
