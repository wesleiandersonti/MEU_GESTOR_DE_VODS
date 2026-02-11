using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace MeuGestorVODs.Repositories
{
    /// <summary>
    /// Serviço de banco de dados SQLite - Implementa Repository Pattern
    /// Facilita migração futura para PostgreSQL
    /// </summary>
    public class DatabaseService : IDisposable
    {
        private readonly string _databasePath;
        private readonly string _connectionString;
        private readonly object _lock = new();

        // Repositórios públicos
        public IEntryRepository Entries { get; }
        public IDownloadHistoryRepository Downloads { get; }
        public IFavoriteRepository Favorites { get; }
        public IM3uUrlRepository M3uUrls { get; }

        public DatabaseService(string databaseDirectory)
        {
            if (!Directory.Exists(databaseDirectory))
            {
                Directory.CreateDirectory(databaseDirectory);
            }

            _databasePath = Path.Combine(databaseDirectory, "database.sqlite");
            _connectionString = $"Data Source={_databasePath};Cache=Shared";
            
            // Inicializar repositórios
            Entries = new SqliteEntryRepository(this);
            Downloads = new SqliteDownloadHistoryRepository(this);
            Favorites = new SqliteFavoriteRepository(this);
            M3uUrls = new SqliteM3uUrlRepository(this);

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            lock (_lock)
            {
                using var connection = CreateConnection();
                connection.Open();

                // Tabela de entradas (canais/VODs)
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS Entries (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        EntryId TEXT UNIQUE NOT NULL,
                        Name TEXT NOT NULL,
                        Url TEXT UNIQUE NOT NULL,
                        GroupTitle TEXT,
                        Category TEXT,
                        SubCategory TEXT,
                        LogoUrl TEXT,
                        TvgId TEXT,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE INDEX IF NOT EXISTS idx_entries_url ON Entries(Url);
                    CREATE INDEX IF NOT EXISTS idx_entries_category ON Entries(Category);
                    CREATE INDEX IF NOT EXISTS idx_entries_name ON Entries(Name);
                ");

                // Tabela de histórico de downloads
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS DownloadHistory (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        EntryUrl TEXT NOT NULL,
                        EntryName TEXT NOT NULL,
                        DownloadDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FilePath TEXT,
                        FileSize INTEGER DEFAULT 0,
                        Success BOOLEAN DEFAULT 1,
                        ErrorMessage TEXT,
                        FOREIGN KEY (EntryUrl) REFERENCES Entries(Url)
                    );

                    CREATE INDEX IF NOT EXISTS idx_downloads_url ON DownloadHistory(EntryUrl);
                    CREATE INDEX IF NOT EXISTS idx_downloads_date ON DownloadHistory(DownloadDate);
                ");

                // Tabela de favoritos
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS Favorites (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        EntryUrl TEXT UNIQUE NOT NULL,
                        AddedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (EntryUrl) REFERENCES Entries(Url) ON DELETE CASCADE
                    );

                    CREATE INDEX IF NOT EXISTS idx_favorites_url ON Favorites(EntryUrl);
                ");

                // Tabela de versão do schema (para migrações futuras)
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS SchemaVersion (
                        Id INTEGER PRIMARY KEY CHECK (Id = 1),
                        Version INTEGER NOT NULL DEFAULT 1,
                        UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    );

                    INSERT OR IGNORE INTO SchemaVersion (Id, Version) VALUES (1, 1);
                ");

                // Tabela de histórico de URLs M3U
                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS M3uUrlHistory (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Url TEXT UNIQUE NOT NULL,
                        Name TEXT,
                        IsOnline BOOLEAN DEFAULT 1,
                        LastChecked DATETIME DEFAULT CURRENT_TIMESTAMP,
                        EntryCount INTEGER DEFAULT 0,
                        SuccessCount INTEGER DEFAULT 0,
                        FailCount INTEGER DEFAULT 0,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE INDEX IF NOT EXISTS idx_m3uurl_url ON M3uUrlHistory(Url);
                    CREATE INDEX IF NOT EXISTS idx_m3uurl_online ON M3uUrlHistory(IsOnline);
                    CREATE INDEX IF NOT EXISTS idx_m3uurl_checked ON M3uUrlHistory(LastChecked);
                ");

                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS OfflineUrlArchive (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Url TEXT UNIQUE NOT NULL,
                        Name TEXT,
                        FirstDetectedOfflineAt DATETIME NOT NULL,
                        LastCheckAt DATETIME NOT NULL,
                        RetryCount INTEGER NOT NULL DEFAULT 0,
                        NextRetryAt DATETIME NOT NULL,
                        LastError TEXT
                    );

                    CREATE INDEX IF NOT EXISTS idx_offlinearchive_url ON OfflineUrlArchive(Url);
                    CREATE INDEX IF NOT EXISTS idx_offlinearchive_nextretry ON OfflineUrlArchive(NextRetryAt);
                ");

                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS StreamCheckLog (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Url TEXT NOT NULL,
                        NormalizedUrl TEXT,
                        ServerHost TEXT,
                        Status TEXT NOT NULL,
                        ResponseTimeMs REAL,
                        IsDuplicate BOOLEAN DEFAULT 0,
                        CheckedAt DATETIME NOT NULL,
                        Details TEXT
                    );

                    CREATE INDEX IF NOT EXISTS idx_streamchecklog_checkedat ON StreamCheckLog(CheckedAt);
                    CREATE INDEX IF NOT EXISTS idx_streamchecklog_serverhost ON StreamCheckLog(ServerHost);
                    CREATE INDEX IF NOT EXISTS idx_streamchecklog_status ON StreamCheckLog(Status);
                ");

                connection.Execute(@"
                    CREATE TABLE IF NOT EXISTS ServerScoreSnapshot (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ServerHost TEXT NOT NULL,
                        Score REAL NOT NULL,
                        Quality TEXT NOT NULL,
                        SuccessRate REAL NOT NULL,
                        AverageResponseMs REAL NOT NULL,
                        TotalLinks INTEGER NOT NULL,
                        OnlineLinks INTEGER NOT NULL,
                        OfflineLinks INTEGER NOT NULL,
                        AnalyzedAt DATETIME NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS idx_serverscore_analyzedat ON ServerScoreSnapshot(AnalyzedAt);
                    CREATE INDEX IF NOT EXISTS idx_serverscore_serverhost ON ServerScoreSnapshot(ServerHost);
                ");
            }
        }

        internal SqliteConnection CreateConnection()
        {
            return new SqliteConnection(_connectionString);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Implementação SQLite do repositório de entradas
    /// </summary>
    public class SqliteEntryRepository : IEntryRepository
    {
        private readonly DatabaseService _db;

        public SqliteEntryRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task<bool> ExistsByUrlAsync(string url)
        {
            using var connection = _db.CreateConnection();
            var count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Entries WHERE Url = @Url",
                new { Url = url });
            return count > 0;
        }

        public async Task<int> AddAsync(M3UEntry entry)
        {
            using var connection = _db.CreateConnection();
            return await connection.ExecuteAsync(@"
                INSERT OR IGNORE INTO Entries 
                (EntryId, Name, Url, GroupTitle, Category, SubCategory, LogoUrl, TvgId)
                VALUES 
                (@Id, @Name, @Url, @GroupTitle, @Category, @SubCategory, @LogoUrl, @TvgId)
            ", entry);
        }

        public async Task<int> AddRangeAsync(IEnumerable<M3UEntry> entries)
        {
            using var connection = _db.CreateConnection();
            connection.Open();
            
            using var transaction = connection.BeginTransaction();
            try
            {
                var count = await connection.ExecuteAsync(@"
                    INSERT OR IGNORE INTO Entries 
                    (EntryId, Name, Url, GroupTitle, Category, SubCategory, LogoUrl, TvgId)
                    VALUES 
                    (@Id, @Name, @Url, @GroupTitle, @Category, @SubCategory, @LogoUrl, @TvgId)
                ", entries, transaction);
                
                transaction.Commit();
                return count;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<List<M3UEntry>> GetAllAsync()
        {
            using var connection = _db.CreateConnection();
            var entries = await connection.QueryAsync<EntryDto>(@"
                SELECT EntryId as Id, Name, Url, GroupTitle, Category, SubCategory, LogoUrl, TvgId
                FROM Entries
                ORDER BY Category, Name
            ");
            
            return entries.Select(MapToM3UEntry).ToList();
        }

        public async Task<List<M3UEntry>> GetByCategoryAsync(string category)
        {
            using var connection = _db.CreateConnection();
            var entries = await connection.QueryAsync<EntryDto>(@"
                SELECT EntryId as Id, Name, Url, GroupTitle, Category, SubCategory, LogoUrl, TvgId
                FROM Entries
                WHERE Category = @Category
                ORDER BY Name
            ", new { Category = category });
            
            return entries.Select(MapToM3UEntry).ToList();
        }

        public async Task<List<M3UEntry>> SearchAsync(string searchTerm)
        {
            using var connection = _db.CreateConnection();
            var pattern = $"%{searchTerm}%";
            var entries = await connection.QueryAsync<EntryDto>(@"
                SELECT EntryId as Id, Name, Url, GroupTitle, Category, SubCategory, LogoUrl, TvgId
                FROM Entries
                WHERE Name LIKE @Pattern OR GroupTitle LIKE @Pattern OR Category LIKE @Pattern
                ORDER BY Name
            ", new { Pattern = pattern });
            
            return entries.Select(MapToM3UEntry).ToList();
        }

        public async Task<int> GetCountAsync()
        {
            using var connection = _db.CreateConnection();
            return await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Entries");
        }

        public async Task<bool> DeleteByUrlAsync(string url)
        {
            using var connection = _db.CreateConnection();
            var rows = await connection.ExecuteAsync(
                "DELETE FROM Entries WHERE Url = @Url",
                new { Url = url });
            return rows > 0;
        }

        public async Task<int> GetVersionAsync()
        {
            using var connection = _db.CreateConnection();
            return await connection.ExecuteScalarAsync<int>(
                "SELECT Version FROM SchemaVersion WHERE Id = 1");
        }

        private M3UEntry MapToM3UEntry(EntryDto dto)
        {
            return new M3UEntry
            {
                Id = dto.Id,
                Name = dto.Name,
                Url = dto.Url,
                GroupTitle = dto.GroupTitle,
                Category = dto.Category,
                SubCategory = dto.SubCategory,
                LogoUrl = dto.LogoUrl,
                TvgId = dto.TvgId
            };
        }
    }

    /// <summary>
    /// DTO interno para mapeamento
    /// </summary>
    internal class EntryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string GroupTitle { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public string TvgId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Implementação SQLite do repositório de histórico de downloads
    /// </summary>
    public class SqliteDownloadHistoryRepository : IDownloadHistoryRepository
    {
        private readonly DatabaseService _db;

        public SqliteDownloadHistoryRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task<int> AddAsync(DownloadHistoryEntry entry)
        {
            using var connection = _db.CreateConnection();
            return await connection.ExecuteAsync(@"
                INSERT INTO DownloadHistory 
                (EntryUrl, EntryName, FilePath, FileSize, Success, ErrorMessage)
                VALUES 
                (@EntryUrl, @EntryName, @FilePath, @FileSize, @Success, @ErrorMessage)
            ", entry);
        }

        public async Task<List<DownloadHistoryEntry>> GetByEntryIdAsync(string entryUrl)
        {
            using var connection = _db.CreateConnection();
            return (await connection.QueryAsync<DownloadHistoryEntry>(@"
                SELECT * FROM DownloadHistory
                WHERE EntryUrl = @EntryUrl
                ORDER BY DownloadDate DESC
            ", new { EntryUrl = entryUrl })).ToList();
        }

        public async Task<List<DownloadHistoryEntry>> GetRecentAsync(int count)
        {
            using var connection = _db.CreateConnection();
            return (await connection.QueryAsync<DownloadHistoryEntry>(@"
                SELECT * FROM DownloadHistory
                ORDER BY DownloadDate DESC
                LIMIT @Count
            ", new { Count = count })).ToList();
        }
    }

    /// <summary>
    /// Implementação SQLite do repositório de favoritos
    /// </summary>
    public class SqliteFavoriteRepository : IFavoriteRepository
    {
        private readonly DatabaseService _db;

        public SqliteFavoriteRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task<bool> AddAsync(string entryUrl)
        {
            using var connection = _db.CreateConnection();
            try
            {
                await connection.ExecuteAsync(@"
                    INSERT OR IGNORE INTO Favorites (EntryUrl)
                    VALUES (@EntryUrl)
                ", new { EntryUrl = entryUrl });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> RemoveAsync(string entryUrl)
        {
            using var connection = _db.CreateConnection();
            var rows = await connection.ExecuteAsync(
                "DELETE FROM Favorites WHERE EntryUrl = @EntryUrl",
                new { EntryUrl = entryUrl });
            return rows > 0;
        }

        public async Task<bool> IsFavoriteAsync(string entryUrl)
        {
            using var connection = _db.CreateConnection();
            var count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Favorites WHERE EntryUrl = @EntryUrl",
                new { EntryUrl = entryUrl });
            return count > 0;
        }

        public async Task<List<string>> GetAllAsync()
        {
            using var connection = _db.CreateConnection();
            return (await connection.QueryAsync<string>(@"
                SELECT EntryUrl FROM Favorites
                ORDER BY AddedAt DESC
            ")).ToList();
        }
    }

    /// <summary>
    /// Implementação SQLite do repositório de URLs M3U
    /// </summary>
    public class SqliteM3uUrlRepository : IM3uUrlRepository
    {
        private readonly DatabaseService _db;

        public SqliteM3uUrlRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task<int> SaveOrUpdateAsync(string url, string? name = null, bool isOnline = true, int entryCount = 0)
        {
            using var connection = _db.CreateConnection();
            
            // Verificar se já existe
            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM M3uUrlHistory WHERE Url = @Url",
                new { Url = url });

            if (exists > 0)
            {
                // Atualizar
                return await connection.ExecuteAsync(@"
                    UPDATE M3uUrlHistory 
                    SET Name = @Name,
                        IsOnline = @IsOnline,
                        LastChecked = CURRENT_TIMESTAMP,
                        EntryCount = @EntryCount,
                        SuccessCount = SuccessCount + CASE WHEN @IsOnline = 1 THEN 1 ELSE 0 END,
                        FailCount = FailCount + CASE WHEN @IsOnline = 0 THEN 1 ELSE 0 END
                    WHERE Url = @Url
                ", new { Url = url, Name = name, IsOnline = isOnline, EntryCount = entryCount });
            }
            else
            {
                // Inserir novo
                return await connection.ExecuteAsync(@"
                    INSERT INTO M3uUrlHistory (Url, Name, IsOnline, EntryCount, SuccessCount, FailCount)
                    VALUES (@Url, @Name, @IsOnline, @EntryCount, 
                            CASE WHEN @IsOnline = 1 THEN 1 ELSE 0 END,
                            CASE WHEN @IsOnline = 0 THEN 1 ELSE 0 END)
                ", new { Url = url, Name = name, IsOnline = isOnline, EntryCount = entryCount });
            }
        }

        public async Task<List<M3uUrlHistory>> GetAllAsync()
        {
            using var connection = _db.CreateConnection();
            return (await connection.QueryAsync<M3uUrlHistory>(@"
                SELECT * FROM M3uUrlHistory
                ORDER BY LastChecked DESC
            ")).ToList();
        }

        public async Task<List<M3uUrlHistory>> GetRecentAsync(int count)
        {
            using var connection = _db.CreateConnection();
            return (await connection.QueryAsync<M3uUrlHistory>(@"
                SELECT * FROM M3uUrlHistory
                ORDER BY LastChecked DESC
                LIMIT @Count
            ", new { Count = count })).ToList();
        }

        public async Task<List<M3uUrlHistory>> GetOnlineAsync()
        {
            using var connection = _db.CreateConnection();
            return (await connection.QueryAsync<M3uUrlHistory>(@"
                SELECT * FROM M3uUrlHistory
                WHERE IsOnline = 1
                ORDER BY LastChecked DESC
            ")).ToList();
        }

        public async Task<List<M3uUrlHistory>> GetOfflineAsync()
        {
            using var connection = _db.CreateConnection();
            return (await connection.QueryAsync<M3uUrlHistory>(@"
                SELECT * FROM M3uUrlHistory
                WHERE IsOnline = 0
                ORDER BY LastChecked DESC
            ")).ToList();
        }

        public async Task<int> DeleteOfflineAsync()
        {
            using var connection = _db.CreateConnection();
            return await connection.ExecuteAsync(@"
                DELETE FROM M3uUrlHistory WHERE IsOnline = 0
            ");
        }

        public async Task<bool> ExistsAsync(string url)
        {
            using var connection = _db.CreateConnection();
            var count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM M3uUrlHistory WHERE Url = @Url",
                new { Url = url });
            return count > 0;
        }

        public async Task UpdateStatusAsync(string url, bool isOnline, int entryCount = 0)
        {
            using var connection = _db.CreateConnection();
            await connection.ExecuteAsync(@"
                UPDATE M3uUrlHistory 
                SET IsOnline = @IsOnline,
                    LastChecked = CURRENT_TIMESTAMP,
                    EntryCount = CASE WHEN @EntryCount > 0 THEN @EntryCount ELSE EntryCount END,
                    SuccessCount = SuccessCount + CASE WHEN @IsOnline = 1 THEN 1 ELSE 0 END,
                    FailCount = FailCount + CASE WHEN @IsOnline = 0 THEN 1 ELSE 0 END
                WHERE Url = @Url
            ", new { Url = url, IsOnline = isOnline, EntryCount = entryCount });
        }

        public async Task EnsureOfflineArchivedAsync(string url, string? name, string? lastError, DateTime checkedAt)
        {
            using var connection = _db.CreateConnection();
            var updated = await connection.ExecuteAsync(@"
                UPDATE OfflineUrlArchive
                SET Name = COALESCE(@Name, Name),
                    LastCheckAt = @CheckedAt,
                    LastError = @LastError
                WHERE Url = @Url
            ", new { Url = url, Name = name, CheckedAt = checkedAt, LastError = lastError });

            if (updated == 0)
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO OfflineUrlArchive
                    (Url, Name, FirstDetectedOfflineAt, LastCheckAt, RetryCount, NextRetryAt, LastError)
                    VALUES
                    (@Url, @Name, @CheckedAt, @CheckedAt, 0, @NextRetryAt, @LastError)
                ", new
                {
                    Url = url,
                    Name = name,
                    CheckedAt = checkedAt,
                    NextRetryAt = checkedAt.AddDays(1),
                    LastError = lastError
                });
            }
        }

        public async Task<List<OfflineUrlArchiveEntry>> GetDueOfflineRetriesAsync(DateTime now)
        {
            using var connection = _db.CreateConnection();
            return (await connection.QueryAsync<OfflineUrlArchiveEntry>(@"
                SELECT * FROM OfflineUrlArchive
                WHERE RetryCount < 2
                  AND NextRetryAt <= @Now
                ORDER BY NextRetryAt ASC
            ", new { Now = now })).ToList();
        }

        public async Task RegisterOfflineRetryFailureAsync(string url, string? lastError, DateTime checkedAt)
        {
            using var connection = _db.CreateConnection();
            await connection.ExecuteAsync(@"
                UPDATE OfflineUrlArchive
                SET RetryCount = RetryCount + 1,
                    LastCheckAt = @CheckedAt,
                    NextRetryAt = CASE
                        WHEN RetryCount + 1 >= 2 THEN DATETIME(FirstDetectedOfflineAt, '+2 days')
                        ELSE @NextRetryAt
                    END,
                    LastError = @LastError
                WHERE Url = @Url
            ", new
            {
                Url = url,
                CheckedAt = checkedAt,
                NextRetryAt = checkedAt.AddDays(1),
                LastError = lastError
            });
        }

        public async Task RemoveOfflineArchiveAsync(string url)
        {
            using var connection = _db.CreateConnection();
            await connection.ExecuteAsync(
                "DELETE FROM OfflineUrlArchive WHERE Url = @Url",
                new { Url = url });
        }

        public async Task<int> DeleteExpiredOfflineUrlsAsync(DateTime now)
        {
            using var connection = _db.CreateConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                var urls = (await connection.QueryAsync<string>(@"
                    SELECT Url FROM OfflineUrlArchive
                    WHERE RetryCount >= 2
                      AND DATETIME(FirstDetectedOfflineAt, '+2 days') <= @Now
                ", new { Now = now }, transaction)).ToList();

                if (urls.Count == 0)
                {
                    transaction.Commit();
                    return 0;
                }

                var deletedHistory = await connection.ExecuteAsync(@"
                    DELETE FROM M3uUrlHistory
                    WHERE Url IN @Urls
                ", new { Urls = urls }, transaction);

                await connection.ExecuteAsync(@"
                    DELETE FROM OfflineUrlArchive
                    WHERE Url IN @Urls
                ", new { Urls = urls }, transaction);

                transaction.Commit();
                return deletedHistory;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<List<OfflineUrlArchiveEntry>> GetOfflineArchiveAsync()
        {
            using var connection = _db.CreateConnection();
            return (await connection.QueryAsync<OfflineUrlArchiveEntry>(@"
                SELECT * FROM OfflineUrlArchive
                ORDER BY LastCheckAt DESC
            ")).ToList();
        }

        public async Task AddStreamCheckLogsAsync(IEnumerable<StreamCheckLogEntry> logs)
        {
            var items = logs?.ToList() ?? new List<StreamCheckLogEntry>();
            if (items.Count == 0)
            {
                return;
            }

            using var connection = _db.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            await connection.ExecuteAsync(@"
                INSERT INTO StreamCheckLog
                (Url, NormalizedUrl, ServerHost, Status, ResponseTimeMs, IsDuplicate, CheckedAt, Details)
                VALUES
                (@Url, @NormalizedUrl, @ServerHost, @Status, @ResponseTimeMs, @IsDuplicate, @CheckedAt, @Details)
            ", items, transaction);

            transaction.Commit();
        }

        public async Task AddServerScoreSnapshotsAsync(IEnumerable<ServerScoreSnapshotEntry> scores, DateTime analyzedAt)
        {
            var items = scores?.ToList() ?? new List<ServerScoreSnapshotEntry>();
            if (items.Count == 0)
            {
                return;
            }

            using var connection = _db.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            await connection.ExecuteAsync(@"
                INSERT INTO ServerScoreSnapshot
                (ServerHost, Score, Quality, SuccessRate, AverageResponseMs, TotalLinks, OnlineLinks, OfflineLinks, AnalyzedAt)
                VALUES
                (@ServerHost, @Score, @Quality, @SuccessRate, @AverageResponseMs, @TotalLinks, @OnlineLinks, @OfflineLinks, @AnalyzedAt)
            ", items.Select(x => new
            {
                x.ServerHost,
                x.Score,
                x.Quality,
                x.SuccessRate,
                x.AverageResponseMs,
                x.TotalLinks,
                x.OnlineLinks,
                x.OfflineLinks,
                AnalyzedAt = analyzedAt
            }), transaction);

            transaction.Commit();
        }
    }
}
