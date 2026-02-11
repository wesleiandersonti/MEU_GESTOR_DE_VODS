using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MeuGestorVODs.Repositories
{
    /// <summary>
    /// Serviço de migração para converter arquivos TXT legados para SQLite
    /// </summary>
    public class MigrationService
    {
        private readonly DatabaseService _databaseService;

        public MigrationService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Migra dados dos arquivos TXT para o SQLite
        /// Retorna estatísticas da migração
        /// </summary>
        public async Task<MigrationResult> MigrateFromTxtFilesAsync(
            string vodFilePath, 
            string liveFilePath,
            IProgress<string>? progress = null)
        {
            var result = new MigrationResult();

            progress?.Report("Iniciando migração dos arquivos TXT...");

            // Migrar VODs
            if (File.Exists(vodFilePath))
            {
                progress?.Report("Migrando banco VOD...");
                var vodEntries = await ParseM3uFileAsync(vodFilePath);
                var vodCount = await _databaseService.Entries.AddRangeAsync(vodEntries);
                result.VodMigrated = vodCount;
                progress?.Report($"{vodCount} entradas VOD migradas");
            }

            // Migrar Canais ao Vivo
            if (File.Exists(liveFilePath))
            {
                progress?.Report("Migrando banco de canais ao vivo...");
                var liveEntries = await ParseM3uFileAsync(liveFilePath);
                var liveCount = await _databaseService.Entries.AddRangeAsync(liveEntries);
                result.LiveMigrated = liveCount;
                progress?.Report($"{liveCount} canais ao vivo migrados");
            }

            result.Success = true;
            
            progress?.Report($"Migração concluída! Total: {result.TotalMigrated} entradas");

            return result;
        }

        /// <summary>
        /// Parse de arquivo M3U ou formato legado (pipe-delimited)
        /// </summary>
        private async Task<List<M3UEntry>> ParseM3uFileAsync(string filePath)
        {
            var entries = new List<M3UEntry>();
            var lines = await File.ReadAllLinesAsync(filePath);

            if (lines.Length == 0)
                return entries;

            // Detectar formato pelo cabeçalho
            var isM3uFormat = lines[0].Trim().StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase);

            if (isM3uFormat)
            {
                entries = ParseM3uFormat(lines);
            }
            else
            {
                entries = ParseLegacyFormat(lines);
            }

            return entries;
        }

        /// <summary>
        /// Parse do formato M3U padrão
        /// </summary>
        private List<M3UEntry> ParseM3uFormat(string[] lines)
        {
            var entries = new List<M3UEntry>();

            for (int i = 0; i < lines.Length - 1; i++)
            {
                var line = lines[i].Trim();
                
                if (!line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var nextLine = lines[i + 1].Trim();
                if (string.IsNullOrWhiteSpace(nextLine) || nextLine.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var entry = new M3UEntry
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Url = nextLine,
                    TvgId = ExtractAttribute(line, "tvg-id") ?? string.Empty,
                    Name = ExtractAttribute(line, "tvg-name") ?? ExtractName(line) ?? "Unknown",
                    GroupTitle = ExtractAttribute(line, "group-title") ?? string.Empty,
                    LogoUrl = ExtractAttribute(line, "tvg-logo") ?? string.Empty
                };

                // Parse do group-title para Category/SubCategory
                var (category, subCategory) = ParseGroupTitle(entry.GroupTitle);
                entry.Category = category;
                entry.SubCategory = subCategory;

                entries.Add(entry);
            }

            return entries;
        }

        /// <summary>
        /// Parse do formato legado (pipe-delimited)
        /// </summary>
        private List<M3UEntry> ParseLegacyFormat(string[] lines)
        {
            var entries = new List<M3UEntry>();

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                
                // Ignorar linhas vazias ou comentários
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                // Formato legado: Nome|Grupo|URL
                var parts = line.Split('|');
                if (parts.Length < 3)
                    continue;

                var name = parts[0].Trim();
                var groupTitle = parts[1].Trim();
                var url = parts[2].Trim();

                if (string.IsNullOrWhiteSpace(url))
                    continue;

                var (category, subCategory) = ParseGroupTitle(groupTitle);

                entries.Add(new M3UEntry
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Name = name,
                    Url = url,
                    GroupTitle = groupTitle,
                    Category = category,
                    SubCategory = subCategory
                });
            }

            return entries;
        }

        /// <summary>
        /// Extrai atributo do formato #EXTINF
        /// </summary>
        private string? ExtractAttribute(string line, string attribute)
        {
            var pattern = $"{attribute}=\"([^\"]*)\"";
            var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Extrai o nome após a vírgula no #EXTINF
        /// </summary>
        private string? ExtractName(string line)
        {
            var lastComma = line.LastIndexOf(',');
            if (lastComma >= 0 && lastComma < line.Length - 1)
            {
                return line[(lastComma + 1)..].Trim();
            }
            return null;
        }

        /// <summary>
        /// Parse do group-title para Category e SubCategory
        /// </summary>
        private (string category, string subCategory) ParseGroupTitle(string? groupTitle)
        {
            var value = (groupTitle ?? string.Empty).Trim();
            
            if (string.IsNullOrWhiteSpace(value))
                return ("Sem Categoria", "Geral");

            // Separadores comuns: | ou >
            var separator = value.Contains('|') ? '|' : value.Contains('>') ? '>' : '\0';
            
            if (separator != '\0')
            {
                var parts = value.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(p => p.Trim())
                                 .Where(p => !string.IsNullOrWhiteSpace(p))
                                 .ToArray();
                
                if (parts.Length >= 2)
                {
                    return (parts[0], string.Join(" | ", parts.Skip(1)));
                }
            }

            return (value, "Geral");
        }

        /// <summary>
        /// Verifica se existe dados para migrar
        /// </summary>
        public bool HasDataToMigrate(string vodFilePath, string liveFilePath)
        {
            var vodExists = File.Exists(vodFilePath) && File.ReadAllLines(vodFilePath).Length > 2;
            var liveExists = File.Exists(liveFilePath) && File.ReadAllLines(liveFilePath).Length > 2;
            return vodExists || liveExists;
        }
    }

    /// <summary>
    /// Resultado da migração
    /// </summary>
    public class MigrationResult
    {
        public bool Success { get; set; }
        public int VodMigrated { get; set; }
        public int LiveMigrated { get; set; }
        public int TotalMigrated => VodMigrated + LiveMigrated;
        public string? ErrorMessage { get; set; }
    }
}
