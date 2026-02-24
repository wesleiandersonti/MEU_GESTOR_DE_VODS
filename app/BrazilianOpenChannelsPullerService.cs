using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MeuGestorVODs;

public sealed class BrazilianOpenChannelsPullerService
{
    private static readonly HttpClient Http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<BrazilianOpenChannelsPullResult> PullAndSaveAsync(string outputPath, bool onlyOnline = false)
    {
        var result = new BrazilianOpenChannelsPullResult
        {
            OutputPath = outputPath,
            OnlyOnlineMode = onlyOnline
        };

        var sources = await LoadSourcesAsync();
        var allEntries = new List<M3UEntry>();

        foreach (var source in sources.Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Url)))
        {
            try
            {
                var content = await Http.GetStringAsync(source.Url);
                var parsed = M3UParser.Parse(content);
                result.SourcesRead++;
                result.TotalFound += parsed.Count;

                var filtered = parsed.Where(IsLikelyBrazilianChannel).ToList();
                allEntries.AddRange(filtered);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Fonte '{source.Name}' falhou: {ex.Message}");
            }
        }

        var unique = allEntries
            .Where(e => !string.IsNullOrWhiteSpace(e.Url))
            .GroupBy(e => e.Url.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        result.TotalUnique = unique.Count;

        List<M3UEntry> finalEntries = unique;
        if (onlyOnline)
        {
            var online = new List<M3UEntry>();
            var offline = 0;

            foreach (var entry in unique)
            {
                var isOnline = await IsStreamLikelyOnlineAsync(entry.Url.Trim());
                if (isOnline)
                {
                    online.Add(entry);
                }
                else
                {
                    offline++;
                }
            }

            result.TotalOnline = online.Count;
            result.TotalOffline = offline;
            finalEntries = online;
        }

        result.TotalSaved = finalEntries.Count;

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var m3u = BuildM3u(finalEntries);
        await File.WriteAllTextAsync(outputPath, m3u, Encoding.UTF8);

        return result;
    }

    private static async Task<bool> IsStreamLikelyOnlineAsync(string url)
    {
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(6));
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            var res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            return (int)res.StatusCode >= 200 && (int)res.StatusCode < 400;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildM3u(IEnumerable<M3UEntry> entries)
    {
        var lines = new List<string> { "#EXTM3U" };

        foreach (var entry in entries)
        {
            var name = Escape(entry.Name);
            var group = Escape(string.IsNullOrWhiteSpace(entry.GroupTitle) ? "Brasil | Canais Abertos" : entry.GroupTitle);
            var logo = Escape(entry.LogoUrl ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(logo))
            {
                lines.Add($"#EXTINF:-1 tvg-name=\"{name}\" tvg-logo=\"{logo}\" group-title=\"{group}\",{name}");
            }
            else
            {
                lines.Add($"#EXTINF:-1 tvg-name=\"{name}\" group-title=\"{group}\",{name}");
            }

            lines.Add(entry.Url.Trim());
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\"", "'").Trim();
    }

    private static bool IsLikelyBrazilianChannel(M3UEntry entry)
    {
        var text = $"{entry.Name} {entry.GroupTitle} {entry.Category} {entry.SubCategory} {entry.TvgId}".ToLowerInvariant();

        if (text.Contains("brasil") || text.Contains("brazil") || text.Contains("br ") || text.Contains("| br"))
            return true;

        string[] known =
        {
            "globo", "sbt", "record", "band", "rede tv", "tv cultura", "gazeta", "canal gov", "tv brasil", "camara", "senado", "justica"
        };

        return known.Any(k => text.Contains(k));
    }

    private static async Task<List<BrazilianOpenChannelsSource>> LoadSourcesAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "legal_sources_br.json");
        if (!File.Exists(path))
        {
            return GetDefaultSources();
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var parsed = JsonSerializer.Deserialize<List<BrazilianOpenChannelsSource>>(json);
            if (parsed == null || parsed.Count == 0)
            {
                return GetDefaultSources();
            }

            return parsed;
        }
        catch
        {
            return GetDefaultSources();
        }
    }

    private static List<BrazilianOpenChannelsSource> GetDefaultSources()
    {
        return new List<BrazilianOpenChannelsSource>
        {
            new() { Name = "IPTV-ORG BR (GitHub Pages)", Url = "https://iptv-org.github.io/iptv/countries/br.m3u", Enabled = true },
            new() { Name = "IPTV-ORG BR (Raw)", Url = "https://raw.githubusercontent.com/iptv-org/iptv/master/countries/br.m3u", Enabled = true },
            new() { Name = "IPTV-ORG Português", Url = "https://iptv-org.github.io/iptv/languages/por.m3u", Enabled = false }
        };
    }
}

public sealed class BrazilianOpenChannelsSource
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

public sealed class BrazilianOpenChannelsPullResult
{
    public string OutputPath { get; set; } = string.Empty;
    public bool OnlyOnlineMode { get; set; }
    public int SourcesRead { get; set; }
    public int TotalFound { get; set; }
    public int TotalUnique { get; set; }
    public int TotalOnline { get; set; }
    public int TotalOffline { get; set; }
    public int TotalSaved { get; set; }
    public List<string> Warnings { get; set; } = new();
}
