using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace M3UVODDownloader.Services;

public interface IUpdateService
{
    Task CheckForUpdatesAsync(bool silent = false);
}

public class UpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateService> _logger;
    private const string GitHubApiUrl = "https://api.github.com/repos/wesleiandersonti/M3U_VOD_Downloader/releases/latest";
    private const string RepositoryUrl = "https://github.com/wesleiandersonti/M3U_VOD_Downloader";

    public UpdateService(HttpClient httpClient, ILogger<UpdateService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task CheckForUpdatesAsync(bool silent = false)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("M3U-VOD-Downloader");
            
            var response = await _httpClient.GetAsync(GitHubApiUrl);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            
            var root = doc.RootElement;
            var latestVersion = root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
            
            if (string.IsNullOrEmpty(latestVersion))
            {
                _logger.LogWarning("Could not parse version from GitHub response");
                if (!silent)
                {
                    await ShowMessageAsync("Update Check", "Could not determine the latest version.");
                }
                return;
            }

            var currentVersion = GetCurrentVersion();
            
            if (IsNewerVersion(latestVersion, currentVersion))
            {
                _logger.LogInformation("New version available: {Latest} (current: {Current})", latestVersion, currentVersion);
                await PromptForUpdateAsync(latestVersion, root);
            }
            else
            {
                _logger.LogInformation("Already up to date (current: {Current})", currentVersion);
                if (!silent)
                {
                    await ShowMessageAsync("Update Check", "You are running the latest version!");
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to check for updates - network error");
            if (!silent)
            {
                await ShowMessageAsync("Update Check", "Could not connect to update server. Please check your internet connection.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            if (!silent)
            {
                await ShowMessageAsync("Update Check", $"An error occurred: {ex.Message}");
            }
        }
    }

    private string GetCurrentVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    }

    private bool IsNewerVersion(string latest, string current)
    {
        if (Version.TryParse(latest, out var latestVer) && Version.TryParse(current, out var currentVer))
        {
            return latestVer > currentVer;
        }
        
        // Fallback to string comparison
        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private async Task PromptForUpdateAsync(string version, JsonElement releaseInfo)
    {
        // This would typically show a dialog, but for now we'll just log
        _logger.LogInformation("Update to version {Version} is available!", version);
        
        // Open browser to releases page
        OpenUrl($"{RepositoryUrl}/releases/tag/v{version}");
        
        await Task.CompletedTask;
    }

    private void OpenUrl(string url)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open URL: {Url}", url);
        }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        // This would typically show a MessageBox, but for async compatibility in WPF:
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, 
                System.Windows.MessageBoxImage.Information);
        });
    }
}
