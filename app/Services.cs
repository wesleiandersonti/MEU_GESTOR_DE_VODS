using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MeuGestorVODs;

public enum ItemStatus
{
    Checking,
    Ok,
    Error
}

public enum ServerQuality
{
    Excelente,
    Bom,
    Regular,
    Ruim
}

public class ServerScoreResult
{
    public string Host { get; set; } = string.Empty;
    public int TotalLinks { get; set; }
    public int OnlineLinks { get; set; }
    public int OfflineLinks { get; set; }
    public double SuccessRate { get; set; }
    public double AverageResponseMs { get; set; }
    public double Score { get; set; }
    public ServerQuality Quality { get; set; } = ServerQuality.Regular;
}

public class M3UEntry : INotifyPropertyChanged
{
    private bool _isSelected;
    private ItemStatus _checkStatus = ItemStatus.Error;
    private bool _isDuplicate;
    private string _normalizedUrl = string.Empty;
    private string _serverHost = string.Empty;
    private double _responseTimeMs;
    private DateTime? _lastCheckedAt;
    private string _checkDetails = string.Empty;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string GroupTitle { get; set; } = string.Empty;
    public string Category { get; set; } = "Sem Categoria";
    public string SubCategory { get; set; } = "Geral";
    public string LogoUrl { get; set; } = string.Empty;
    public string TvgId { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public ItemStatus CheckStatus
    {
        get => _checkStatus;
        set
        {
            if (_checkStatus == value) return;
            _checkStatus = value;
            OnPropertyChanged(nameof(CheckStatus));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsOnline));
        }
    }

    public bool IsDuplicate
    {
        get => _isDuplicate;
        set
        {
            if (_isDuplicate == value) return;
            _isDuplicate = value;
            OnPropertyChanged(nameof(IsDuplicate));
            OnPropertyChanged(nameof(DuplicateText));
        }
    }

    public string NormalizedUrl
    {
        get => _normalizedUrl;
        set
        {
            if (_normalizedUrl == value) return;
            _normalizedUrl = value;
            OnPropertyChanged(nameof(NormalizedUrl));
        }
    }

    public string ServerHost
    {
        get => _serverHost;
        set
        {
            if (_serverHost == value) return;
            _serverHost = value;
            OnPropertyChanged(nameof(ServerHost));
        }
    }

    public double ResponseTimeMs
    {
        get => _responseTimeMs;
        set
        {
            if (Math.Abs(_responseTimeMs - value) < 0.01) return;
            _responseTimeMs = value;
            OnPropertyChanged(nameof(ResponseTimeMs));
        }
    }

    public DateTime? LastCheckedAt
    {
        get => _lastCheckedAt;
        set
        {
            if (_lastCheckedAt == value) return;
            _lastCheckedAt = value;
            OnPropertyChanged(nameof(LastCheckedAt));
        }
    }

    public string CheckDetails
    {
        get => _checkDetails;
        set
        {
            if (_checkDetails == value) return;
            _checkDetails = value;
            OnPropertyChanged(nameof(CheckDetails));
        }
    }

    public bool IsOnline => CheckStatus == ItemStatus.Ok;
    public string DuplicateText => IsDuplicate ? "Sim" : "Nao";
    public string StatusText => CheckStatus switch
    {
        ItemStatus.Checking => "CHECKING",
        ItemStatus.Ok => "ONLINE",
        ItemStatus.Error => "OFFLINE",
        _ => "-"
    };

    public string GroupDisplay => $"{Category} | {SubCategory}";
    public string GroupKey => $"{Category}|{SubCategory}";

    public string SanitizedName
    {
        get
        {
            var name = Name;
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return string.IsNullOrWhiteSpace(name) ? "unknown" : name;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public static class M3UParser
{
    public static List<M3UEntry> Parse(string content)
    {
        var entries = new List<M3UEntry>();
        var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i].Trim();

            if (!line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var nextLine = lines[i + 1].Trim();
            if (string.IsNullOrWhiteSpace(nextLine) || nextLine.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var rawGroupTitle = ExtractAttribute(line, "group-title") ?? string.Empty;
            var (category, subCategory, groupDisplay) = ParseGroupTitle(rawGroupTitle);

            entries.Add(new M3UEntry
            {
                Url = nextLine,
                Id = ExtractAttribute(line, "tvg-id") ?? Guid.NewGuid().ToString("N")[..8],
                TvgId = ExtractAttribute(line, "tvg-id") ?? string.Empty,
                Name = ExtractAttribute(line, "tvg-name") ?? ExtractName(line) ?? "Unknown",
                GroupTitle = groupDisplay,
                Category = category,
                SubCategory = subCategory,
                LogoUrl = ExtractAttribute(line, "tvg-logo") ?? string.Empty
            });
        }

        return entries;
    }

    private static (string category, string subCategory, string groupDisplay) ParseGroupTitle(string rawGroupTitle)
    {
        var value = (rawGroupTitle ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return ("Sem Categoria", "Geral", "Sem Categoria | Geral");
        }

        var primarySeparator = value.Contains('|') ? '|' : value.Contains('>') ? '>' : '\0';
        if (primarySeparator != '\0')
        {
            var parts = value.Split(primarySeparator, StringSplitOptions.RemoveEmptyEntries)
                             .Select(p => p.Trim())
                             .Where(p => !string.IsNullOrWhiteSpace(p))
                             .ToArray();
            if (parts.Length >= 2)
            {
                var category = parts[0];
                var subCategory = string.Join(" | ", parts.Skip(1));
                return (category, subCategory, $"{category} | {subCategory}");
            }
        }

        return (value, "Geral", $"{value} | Geral");
    }

    private static string? ExtractAttribute(string line, string attribute)
    {
        var pattern = $"{attribute}=\"([^\"]*)\"";
        var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractName(string line)
    {
        var lastComma = line.LastIndexOf(',');
        if (lastComma >= 0 && lastComma < line.Length - 1)
        {
            return line[(lastComma + 1)..].Trim();
        }

        return null;
    }
}

public class M3UService : IDisposable
{
    private readonly HttpClient _httpClient;

    public M3UService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MeuGestorVODs/1.0");
    }

    public async Task<List<M3UEntry>> LoadFromUrlAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return M3UParser.Parse(content);
    }

    public List<M3UEntry> ParseFromString(string content)
    {
        return M3UParser.Parse(content);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public class DownloadService : IDisposable
{
    private readonly HttpClient _httpClient;

    public DownloadService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
    }

    public class DownloadProgressInfo
    {
        public double Percent { get; set; }
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public double SpeedBytesPerSecond { get; set; }
    }

    public Task DownloadFileAsync(string url, string outputPath, IProgress<double> progress)
    {
        return DownloadFileAsync(url, outputPath, progress, CancellationToken.None, null);
    }

    public Task DownloadFileAsync(string url, string outputPath, IProgress<DownloadProgressInfo> progress, CancellationToken cancellationToken, ManualResetEventSlim? pauseGate)
    {
        return DownloadFileCoreAsync(url, outputPath, progress, cancellationToken, pauseGate, null);
    }

    public Task DownloadFileAsync(
        string url,
        string outputPath,
        IProgress<DownloadProgressInfo> progress,
        CancellationToken cancellationToken,
        ManualResetEventSlim? globalPauseGate,
        ManualResetEventSlim? itemPauseGate)
    {
        return DownloadFileCoreAsync(url, outputPath, progress, cancellationToken, globalPauseGate, itemPauseGate);
    }

    public async Task DownloadFileAsync(
        string url,
        string outputPath,
        IProgress<double> progress,
        CancellationToken cancellationToken,
        ManualResetEventSlim? pauseGate)
    {
        var richProgress = new Progress<DownloadProgressInfo>(p => progress.Report(p.Percent));
        await DownloadFileCoreAsync(url, outputPath, richProgress, cancellationToken, pauseGate, null);
    }

    private async Task DownloadFileCoreAsync(
        string url,
        string outputPath,
        IProgress<DownloadProgressInfo> progress,
        CancellationToken cancellationToken,
        ManualResetEventSlim? globalPauseGate,
        ManualResetEventSlim? itemPauseGate)
    {
        var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;
        var startedAt = DateTime.UtcNow;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            globalPauseGate?.Wait(cancellationToken);
            itemPauseGate?.Wait(cancellationToken);

            var read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            totalRead += read;

            var elapsedSeconds = Math.Max((DateTime.UtcNow - startedAt).TotalSeconds, 0.001);
            var speed = totalRead / elapsedSeconds;
            var percent = totalBytes > 0 ? (double)totalRead / totalBytes * 100d : 0d;

            progress.Report(new DownloadProgressInfo
            {
                Percent = percent,
                DownloadedBytes = totalRead,
                TotalBytes = totalBytes,
                SpeedBytesPerSecond = speed
            });
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public class LinkCheckResult
{
    public bool IsOnline { get; set; }
    public string Details { get; set; } = string.Empty;
}

public class LinkHealthService : IDisposable
{
    private readonly HttpClient _httpClient;

    public LinkHealthService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MeuGestorVODs-LinkChecker/1.0");
    }

    public async Task<LinkCheckResult> CheckAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new LinkCheckResult { IsOnline = false, Details = "URL vazia" };
        }

        if (url.StartsWith("[LOCAL]", StringComparison.OrdinalIgnoreCase))
        {
            return new LinkCheckResult { IsOnline = true, Details = "Arquivo local" };
        }

        try
        {
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResponse = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead);
            if ((int)headResponse.StatusCode < 400)
            {
                return new LinkCheckResult { IsOnline = true, Details = $"HEAD {(int)headResponse.StatusCode}" };
            }

            if (headResponse.StatusCode == HttpStatusCode.MethodNotAllowed ||
                headResponse.StatusCode == HttpStatusCode.NotImplemented)
            {
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                using var getResponse = await _httpClient.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead);
                if ((int)getResponse.StatusCode < 400)
                {
                    return new LinkCheckResult { IsOnline = true, Details = $"GET {(int)getResponse.StatusCode}" };
                }

                return new LinkCheckResult { IsOnline = false, Details = $"GET {(int)getResponse.StatusCode}" };
            }

            return new LinkCheckResult { IsOnline = false, Details = $"HEAD {(int)headResponse.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new LinkCheckResult { IsOnline = false, Details = ex.Message };
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
