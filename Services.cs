using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MeuGestorVODs;

public class M3UEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string GroupTitle { get; set; } = string.Empty;
    public bool IsSelected { get; set; }

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

            entries.Add(new M3UEntry
            {
                Url = nextLine,
                Id = ExtractAttribute(line, "tvg-id") ?? Guid.NewGuid().ToString("N")[..8],
                Name = ExtractAttribute(line, "tvg-name") ?? ExtractName(line) ?? "Unknown",
                GroupTitle = ExtractAttribute(line, "group-title") ?? string.Empty
            });
        }

        return entries;
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

public class M3UService
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
}

public class DownloadService
{
    private readonly HttpClient _httpClient;

    public DownloadService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
    }

    public async Task DownloadFileAsync(string url, string outputPath, IProgress<double> progress)
    {
        var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;

        while (true)
        {
            var read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length));
            if (read == 0)
            {
                break;
            }

            await fileStream.WriteAsync(buffer.AsMemory(0, read));
            totalRead += read;

            if (totalBytes > 0)
            {
                progress.Report((double)totalRead / totalBytes * 100);
            }
        }
    }
}
