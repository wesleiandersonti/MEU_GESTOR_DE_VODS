using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeuGestorVODs.Models;
using MeuGestorVODs.Services;

namespace MeuGestorVODs.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IM3UService _m3uService;
    private readonly IDownloadService _downloadService;
    private readonly IPlayerService _playerService;
    private readonly IUpdateService _updateService;
    private readonly AppConfig _config;

    [ObservableProperty]
    private string _m3uUrl = string.Empty;

    [ObservableProperty]
    private string _downloadPath = string.Empty;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _maxParallelDownloads = 3;

    public ObservableCollection<M3UEntry> Entries { get; } = new();
    public ObservableCollection<DownloadTask> DownloadTasks { get; } = new();

    private List<M3UEntry> _allEntries = new();
    private CancellationTokenSource? _downloadCts;

    public MainViewModel(
        IM3UService m3uService,
        IDownloadService downloadService,
        IPlayerService playerService,
        IUpdateService updateService,
        AppConfig config)
    {
        _m3uService = m3uService;
        _downloadService = downloadService;
        _playerService = playerService;
        _updateService = updateService;
        _config = config;

        // Load settings
        M3uUrl = config.M3UUrl;
        DownloadPath = config.DownloadPath;
        MaxParallelDownloads = config.MaxParallelDownloads;

        // Check for updates on startup
        _ = CheckForUpdatesAsync();
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Entries.Clear();
        
        var filtered = string.IsNullOrWhiteSpace(FilterText)
            ? _allEntries
            : _allEntries.Where(e => e.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase));

        foreach (var entry in filtered)
        {
            Entries.Add(entry);
        }
    }

    [RelayCommand]
    private async Task LoadM3UAsync()
    {
        if (string.IsNullOrWhiteSpace(M3uUrl))
        {
            StatusMessage = "Please enter a valid M3U URL";
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Loading M3U...";

            _allEntries = (await _m3uService.LoadM3UAsync(M3uUrl)).ToList();
            
            ApplyFilter();
            
            StatusMessage = $"Loaded {_allEntries.Count} VOD entries";
            
            // Save URL to config
            _config.M3UUrl = M3uUrl;
            _config.Save();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DownloadSelectedAsync()
    {
        var selectedEntries = _allEntries.Where(e => e.IsSelected).ToList();
        
        if (!selectedEntries.Any())
        {
            StatusMessage = "Please select at least one item to download";
            return;
        }

        if (!Directory.Exists(DownloadPath))
        {
            try
            {
                Directory.CreateDirectory(DownloadPath);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Cannot create download directory: {ex.Message}";
                return;
            }
        }

        // Create download tasks
        var tasks = selectedEntries.Select(entry => new DownloadTask
        {
            Entry = entry,
            OutputPath = Path.Combine(
                DownloadPath,
                entry.SanitizedName + entry.FileExtension)
        }).ToList();

        // Add to observable collection
        DownloadTasks.Clear();
        foreach (var task in tasks)
        {
            DownloadTasks.Add(task);
        }

        _downloadCts = new CancellationTokenSource();
        StatusMessage = $"Downloading {tasks.Count} files...";

        try
        {
            var progress = new Progress<DownloadTask>(task =>
            {
                // Progress updates are handled via binding
            });

            await _downloadService.DownloadAsync(tasks, progress, _downloadCts.Token);
            
            var completed = tasks.Count(t => t.Status == "Completed");
            var failed = tasks.Count(t => t.Status == "Failed");
            StatusMessage = $"Download complete. Success: {completed}, Failed: {failed}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Download cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Download error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelDownload()
    {
        _downloadCts?.Cancel();
        _downloadService.CancelAll();
    }

    [RelayCommand]
    private async Task PlaySelectedAsync()
    {
        var selected = _allEntries.FirstOrDefault(e => e.IsSelected);
        if (selected == null)
        {
            StatusMessage = "Please select an item to play";
            return;
        }

        try
        {
            await _playerService.PlayAsync(selected);
            StatusMessage = $"Playing: {selected.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cannot play: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var entry in Entries)
        {
            entry.IsSelected = true;
        }
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var entry in Entries)
        {
            entry.IsSelected = false;
        }
    }

    [RelayCommand]
    private async Task BrowseDownloadPathAsync()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select download folder",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            DownloadPath = dialog.SelectedPath;
            _config.DownloadPath = DownloadPath;
            _config.Save();
        }
        
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        await _updateService.CheckForUpdatesAsync(silent: true);
    }

    [RelayCommand]
    private void OpenGitHub()
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://github.com/wesleiandersonti/M3U_VOD_Downloader",
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(psi);
    }

    partial void OnMaxParallelDownloadsChanged(int value)
    {
        _config.MaxParallelDownloads = value;
        _config.Save();
    }
}
