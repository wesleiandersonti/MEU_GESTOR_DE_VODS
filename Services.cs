using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;

namespace MeuGestorVODs;

public class M3UEntry : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isSelected;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string GroupTitle { get; set; } = string.Empty;
    public string Category { get; set; } = "Sem Categoria";
    public string SubCategory { get; set; } = "Geral";
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

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

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
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
                Name = ExtractAttribute(line, "tvg-name") ?? ExtractName(line) ?? "Unknown",
                GroupTitle = groupDisplay,
                Category = category,
                SubCategory = subCategory
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

public class StorageService
{
    public void EnsureLinkDatabaseFiles(string downloadPath, string vodFileName, string liveFileName)
    {
        if (!Directory.Exists(downloadPath))
        {
            Directory.CreateDirectory(downloadPath);
        }

        var vodFilePath = Path.Combine(downloadPath, vodFileName);
        if (!File.Exists(vodFilePath))
        {
            File.WriteAllLines(vodFilePath, new[]
            {
                "# Banco TXT de links VOD (videos e series)",
                "# Formato: Nome|Grupo|URL"
            });
        }

        var liveFilePath = Path.Combine(downloadPath, liveFileName);
        if (!File.Exists(liveFilePath))
        {
            File.WriteAllLines(liveFilePath, new[]
            {
                "# Banco TXT de links de canais ao vivo",
                "# Formato: Nome|Grupo|URL"
            });
        }
    }

    public (int newVod, int newLive) PersistLinkDatabases(string downloadPath, IEnumerable<M3UEntry> entries, string vodFileName, string liveFileName)
    {
        EnsureLinkDatabaseFiles(downloadPath, vodFileName, liveFileName);

        var vodFilePath = Path.Combine(downloadPath, vodFileName);
        var liveFilePath = Path.Combine(downloadPath, liveFileName);

        var newVod = MergeEntriesIntoDatabase(vodFilePath, entries.Where(IsVodEntry));
        var newLive = MergeEntriesIntoDatabase(liveFilePath, entries.Where(e => !IsVodEntry(e)));

        return (newVod, newLive);
    }

    public Dictionary<string, string> EnsureAndLoadDownloadStructure(string downloadPath, string structureFileName)
    {
        if (!Directory.Exists(downloadPath))
        {
            Directory.CreateDirectory(downloadPath);
        }

        var structurePath = Path.Combine(downloadPath, structureFileName);
        if (!File.Exists(structurePath))
        {
            File.WriteAllLines(structurePath, new[]
            {
                "# Estrutura de pastas para downloads",
                "# Formato: Categoria=Pasta",
                "Videos=Videos",
                "Series=Series",
                "Filmes=Filmes",
                "Canais=Canais",
                "24 Horas=24 Horas",
                "Documentarios=Documentarios",
                "Novelas=Novelas",
                "Outros=Outros"
            });
        }

        var structure = LoadStructure(structurePath);

        foreach (var folder in structure.Values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.Combine(downloadPath, folder));
        }

        return structure;
    }

    public bool IsVodEntry(M3UEntry entry)
    {
        var category = ResolveCategory(entry);
        if (string.Equals(category, "Canais", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "24 Horas", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var url = entry.Url?.ToLowerInvariant() ?? string.Empty;
        if (url.Contains("/live") || url.Contains("/channel") || url.Contains("channels"))
        {
            return false;
        }

        return true;
    }

    public string BuildOutputPath(string downloadPath, Dictionary<string, string> structure, M3UEntry entry)
    {
        var category = ResolveCategory(entry);
        if (!structure.TryGetValue(category, out var folderName))
        {
            if (!structure.TryGetValue("Outros", out folderName))
            {
                folderName = "Outros";
            }
        }

        var folderPath = Path.Combine(downloadPath, folderName);
        Directory.CreateDirectory(folderPath);

        return Path.Combine(folderPath, entry.SanitizedName + ResolveFileExtension(entry.Url));
    }

    private static int MergeEntriesIntoDatabase(string filePath, IEnumerable<M3UEntry> entries)
    {
        var existingUrls = LoadExistingUrls(filePath);
        var linesToAppend = new List<string>();

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Url))
            {
                continue;
            }

            if (!existingUrls.Add(entry.Url.Trim()))
            {
                continue;
            }

            var safeName = (entry.Name ?? string.Empty).Replace("|", " ").Trim();
            var safeGroup = (entry.GroupTitle ?? string.Empty).Replace("|", " ").Trim();
            var safeUrl = entry.Url.Trim();
            linesToAppend.Add($"{safeName}|{safeGroup}|{safeUrl}");
        }

        if (linesToAppend.Count > 0)
        {
            File.AppendAllLines(filePath, linesToAppend);
        }

        return linesToAppend.Count;
    }

    private static HashSet<string> LoadExistingUrls(string filePath)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(filePath))
        {
            return urls;
        }

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|');
            if (parts.Length == 0)
            {
                continue;
            }

            var url = parts[^1].Trim();
            if (!string.IsNullOrWhiteSpace(url))
            {
                urls.Add(url);
            }
        }

        return urls;
    }

    private static Dictionary<string, string> LoadStructure(string structurePath)
    {
        var structure = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in File.ReadAllLines(structurePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0 || separator == line.Length - 1)
            {
                continue;
            }

            var category = line[..separator].Trim();
            var folderName = SanitizeFolderName(line[(separator + 1)..].Trim());

            if (!string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(folderName))
            {
                structure[category] = folderName;
            }
        }

        if (structure.Count == 0)
        {
            structure["Videos"] = "Videos";
            structure["Series"] = "Series";
            structure["Outros"] = "Outros";
        }

        return structure;
    }

    private static string ResolveCategory(M3UEntry entry)
    {
        var text = $"{entry.GroupTitle} {entry.Name} {entry.Url}".ToLowerInvariant();

        if (text.Contains("serie") || text.Contains("series") || text.Contains("/series")) return "Series";
        if (text.Contains("filme") || text.Contains("movie") || text.Contains("cinema") || text.Contains("/movie")) return "Filmes";
        if (text.Contains("canal") || text.Contains("channels")) return "Canais";
        if (text.Contains("24 horas") || text.Contains("24h")) return "24 Horas";
        if (text.Contains("document")) return "Documentarios";
        if (text.Contains("novela")) return "Novelas";

        return "Videos";
    }

    private static string ResolveFileExtension(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var ext = Path.GetExtension(uri.AbsolutePath);
                if (!string.IsNullOrWhiteSpace(ext) && ext.Length <= 5)
                {
                    return ext;
                }
            }
        }
        catch
        {
        }

        return ".mp4";
    }

    private static string SanitizeFolderName(string folderName)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            folderName = folderName.Replace(c, '_');
        }

        return folderName.Trim();
    }
}
