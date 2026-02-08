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
