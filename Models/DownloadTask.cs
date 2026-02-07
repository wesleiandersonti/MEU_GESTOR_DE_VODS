using System;
using System.ComponentModel;
using System.IO;

namespace MeuGestorVODs.Models;

public class DownloadTask : INotifyPropertyChanged
{
    private double _progress;
    private string _status = "Pending";
    private long _bytesReceived;
    private long _totalBytes;
    private double _speed;

    public Guid Id { get; set; } = Guid.NewGuid();
    public M3UEntry Entry { get; set; } = null!;
    public string OutputPath { get; set; } = string.Empty;
    
    public double Progress
    {
        get => _progress;
        set
        {
            _progress = value;
            OnPropertyChanged(nameof(Progress));
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged(nameof(Status));
        }
    }

    public long BytesReceived
    {
        get => _bytesReceived;
        set
        {
            _bytesReceived = value;
            OnPropertyChanged(nameof(BytesReceived));
            OnPropertyChanged(nameof(BytesReceivedFormatted));
        }
    }

    public long TotalBytes
    {
        get => _totalBytes;
        set
        {
            _totalBytes = value;
            OnPropertyChanged(nameof(TotalBytes));
            OnPropertyChanged(nameof(TotalBytesFormatted));
        }
    }

    public double Speed
    {
        get => _speed;
        set
        {
            _speed = value;
            OnPropertyChanged(nameof(Speed));
            OnPropertyChanged(nameof(SpeedFormatted));
        }
    }

    public string BytesReceivedFormatted => FormatBytes(BytesReceived);
    public string TotalBytesFormatted => FormatBytes(TotalBytes);
    public string SpeedFormatted => $"{Speed:F2} MB/s";

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:F2} {sizes[order]}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public enum DownloadStatus
{
    Pending,
    Downloading,
    Paused,
    Completed,
    Failed,
    Cancelled
}
