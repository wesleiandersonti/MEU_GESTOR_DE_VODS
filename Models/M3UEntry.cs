using System;
using System.IO;

namespace MeuGestorVODs.Models;

public class M3UEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string GroupTitle { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public EntryType Type { get; set; }
    public bool IsSelected { get; set; }

    public string SanitizedName => SanitizeFileName(Name);
    public string FileExtension => Path.GetExtension(Url)?.ToLowerInvariant() ?? ".mp4";

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "unknown";

        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
        {
            name = name.Replace(c, '_');
        }
        return name.Trim();
    }
}

public enum EntryType
{
    Movie,
    Series,
    LiveTV,
    Unknown
}
