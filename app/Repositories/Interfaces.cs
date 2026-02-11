using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MeuGestorVODs.Repositories
{
    /// <summary>
    /// Interface base para repositórios - preparada para migração futura para PostgreSQL
    /// </summary>
    public interface IEntryRepository
    {
        Task<bool> ExistsByUrlAsync(string url);
        Task<int> AddAsync(M3UEntry entry);
        Task<int> AddRangeAsync(IEnumerable<M3UEntry> entries);
        Task<List<M3UEntry>> GetAllAsync();
        Task<List<M3UEntry>> GetByCategoryAsync(string category);
        Task<List<M3UEntry>> SearchAsync(string searchTerm);
        Task<int> GetCountAsync();
        Task<bool> DeleteByUrlAsync(string url);
        Task<int> GetVersionAsync();
    }

    /// <summary>
    /// Interface para histórico de downloads
    /// </summary>
    public interface IDownloadHistoryRepository
    {
        Task<int> AddAsync(DownloadHistoryEntry entry);
        Task<List<DownloadHistoryEntry>> GetByEntryIdAsync(string entryId);
        Task<List<DownloadHistoryEntry>> GetRecentAsync(int count);
    }

    /// <summary>
    /// Interface para favoritos
    /// </summary>
    public interface IFavoriteRepository
    {
        Task<bool> AddAsync(string entryUrl);
        Task<bool> RemoveAsync(string entryUrl);
        Task<bool> IsFavoriteAsync(string entryUrl);
        Task<List<string>> GetAllAsync();
    }

    /// <summary>
    /// Interface para histórico de URLs M3U
    /// </summary>
    public interface IM3uUrlRepository
    {
        Task<int> SaveOrUpdateAsync(string url, string? name = null, bool isOnline = true, int entryCount = 0);
        Task<List<M3uUrlHistory>> GetAllAsync();
        Task<List<M3uUrlHistory>> GetRecentAsync(int count);
        Task<List<M3uUrlHistory>> GetOnlineAsync();
        Task<List<M3uUrlHistory>> GetOfflineAsync();
        Task<int> DeleteOfflineAsync();
        Task<bool> ExistsAsync(string url);
        Task UpdateStatusAsync(string url, bool isOnline, int entryCount = 0);
        Task EnsureOfflineArchivedAsync(string url, string? name, string? lastError, DateTime checkedAt);
        Task<List<OfflineUrlArchiveEntry>> GetDueOfflineRetriesAsync(DateTime now);
        Task RegisterOfflineRetryFailureAsync(string url, string? lastError, DateTime checkedAt);
        Task RemoveOfflineArchiveAsync(string url);
        Task<int> DeleteExpiredOfflineUrlsAsync(DateTime now);
        Task<List<OfflineUrlArchiveEntry>> GetOfflineArchiveAsync();
        Task AddStreamCheckLogsAsync(IEnumerable<StreamCheckLogEntry> logs);
        Task AddServerScoreSnapshotsAsync(IEnumerable<ServerScoreSnapshotEntry> scores, DateTime analyzedAt);
    }

    /// <summary>
    /// Entidade de histórico de URLs M3U
    /// </summary>
    public class M3uUrlHistory
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Name { get; set; }
        public bool IsOnline { get; set; } = true;
        public DateTime LastChecked { get; set; }
        public int EntryCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class OfflineUrlArchiveEntry
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Name { get; set; }
        public DateTime FirstDetectedOfflineAt { get; set; }
        public DateTime LastCheckAt { get; set; }
        public int RetryCount { get; set; }
        public DateTime NextRetryAt { get; set; }
        public string? LastError { get; set; }
    }

    public class StreamCheckLogEntry
    {
        public string Url { get; set; } = string.Empty;
        public string NormalizedUrl { get; set; } = string.Empty;
        public string ServerHost { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double ResponseTimeMs { get; set; }
        public bool IsDuplicate { get; set; }
        public DateTime CheckedAt { get; set; }
        public string? Details { get; set; }
    }

    public class ServerScoreSnapshotEntry
    {
        public string ServerHost { get; set; } = string.Empty;
        public double Score { get; set; }
        public string Quality { get; set; } = string.Empty;
        public double SuccessRate { get; set; }
        public double AverageResponseMs { get; set; }
        public int TotalLinks { get; set; }
        public int OnlineLinks { get; set; }
        public int OfflineLinks { get; set; }
    }

    /// <summary>
    /// Entidade de histórico de download
    /// </summary>
    public class DownloadHistoryEntry
    {
        public int Id { get; set; }
        public string EntryUrl { get; set; } = string.Empty;
        public string EntryName { get; set; } = string.Empty;
        public DateTime DownloadDate { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
