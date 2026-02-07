using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MeuGestorVODs.Models;

namespace MeuGestorVODs.Services;

public interface IPlayerService
{
    Task PlayAsync(M3UEntry entry);
    bool IsPlayerAvailable();
    string? GetPlayerPath();
}

public class PlayerService : IPlayerService
{
    private readonly ILogger<PlayerService> _logger;
    private readonly AppConfig _config;
    private string? _cachedPlayerPath;

    public PlayerService(ILogger<PlayerService> logger, AppConfig config)
    {
        _logger = logger;
        _config = config;
    }

    public Task PlayAsync(M3UEntry entry)
    {
        var playerPath = GetPlayerPath();
        if (string.IsNullOrEmpty(playerPath))
        {
            throw new InvalidOperationException("No media player found. Please install VLC or configure a player path.");
        }

        _logger.LogInformation("Playing {Name} with {Player}", entry.Name, playerPath);

        var psi = new ProcessStartInfo
        {
            FileName = playerPath,
            Arguments = $"\"{entry.Url}\"",
            UseShellExecute = true
        };

        Process.Start(psi);
        return Task.CompletedTask;
    }

    public bool IsPlayerAvailable()
    {
        return !string.IsNullOrEmpty(GetPlayerPath());
    }

    public string? GetPlayerPath()
    {
        if (!string.IsNullOrEmpty(_cachedPlayerPath) && File.Exists(_cachedPlayerPath))
        {
            return _cachedPlayerPath;
        }

        // Check configured path first
        if (!string.IsNullOrEmpty(_config.VlcPath) && File.Exists(_config.VlcPath))
        {
            _cachedPlayerPath = _config.VlcPath;
            return _cachedPlayerPath;
        }

        // Check common VLC locations on Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var possiblePaths = new[]
            {
                @"C:\Program Files\VideoLAN\VLC\vlc.exe",
                @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoLAN", "VLC", "vlc.exe")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    _cachedPlayerPath = path;
                    return path;
                }
            }

            // Try to find VLC from registry
            _cachedPlayerPath = FindVlcInRegistry();
            if (!string.IsNullOrEmpty(_cachedPlayerPath))
            {
                return _cachedPlayerPath;
            }
        }

        // On Linux/Mac, check if vlc is in PATH
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var vlcPath = FindInPath("vlc");
            if (!string.IsNullOrEmpty(vlcPath))
            {
                _cachedPlayerPath = vlcPath;
                return vlcPath;
            }

            // Try mpv as alternative
            var mpvPath = FindInPath("mpv");
            if (!string.IsNullOrEmpty(mpvPath))
            {
                _cachedPlayerPath = mpvPath;
                return mpvPath;
            }
        }

        return null;
    }

    private string? FindVlcInRegistry()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\VideoLAN\VLC");
                if (key != null)
                {
                    var installDir = key.GetValue("InstallDir") as string;
                    if (!string.IsNullOrEmpty(installDir))
                    {
                        var vlcPath = Path.Combine(installDir, "vlc.exe");
                        if (File.Exists(vlcPath))
                        {
                            return vlcPath;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read VLC registry key");
        }

        return null;
    }

    private string? FindInPath(string executable)
    {
        try
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
            var paths = pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var path in paths)
            {
                var fullPath = Path.Combine(path, executable);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    fullPath += ".exe";
                }

                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to search PATH for {Executable}", executable);
        }

        return null;
    }
}
