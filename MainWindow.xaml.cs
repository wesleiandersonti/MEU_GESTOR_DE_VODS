using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
        private bool _isLoading = false;
        private M3UEntry _selectedEntry;
        private const string DownloadStructureFileName = "estrutura_downloads.txt";
        private Dictionary<string, string> _downloadStructure = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            try
            {
                StatusMessage = "Verificando atualizações...";
                
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "MeuGestorVODs");
                
                var response = await client.GetAsync("https://api.github.com/repos/wesleiandersonti/MEU_GESTOR_DE_VODS/releases/latest");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    
                    // Abre a página de releases no navegador
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS/releases/latest",
                        UseShellExecute = true
                    });
                    
                    StatusMessage = "Página de atualizações aberta";
                }
                else
                {
                    System.Windows.MessageBox.Show("Não foi possível verificar atualizações. Verifique sua conexão.", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StatusMessage = "Erro ao verificar atualizações";
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao verificar atualizações: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Erro ao verificar atualizações";
            }
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
