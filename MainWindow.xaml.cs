using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MeuGestorVODs
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _m3uUrl = "";
        private string _downloadPath = "";
        private string _filterText = "";
        private string _statusMessage = "Pronto";
        private string _currentVersionText = "Versao atual: -";
        private bool _isLoading = false;
        private M3UEntry _selectedEntry;
        private const string DownloadStructureFileName = "estrutura_downloads.txt";
        private const string RepoApiBase = "https://api.github.com/repos/wesleiandersonti/MEU_GESTOR_DE_VODS";
        private readonly HttpClient _releaseClient = new HttpClient();
        private Dictionary<string, string> _downloadStructure = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool _isUpdateInProgress;

        public ObservableCollection<M3UEntry> Entries { get; set; } = new ObservableCollection<M3UEntry>();
        public ObservableCollection<M3UEntry> FilteredEntries { get; set; } = new ObservableCollection<M3UEntry>();
        public ObservableCollection<DownloadItem> Downloads { get; set; } = new ObservableCollection<DownloadItem>();

        public string M3UUrl
        {
            get => _m3uUrl;
            set { _m3uUrl = value; OnPropertyChanged(nameof(M3UUrl)); }
        }

        public string DownloadPath
        {
            get => _downloadPath;
            set { _downloadPath = value; OnPropertyChanged(nameof(DownloadPath)); }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                _filterText = value;
                OnPropertyChanged(nameof(FilterText));
                ApplyFilter();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        public string CurrentVersionText
        {
            get => _currentVersionText;
            set { _currentVersionText = value; OnPropertyChanged(nameof(CurrentVersionText)); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }

        public M3UEntry SelectedEntry
        {
            get => _selectedEntry;
            set { _selectedEntry = value; OnPropertyChanged(nameof(SelectedEntry)); }
        }

        private M3UService _m3uService;
        private DownloadService _downloadService;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            _m3uService = new M3UService();
            _downloadService = new DownloadService();
            DownloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Meu Gestor VODs");
            EnsureAndLoadDownloadStructure();
            _releaseClient.DefaultRequestHeaders.Add("User-Agent", "MeuGestorVODs");
            CurrentVersionText = $"Versao atual: {GetCurrentAppVersion()}";
        }

        private async void LoadM3U_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(M3UUrl))
            {
                System.Windows.MessageBox.Show("Por favor, insira a URL do arquivo M3U", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Carregando lista M3U...";
                
                var entries = await _m3uService.LoadFromUrlAsync(M3UUrl);
                
                Entries.Clear();
                foreach (var entry in entries)
                {
                    Entries.Add(entry);
                }
                
                ApplyFilter();
                StatusMessage = $"Carregados {entries.Count} itens";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao carregar M3U: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Erro ao carregar";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilter()
        {
            FilteredEntries.Clear();
            
            var filtered = string.IsNullOrWhiteSpace(FilterText)
                ? Entries
                : Entries.Where(x => x.Name.ToLower().Contains(FilterText.ToLower()));

            foreach (var entry in filtered)
            {
                FilteredEntries.Add(entry);
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var entry in FilteredEntries)
            {
                entry.IsSelected = true;
            }
            EntriesList.Items.Refresh();
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var entry in FilteredEntries)
            {
                entry.IsSelected = false;
            }
            EntriesList.Items.Refresh();
        }

        private async void DownloadSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = FilteredEntries.Where(x => x.IsSelected).ToList();
            
            if (!selected.Any())
            {
                System.Windows.MessageBox.Show("Selecione pelo menos um item para download", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(DownloadPath))
                Directory.CreateDirectory(DownloadPath);

            EnsureAndLoadDownloadStructure();
            var skippedExisting = 0;

            foreach (var entry in selected)
            {
                var outputPath = BuildOutputPath(entry);
                if (File.Exists(outputPath))
                {
                    skippedExisting++;
                    Downloads.Add(new DownloadItem
                    {
                        Name = entry.Name,
                        Progress = 100,
                        Status = "Ja existe - ignorado"
                    });
                    continue;
                }

                var downloadItem = new DownloadItem
                {
                    Name = entry.Name,
                    Progress = 0,
                    Status = "Baixando..."
                };
                Downloads.Add(downloadItem);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var progress = new Progress<double>(p =>
                        {
                            Dispatcher.Invoke(() => downloadItem.Progress = p);
                        });

                        await _downloadService.DownloadFileAsync(entry.Url, outputPath, progress);
                        
                        Dispatcher.Invoke(() =>
                        {
                            downloadItem.Progress = 100;
                            downloadItem.Status = "Concluído";
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            downloadItem.Status = $"Erro: {ex.Message}";
                        });
                    }
                });
            }

            var totalDownload = selected.Count - skippedExisting;
            StatusMessage = $"Iniciando download de {totalDownload} arquivo(s). {skippedExisting} ja existente(s).";
        }

        private void BrowsePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DownloadPath = dialog.SelectedPath;
                EnsureAndLoadDownloadStructure();
                StatusMessage = $"Estrutura de download carregada em: {Path.Combine(DownloadPath, DownloadStructureFileName)}";
            }
        }

        private void EnsureAndLoadDownloadStructure()
        {
            try
            {
                if (!Directory.Exists(DownloadPath))
                {
                    Directory.CreateDirectory(DownloadPath);
                }

                var structurePath = Path.Combine(DownloadPath, DownloadStructureFileName);
                if (!File.Exists(structurePath))
                {
                    File.WriteAllLines(structurePath, new[]
                    {
                        "# Estrutura de pastas para downloads",
                        "# Formato: Categoria=Pasta",
                        "Videos=Videos",
                        "Series=Series",
                        "Filmes=Filmes",
                        "Canais=Canais",
                        "24 Horas=24 Horas",
                        "Documentarios=Documentarios",
                        "Novelas=Novelas",
                        "Outros=Outros"
                    });
                }

                _downloadStructure = LoadStructure(structurePath);

                foreach (var folder in _downloadStructure.Values.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(Path.Combine(DownloadPath, folder));
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao preparar estrutura de pastas: {ex.Message}";
            }
        }

        private Dictionary<string, string> LoadStructure(string structurePath)
        {
            var structure = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in File.ReadAllLines(structurePath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator <= 0 || separator == line.Length - 1)
                {
                    continue;
                }

                var category = line[..separator].Trim();
                var folderName = SanitizeFolderName(line[(separator + 1)..].Trim());

                if (!string.IsNullOrWhiteSpace(category) && !string.IsNullOrWhiteSpace(folderName))
                {
                    structure[category] = folderName;
                }
            }

            if (structure.Count == 0)
            {
                structure["Videos"] = "Videos";
                structure["Series"] = "Series";
                structure["Outros"] = "Outros";
            }

            return structure;
        }

        private string BuildOutputPath(M3UEntry entry)
        {
            var category = ResolveCategory(entry);
            if (!_downloadStructure.TryGetValue(category, out var folderName))
            {
                if (!_downloadStructure.TryGetValue("Outros", out folderName))
                {
                    folderName = "Outros";
                }
            }

            var folderPath = Path.Combine(DownloadPath, folderName);
            Directory.CreateDirectory(folderPath);

            return Path.Combine(folderPath, entry.SanitizedName + ResolveFileExtension(entry.Url));
        }

        private string ResolveCategory(M3UEntry entry)
        {
            var text = $"{entry.GroupTitle} {entry.Name} {entry.Url}".ToLowerInvariant();

            if (text.Contains("serie") || text.Contains("series") || text.Contains("/series")) return "Series";
            if (text.Contains("filme") || text.Contains("movie") || text.Contains("cinema") || text.Contains("/movie")) return "Filmes";
            if (text.Contains("canal") || text.Contains("channels")) return "Canais";
            if (text.Contains("24 horas") || text.Contains("24h")) return "24 Horas";
            if (text.Contains("document")) return "Documentarios";
            if (text.Contains("novela")) return "Novelas";

            return "Videos";
        }

        private static string ResolveFileExtension(string url)
        {
            try
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var ext = Path.GetExtension(uri.AbsolutePath);
                    if (!string.IsNullOrWhiteSpace(ext) && ext.Length <= 5)
                    {
                        return ext;
                    }
                }
            }
            catch
            {
            }

            return ".mp4";
        }

        private static string SanitizeFolderName(string folderName)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                folderName = folderName.Replace(c, '_');
            }

            return folderName.Trim();
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdateInProgress)
            {
                return;
            }

            try
            {
                _isUpdateInProgress = true;
                IsLoading = true;

                var currentVersion = GetCurrentAppVersion();
                CurrentVersionText = $"Versao atual: {currentVersion}";
                StatusMessage = "Verificando atualizacoes...";

                var latest = await GetLatestReleaseAsync();
                if (latest == null)
                {
                    System.Windows.MessageBox.Show("Nao foi possivel obter informacoes da ultima versao.", "Atualizacao", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StatusMessage = "Falha ao verificar atualizacoes";
                    return;
                }

                if (!IsNewerRelease(latest.TagName, currentVersion))
                {
                    System.Windows.MessageBox.Show($"Voce ja esta na versao mais recente ({currentVersion}).", "Atualizacao", MessageBoxButton.OK, MessageBoxImage.Information);
                    StatusMessage = "Aplicativo ja esta atualizado";
                    return;
                }

                var confirm = System.Windows.MessageBox.Show(
                    $"Nova versao encontrada: {latest.TagName}.\n\nDeseja baixar e atualizar agora automaticamente?",
                    "Atualizacao disponivel",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                {
                    StatusMessage = "Atualizacao cancelada pelo usuario";
                    return;
                }

                await InstallReleaseAsync(latest, "Atualizacao");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao verificar/atualizar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Erro na atualizacao";
            }
            finally
            {
                IsLoading = false;
                _isUpdateInProgress = false;
            }
        }

        private async void RollbackVersion_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdateInProgress)
            {
                return;
            }

            try
            {
                _isUpdateInProgress = true;
                IsLoading = true;
                StatusMessage = "Buscando versoes anteriores...";

                var releases = await GetStableReleasesAsync();
                if (releases.Count == 0)
                {
                    System.Windows.MessageBox.Show("Nenhuma release encontrada.", "Rollback", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var currentVersion = GetCurrentAppVersion();
                var older = releases.Where(r => CompareReleaseTags(r.TagName, currentVersion) < 0).ToList();
                if (older.Count == 0)
                {
                    older = releases.Skip(1).ToList();
                }

                if (older.Count == 0)
                {
                    System.Windows.MessageBox.Show("Nao ha versoes anteriores disponiveis para rollback.", "Rollback", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var preview = string.Join(", ", older.Take(6).Select(x => x.TagName));
                var suggested = older.First().TagName;
                var chosenTag = PromptForText(
                    $"Versoes anteriores disponiveis (exemplos): {preview}\n\nDigite a versao desejada (ex: {suggested}):",
                    "Rollback de versao",
                    suggested);

                if (string.IsNullOrWhiteSpace(chosenTag))
                {
                    StatusMessage = "Rollback cancelado pelo usuario";
                    return;
                }

                var selected = older.FirstOrDefault(r =>
                    string.Equals(NormalizeTag(r.TagName), NormalizeTag(chosenTag), StringComparison.OrdinalIgnoreCase));

                if (selected == null)
                {
                    System.Windows.MessageBox.Show("Versao nao encontrada entre as releases disponiveis.", "Rollback", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StatusMessage = "Versao informada nao encontrada";
                    return;
                }

                var confirm = System.Windows.MessageBox.Show(
                    $"Deseja voltar para a versao {selected.TagName}?",
                    "Confirmar rollback",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                {
                    StatusMessage = "Rollback cancelado pelo usuario";
                    return;
                }

                await InstallReleaseAsync(selected, "Rollback");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro no rollback: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Erro no rollback";
            }
            finally
            {
                IsLoading = false;
                _isUpdateInProgress = false;
            }
        }

        private async Task InstallReleaseAsync(GitHubRelease release, string operation)
        {
            var installer = release.Assets.FirstOrDefault(a =>
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                a.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase));

            if (installer == null)
            {
                throw new InvalidOperationException("Nao foi encontrado instalador .exe na release selecionada.");
            }

            var downloadPath = Path.Combine(Path.GetTempPath(), "MeuGestorVODs", installer.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(downloadPath)!);

            StatusMessage = $"{operation}: baixando {release.TagName}...";
            await DownloadFileWithProgressAsync(installer.BrowserDownloadUrl, downloadPath);

            StatusMessage = $"{operation}: abrindo instalador...";
            Process.Start(new ProcessStartInfo
            {
                FileName = downloadPath,
                UseShellExecute = true
            });

            System.Windows.MessageBox.Show(
                $"Instalador da versao {release.TagName} aberto com sucesso.\n\nFinalize o assistente para concluir.",
                operation,
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            System.Windows.Application.Current.Shutdown();
        }

        private async Task DownloadFileWithProgressAsync(string url, string outputPath)
        {
            using var response = await _releaseClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var input = await response.Content.ReadAsStreamAsync();
            await using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long readTotal = 0;
            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length));
                if (read == 0)
                {
                    break;
                }

                await output.WriteAsync(buffer.AsMemory(0, read));
                readTotal += read;

                if (totalBytes > 0)
                {
                    var pct = (int)Math.Round((double)readTotal / totalBytes * 100);
                    StatusMessage = $"Baixando atualizacao... {pct}%";
                }
            }
        }

        private async Task<GitHubRelease?> GetLatestReleaseAsync()
        {
            using var response = await _releaseClient.GetAsync($"{RepoApiBase}/releases/latest");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GitHubRelease>(json);
        }

        private async Task<List<GitHubRelease>> GetStableReleasesAsync()
        {
            using var response = await _releaseClient.GetAsync($"{RepoApiBase}/releases?per_page=30");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var all = JsonSerializer.Deserialize<List<GitHubRelease>>(json) ?? new List<GitHubRelease>();
            return all.Where(r => !r.Draft && !r.Prerelease).ToList();
        }

        private static bool IsNewerRelease(string releaseTag, string currentVersion)
        {
            var releaseVersion = ParseVersion(NormalizeTag(releaseTag));
            var installedVersion = ParseVersion(NormalizeTag(currentVersion));

            if (releaseVersion != null && installedVersion != null)
            {
                return releaseVersion > installedVersion;
            }

            return !string.Equals(NormalizeTag(releaseTag), NormalizeTag(currentVersion), StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareReleaseTags(string leftTag, string rightTag)
        {
            var left = ParseVersion(NormalizeTag(leftTag));
            var right = ParseVersion(NormalizeTag(rightTag));

            if (left != null && right != null)
            {
                return left.CompareTo(right);
            }

            return string.Compare(NormalizeTag(leftTag), NormalizeTag(rightTag), StringComparison.OrdinalIgnoreCase);
        }

        private static Version? ParseVersion(string value)
        {
            var cleaned = value.Trim();
            var idx = cleaned.IndexOf('-', StringComparison.Ordinal);
            if (idx > 0)
            {
                cleaned = cleaned[..idx];
            }

            return Version.TryParse(cleaned, out var parsed) ? parsed : null;
        }

        private static string NormalizeTag(string tag)
        {
            return tag.Trim().TrimStart('v', 'V');
        }

        private string GetCurrentAppVersion()
        {
            var versionFile = Path.Combine(AppContext.BaseDirectory, "version.txt");
            if (File.Exists(versionFile))
            {
                var txt = File.ReadAllText(versionFile).Trim();
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    return txt;
                }
            }

            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
            {
                var info = FileVersionInfo.GetVersionInfo(processPath);
                if (!string.IsNullOrWhiteSpace(info.ProductVersion))
                {
                    return info.ProductVersion!;
                }
            }

            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        }

        private static string? PromptForText(string message, string title, string defaultValue)
        {
            var input = new System.Windows.Controls.TextBox
            {
                Margin = new Thickness(0, 10, 0, 10),
                Text = defaultValue,
                MinWidth = 320
            };

            var okButton = new System.Windows.Controls.Button
            {
                Content = "OK",
                Width = 90,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Cancelar",
                Width = 90,
                IsCancel = true
            };

            var buttons = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);

            var panel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(16)
            };
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(input);
            panel.Children.Add(buttons);

            var dialog = new System.Windows.Window
            {
                Title = title,
                Content = panel,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Owner = System.Windows.Application.Current?.MainWindow
            };

            okButton.Click += (_, __) => dialog.DialogResult = true;

            return dialog.ShowDialog() == true ? input.Text.Trim() : null;
        }

        private sealed class GitHubRelease
        {
            [JsonPropertyName("tag_name")]
            public string TagName { get; set; } = string.Empty;

            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("draft")]
            public bool Draft { get; set; }

            [JsonPropertyName("prerelease")]
            public bool Prerelease { get; set; }

            [JsonPropertyName("assets")]
            public List<GitHubAsset> Assets { get; set; } = new List<GitHubAsset>();
        }

        private sealed class GitHubAsset
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("browser_download_url")]
            public string BrowserDownloadUrl { get; set; } = string.Empty;
        }

        private void OpenGitHub_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS",
                UseShellExecute = true
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DownloadItem : INotifyPropertyChanged
    {
        private string _name;
        private double _progress;
        private string _status;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(nameof(Progress)); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
