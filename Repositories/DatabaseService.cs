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
        private SqliteConnection? _connection;
        private readonly string _connectionString;
        private readonly object _lock = new();

        // Repositórios públicos
        public IEntryRepository Entries { get; }
        public IDownloadHistoryRepository Downloads { get; }
        public IFavoriteRepository Favorites { get; }

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
            }
        }

        internal SqliteConnection CreateConnection()
        {
            return new SqliteConnection(_connectionString);
        }

        public void Dispose()
        {
            _connection?.Dispose();
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
}
