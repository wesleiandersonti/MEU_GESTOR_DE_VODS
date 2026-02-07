using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MeuGestorVODs.Models;

namespace MeuGestorVODs.Services;

public interface IDownloadService
{
    Task DownloadAsync(IEnumerable<DownloadTask> tasks, IProgress<DownloadTask>? progress = null, CancellationToken cancellationToken = default);
    Task DownloadWithResumeAsync(DownloadTask task, CancellationToken cancellationToken = default);
    void CancelAll();
}

public class DownloadService : IDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DownloadService> _logger;
    private readonly AppConfig _config;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeDownloads = new();

    public DownloadService(HttpClient httpClient, ILogger<DownloadService> logger, AppConfig config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config;
    }

    public async Task DownloadAsync(IEnumerable<DownloadTask> tasks, IProgress<DownloadTask>? progress = null, CancellationToken cancellationToken = default)
    {
        var taskList = tasks.ToList();
        if (!taskList.Any())
            return;

        _logger.LogInformation("Starting download of {Count} files with max {Parallel} parallel downloads", 
            taskList.Count, _config.MaxParallelDownloads);

        using var semaphore = new SemaphoreSlim(_config.MaxParallelDownloads, _config.MaxParallelDownloads);
        var downloadTasks = taskList.Select(task => DownloadWithSemaphoreAsync(task, semaphore, progress, cancellationToken));

        await Task.WhenAll(downloadTasks);
    }

    private async Task DownloadWithSemaphoreAsync(DownloadTask task, SemaphoreSlim semaphore, IProgress<DownloadTask>? progress, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            await DownloadWithRetryAsync(task, progress, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task DownloadWithResumeAsync(DownloadTask task, CancellationToken cancellationToken = default)
    {
        var cts = new CancellationTokenSource();
        _activeDownloads[task.Id] = cts;
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

        try
        {
            task.Status = "Downloading";
            var fileInfo = new FileInfo(task.OutputPath);
            long existingSize = fileInfo.Exists ? fileInfo.Length : 0;

            using var request = new HttpRequestMessage(HttpMethod.Get, task.Entry.Url);
            
            // Add range header if resuming
            if (existingSize > 0)
            {
                request.Headers.Range = new RangeHeaderValue(existingSize, null);
                _logger.LogInformation("Resuming download from {Bytes} bytes", existingSize);
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            if (totalBytes.HasValue && existingSize > 0)
            {
                task.TotalBytes = totalBytes.Value + existingSize;
                task.BytesReceived = existingSize;
            }
            else if (totalBytes.HasValue)
            {
                task.TotalBytes = totalBytes.Value;
            }

            var fileMode = existingSize > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent
                ? FileMode.Append 
                : FileMode.Create;

            await using var contentStream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
            await using var fileStream = new FileStream(task.OutputPath, fileMode, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            var lastUpdate = DateTime.Now;
            var bytesSinceLastUpdate = 0L;

            while (true)
            {
                var read = await contentStream.ReadAsync(buffer, linkedCts.Token);
                if (read == 0) break;

                await fileStream.WriteAsync(buffer.AsMemory(0, read), linkedCts.Token);
                
                task.BytesReceived += read;
                bytesSinceLastUpdate += read;

                // Update progress every 100ms
                var now = DateTime.Now;
                if ((now - lastUpdate).TotalMilliseconds >= 100)
                {
                    var elapsedSeconds = (now - lastUpdate).TotalSeconds;
                    task.Speed = bytesSinceLastUpdate / (1024.0 * 1024.0) / elapsedSeconds;
                    task.Progress = task.TotalBytes > 0 
                        ? (double)task.BytesReceived / task.TotalBytes * 100 
                        : 0;

                    lastUpdate = now;
                    bytesSinceLastUpdate = 0;
                }
            }

            task.Progress = 100;
            task.Status = "Completed";
            _logger.LogInformation("Download completed: {File}", Path.GetFileName(task.OutputPath));
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            task.Status = "Cancelled";
            _logger.LogInformation("Download cancelled: {File}", Path.GetFileName(task.OutputPath));
        }
        catch (Exception ex)
        {
            task.Status = "Failed";
            _logger.LogError(ex, "Download failed: {File}", Path.GetFileName(task.OutputPath));
        }
        finally
        {
            _activeDownloads.TryRemove(task.Id, out _);
        }
    }

    private async Task DownloadWithRetryAsync(DownloadTask task, IProgress<DownloadTask>? progress, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                await DownloadWithResumeAsync(task, cancellationToken);
                progress?.Report(task);
                return;
            }
            catch (Exception ex) when (retryCount < maxRetries - 1)
            {
                retryCount++;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff
                _logger.LogWarning(ex, "Download attempt {Attempt} failed, retrying in {Delay}s...", 
                    retryCount, delay.TotalSeconds);
                task.Status = $"Retrying ({retryCount}/{maxRetries})...";
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public void CancelAll()
    {
        foreach (var cts in _activeDownloads.Values)
        {
            cts.Cancel();
        }
        _activeDownloads.Clear();
    }
}
