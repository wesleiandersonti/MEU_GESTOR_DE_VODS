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
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MeuGestorVODs.Repositories;

namespace MeuGestorVODs
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _m3uUrl = "";
        private string _downloadPath = "";
        private string _filterText = "";
        private string _localFilePath = "";
        private string _statusMessage = "Pronto";
        private string _currentVersionText = "Versao atual: -";
        private string _itemCountText = "Itens: 0";
        private string _groupCountText = "Grupos: 0";
        private string _groupFilterInfoText = "";
        private Visibility _groupPanelVisibility = Visibility.Collapsed;
        private bool _isLoading = false;
        private M3UEntry _selectedEntry = new M3UEntry();
        private const string DownloadStructureFileName = "estrutura_downloads.txt";
        private const string VodLinksDatabaseFileName = "banco_vod_links.txt";
        private const string LiveLinksDatabaseFileName = "banco_canais_ao_vivo.txt";
        private const string RepoApiBase = "https://api.github.com/repos/wesleiandersonti/MEU_GESTOR_DE_VODS";
        private readonly HttpClient _releaseClient = new HttpClient();
        private Dictionary<string, string> _downloadStructure = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool _isUpdateInProgress;
        private readonly List<M3UEntry> _allEntries = new List<M3UEntry>();
        private string? _selectedCategoryFilter;
        private string? _selectedGroupKeyFilter;

        public ObservableCollection<M3UEntry> Entries { get; set; } = new ObservableCollection<M3UEntry>();
        public ObservableCollection<M3UEntry> FilteredEntries { get; set; } = new ObservableCollection<M3UEntry>();
        public ObservableCollection<DownloadItem> Downloads { get; set; } = new ObservableCollection<DownloadItem>();
        public ObservableCollection<GroupCategoryItem> GroupCategories { get; set; } = new ObservableCollection<GroupCategoryItem>();

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

        public string LocalFilePath
        {
            get => _localFilePath;
            set { _localFilePath = value; OnPropertyChanged(nameof(LocalFilePath)); }
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

        public string WindowTitle
        {
            get => $"MEU GESTOR DE VODS v{GetCurrentAppVersion()}";
        }

        public string ItemCountText
        {
            get => _itemCountText;
            set { _itemCountText = value; OnPropertyChanged(nameof(ItemCountText)); }
        }

        public string GroupCountText
        {
            get => _groupCountText;
            set { _groupCountText = value; OnPropertyChanged(nameof(GroupCountText)); }
        }

        public string GroupFilterInfoText
        {
            get => _groupFilterInfoText;
            set { _groupFilterInfoText = value; OnPropertyChanged(nameof(GroupFilterInfoText)); }
        }

        public Visibility GroupPanelVisibility
        {
            get => _groupPanelVisibility;
            set { _groupPanelVisibility = value; OnPropertyChanged(nameof(GroupPanelVisibility)); }
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
        private DatabaseService? _databaseService;
        private MigrationService? _migrationService;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            _m3uService = new M3UService();
            _downloadService = new DownloadService();
            DownloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Meu Gestor VODs");
            EnsureAndLoadDownloadStructure();
            InitializeDatabase();
            EnsureLinkDatabaseFiles();
            _releaseClient.DefaultRequestHeaders.Add("User-Agent", "MeuGestorVODs");
            CurrentVersionText = $"Versao atual: {GetCurrentAppVersion()}";
            ItemCountText = "Itens: 0";
            GroupCountText = "Grupos: 0";
            GroupFilterInfoText = "";
        }

        private void InitializeDatabase()
        {
            try
            {
                if (!Directory.Exists(DownloadPath))
                {
                    Directory.CreateDirectory(DownloadPath);
                }

                _databaseService = new DatabaseService(DownloadPath);
                _migrationService = new MigrationService(_databaseService);

                // Verificar se precisa migrar dados antigos
                var vodFilePath = Path.Combine(DownloadPath, VodLinksDatabaseFileName);
                var liveFilePath = Path.Combine(DownloadPath, LiveLinksDatabaseFileName);

                if (_migrationService.HasDataToMigrate(vodFilePath, liveFilePath))
                {
                    var result = System.Windows.MessageBox.Show(
                        "Foram encontrados dados antigos nos arquivos TXT. Deseja migrar para o novo banco SQLite?\n\n" +
                        "A migração preservará todos os seus links e metadados.",
                        "Migração de Dados",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var migrationResult = await _migrationService.MigrateFromTxtFilesAsync(
                                    vodFilePath, liveFilePath);

                                await Dispatcher.InvokeAsync(() =>
                                {
                                    StatusMessage = $"Migração concluída: {migrationResult.TotalMigrated} entradas migradas";
                                });
                            }
                            catch (Exception ex)
                            {
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    StatusMessage = $"Erro na migração: {ex.Message}";
                                });
                            }
                        });
                    }
                }

                // Carregar histórico de URLs
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000); // Aguardar UI carregar
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        await LoadM3UUrlHistory();
                    });
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Erro ao inicializar banco de dados: {ex.Message}\n\nO aplicativo funcionará sem persistência.",
                    "Erro de Inicialização",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async Task LoadM3UUrlHistory()
        {
            if (_databaseService == null || M3UUrlComboBox == null) return;

            try
            {
                var urls = await _databaseService.M3uUrls.GetRecentAsync(20);
                M3UUrlComboBox.Items.Clear();
                foreach (var url in urls)
                {
                    M3UUrlComboBox.Items.Add(url.Url);
                }
            }
            catch { }
        }

        private async void ShowUrlHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_databaseService == null)
            {
                System.Windows.MessageBox.Show("Banco de dados não inicializado.", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var urls = await _databaseService.M3uUrls.GetAllAsync();
                var onlineCount = urls.Count(u => u.IsOnline);
                var offlineCount = urls.Count(u => !u.IsOnline);

                var message = $"📊 HISTÓRICO DE URLs M3U\n\n" +
                             $"Total: {urls.Count}\n" +
                             $"✅ Online: {onlineCount}\n" +
                             $"❌ Offline: {offlineCount}\n\n" +
                             $"Últimas 10 URLs:\n";

                foreach (var url in urls.Take(10))
                {
                    var status = url.IsOnline ? "✅" : "❌";
                    var date = url.LastChecked.ToString("dd/MM/yy HH:mm");
                    var entries = url.EntryCount > 0 ? $" ({url.EntryCount} itens)" : "";
                    message += $"{status} {date} - {url.Url.Substring(0, Math.Min(50, url.Url.Length))}...{entries}\n";
                }

                System.Windows.MessageBox.Show(message, "Histórico de URLs", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ClearOfflineUrls_Click(object sender, RoutedEventArgs e)
        {
            if (_databaseService == null)
            {
                System.Windows.MessageBox.Show("Banco de dados não inicializado.", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var offlineUrls = await _databaseService.M3uUrls.GetOfflineAsync();
            if (offlineUrls.Count == 0)
            {
                System.Windows.MessageBox.Show("Não há URLs offline para remover.", "Informação", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Encontradas {offlineUrls.Count} URLs offline.\n\n" +
                $"Deseja removê-las do histórico?\n\n" +
                $"URLs offline:\n" +
                string.Join("\n", offlineUrls.Take(5).Select(u => $"- {u.Url.Substring(0, Math.Min(40, u.Url.Length))}...")),
                "Confirmar Remoção",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var deleted = await _databaseService.M3uUrls.DeleteOfflineAsync();
                    System.Windows.MessageBox.Show($"{deleted} URLs offline removidas com sucesso!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadM3UUrlHistory();
                    StatusMessage = $"{deleted} URLs offline removidas";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Erro ao remover: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
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

                _allEntries.Clear();
                _allEntries.AddRange(entries);

                _selectedCategoryFilter = null;
                _selectedGroupKeyFilter = null;
                GroupFilterInfoText = string.Empty;

                Entries.Clear();
                foreach (var entry in _allEntries)
                {
                    Entries.Add(entry);
                }

                BuildGroupIndex(_allEntries);

                var (newVod, newLive) = PersistLinkDatabases(entries);
                
                // Salvar URL no histórico
                if (_databaseService != null)
                {
                    await _databaseService.M3uUrls.SaveOrUpdateAsync(
                        M3UUrl, 
                        $"Lista carregada em {DateTime.Now:dd/MM/yyyy HH:mm}", 
                        isOnline: true, 
                        entryCount: entries.Count);
                }
                
                ApplyFilter();
                
                // Mostrar estatísticas do banco
                var dbCount = _databaseService?.Entries.GetCountAsync().Result ?? 0;
                var urlCount = _databaseService?.M3uUrls.GetAllAsync().Result.Count ?? 0;
                StatusMessage = $"Carregados {entries.Count} itens | SQLite: +{newVod} VOD, +{newLive} canais | Total no banco: {dbCount} | URLs: {urlCount}";
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

            IEnumerable<M3UEntry> filtered = _allEntries;

            if (!string.IsNullOrWhiteSpace(_selectedCategoryFilter))
            {
                filtered = filtered.Where(x => string.Equals(x.Category, _selectedCategoryFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(_selectedGroupKeyFilter))
            {
                filtered = filtered.Where(x => string.Equals(x.GroupKey, _selectedGroupKeyFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                filtered = filtered.Where(MatchesFilter);
            }

            foreach (var entry in filtered)
            {
                FilteredEntries.Add(entry);
            }
        }

        private bool MatchesFilter(M3UEntry entry)
        {
            return entry.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                   entry.GroupDisplay.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
        }

        private void BuildGroupIndex(IEnumerable<M3UEntry> entries)
        {
            GroupCategories.Clear();

            var groupedByCategory = entries
                .GroupBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var totalGroups = 0;
            foreach (var categoryGroup in groupedByCategory)
            {
                var categoryItem = new GroupCategoryItem
                {
                    CategoryName = categoryGroup.Key
                };

                var groups = categoryGroup
                    .GroupBy(e => e.GroupKey, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new GroupListItem
                    {
                        CategoryName = categoryGroup.Key,
                        GroupName = g.First().SubCategory,
                        GroupKey = g.Key,
                        ChannelCount = g.Count()
                    })
                    .OrderBy(g => g.GroupName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                totalGroups += groups.Count;
                foreach (var group in groups)
                {
                    categoryItem.Groups.Add(group);
                }

                GroupCategories.Add(categoryItem);
            }

            ItemCountText = $"Itens: {_allEntries.Count}";
            GroupCountText = $"Grupos: {totalGroups}";
        }

        private void ItemsSummary_Click(object sender, RoutedEventArgs e)
        {
            _selectedCategoryFilter = null;
            _selectedGroupKeyFilter = null;
            GroupFilterInfoText = "";
            FilterText = string.Empty;
            GroupPanelVisibility = Visibility.Collapsed;
            ApplyFilter();
            StatusMessage = $"Listagem completa exibida ({_allEntries.Count} itens).";
        }

        private void GroupsSummary_Click(object sender, RoutedEventArgs e)
        {
            if (GroupCategories.Count == 0 && _allEntries.Count > 0)
            {
                BuildGroupIndex(_allEntries);
            }

            GroupPanelVisibility = GroupPanelVisibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;

            StatusMessage = GroupPanelVisibility == Visibility.Visible
                ? "Painel de grupos aberto"
                : "Painel de grupos fechado";
        }

        private void GroupsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is GroupListItem group)
            {
                _selectedCategoryFilter = group.CategoryName;
                _selectedGroupKeyFilter = group.GroupKey;
                GroupFilterInfoText = $"Filtro: {group.CategoryName} | {group.GroupName}";
                ApplyFilter();
                StatusMessage = $"Grupo selecionado: {group.DisplayName}";
                return;
            }

            if (e.NewValue is GroupCategoryItem category)
            {
                _selectedCategoryFilter = category.CategoryName;
                _selectedGroupKeyFilter = null;
                GroupFilterInfoText = $"Filtro: {category.CategoryName}";
                ApplyFilter();
                StatusMessage = $"Categoria selecionada: {category.CategoryName}";
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

        private void EntriesList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (FindParent<System.Windows.Controls.CheckBox>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            var row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row?.Item is not M3UEntry entry)
            {
                return;
            }

            SelectedEntry = entry;
            EntriesList.SelectedItem = entry;

            var menu = new ContextMenu();

            var copyItem = new MenuItem
            {
                Header = "Copiar URL"
            };
            copyItem.Click += CopySelectedUrl_Click;

            var pasteItem = new MenuItem
            {
                Header = "Colar (URL M3U)"
            };
            pasteItem.Click += PasteToM3uField_Click;

            var checkItem = new MenuItem
            {
                Header = "Verificar se ja esta salvo no TXT"
            };
            checkItem.Click += CheckSelectedInTxt_Click;

            var playItem = new MenuItem
            {
                Header = "Reproduzir no VLC"
            };
            playItem.Click += PlaySelectedInVlc_Click;

            menu.Items.Add(copyItem);
            menu.Items.Add(pasteItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(checkItem);
            menu.Items.Add(playItem);
            menu.PlacementTarget = row;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private void CopySelectedUrl_Click(object sender, RoutedEventArgs e)
        {
            var entry = ResolveCurrentEntry();
            if (entry == null || string.IsNullOrWhiteSpace(entry.Url))
            {
                System.Windows.MessageBox.Show("Selecione um canal com URL valida.", "Copiar URL", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            System.Windows.Clipboard.SetText(entry.Url);
            StatusMessage = $"URL copiada: {entry.Name}";
        }

        private void PasteToM3uField_Click(object sender, RoutedEventArgs e)
        {
            if (!System.Windows.Clipboard.ContainsText())
            {
                System.Windows.MessageBox.Show("Area de transferencia vazia.", "Colar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            M3UUrl = System.Windows.Clipboard.GetText().Trim();
            StatusMessage = "URL colada no campo M3U";
        }

        private async void CheckSelectedInTxt_Click(object sender, RoutedEventArgs e)
        {
            var entry = ResolveCurrentEntry();
            if (entry == null)
            {
                System.Windows.MessageBox.Show("Selecione um conteudo primeiro.", "Informacao", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Verificar no banco SQLite primeiro
            if (_databaseService != null)
            {
                var exists = await _databaseService.Entries.ExistsByUrlAsync(entry.Url);
                if (exists)
                {
                    System.Windows.MessageBox.Show(
                        $"Conteudo encontrado no banco de dados SQLite.\n\nNome: {entry.Name}",
                        "Verificacao Banco de Dados",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    StatusMessage = "Conteudo ja esta salvo no banco SQLite";
                    return;
                }
            }

            // Fallback para arquivos TXT (compatibilidade)
            EnsureLinkDatabaseFiles();

            var vodFilePath = Path.Combine(DownloadPath, VodLinksDatabaseFileName);
            var liveFilePath = Path.Combine(DownloadPath, LiveLinksDatabaseFileName);

            var inVod = IsUrlInDatabase(vodFilePath, entry.Url);
            var inLive = IsUrlInDatabase(liveFilePath, entry.Url);

            if (inVod || inLive)
            {
                var source = inVod ? VodLinksDatabaseFileName : LiveLinksDatabaseFileName;
                System.Windows.MessageBox.Show(
                    $"Conteudo encontrado no banco TXT.\n\nArquivo: {source}\nNome: {entry.Name}",
                    "Verificacao TXT",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                StatusMessage = $"Conteudo ja esta salvo no TXT ({source})";
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Conteudo ainda nao foi encontrado nos bancos.",
                    "Verificacao",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                StatusMessage = "Conteudo nao encontrado no banco de dados";
            }
        }

        private void PlaySelectedInVlc_Click(object sender, RoutedEventArgs e)
        {
            var entry = ResolveCurrentEntry();
            if (entry == null)
            {
                System.Windows.MessageBox.Show("Selecione um conteudo para reproduzir.", "Informacao", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.Url))
            {
                System.Windows.MessageBox.Show("O conteudo selecionado nao possui URL valida.", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var vlcPath = FindVlcPath();
            if (string.IsNullOrWhiteSpace(vlcPath))
            {
                System.Windows.MessageBox.Show(
                    "VLC nao encontrado. Instale o VLC para usar esta opcao.",
                    "VLC nao encontrado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                StatusMessage = "VLC nao encontrado no sistema";
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = vlcPath,
                    Arguments = $"\"{entry.Url}\"",
                    UseShellExecute = true
                });
                StatusMessage = $"Reproduzindo no VLC: {entry.Name}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Falha ao abrir VLC: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Erro ao abrir VLC";
            }
        }

        private M3UEntry? ResolveCurrentEntry()
        {
            return EntriesList.SelectedItem as M3UEntry ?? SelectedEntry;
        }

        private static bool IsUrlInDatabase(string filePath, string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || !File.Exists(filePath))
            {
                return false;
            }

            var target = url.Trim();
            var lines = File.ReadAllLines(filePath);
            var isM3uFormat = lines.Length > 0 && lines[0].Trim().StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase);

            if (isM3uFormat)
            {
                // Parse M3U format
                for (int i = 0; i < lines.Length - 1; i++)
                {
                    var line = lines[i].Trim();
                    if (!line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var nextLine = lines[i + 1].Trim();
                    if (string.IsNullOrWhiteSpace(nextLine) || nextLine.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.Equals(nextLine, target, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            else
            {
                // Parse old pipe-delimited format (backward compatibility)
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parts = line.Split('|');
                    if (parts.Length == 0)
                    {
                        continue;
                    }

                    var candidate = parts[^1].Trim();
                    if (string.Equals(candidate, target, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string? FindVlcPath()
        {
            var commonPaths = new[]
            {
                @"C:\Program Files\VideoLAN\VLC\vlc.exe",
                @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoLAN", "VLC", "vlc.exe")
            };

            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var folder in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(folder, "vlc.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T typed)
                {
                    return typed;
                }
                child = VisualTreeHelper.GetParent(child);
            }

            return null;
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
                
                // Reinicializar banco na nova pasta
                _databaseService?.Dispose();
                InitializeDatabase();
                
                EnsureLinkDatabaseFiles();
                StatusMessage = $"Banco de dados SQLite carregado em: {Path.Combine(DownloadPath, "database.sqlite")}";
            }
        }

        private void BrowseLocalFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Arquivos M3U/M3U8/TXT|*.m3u;*.m3u8;*.txt|Todos os arquivos|*.*",
                Title = "Selecionar arquivo de playlist",
                CheckFileExists = true,
                CheckPathExists = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LocalFilePath = dialog.FileName;
                StatusMessage = $"Arquivo selecionado: {Path.GetFileName(LocalFilePath)}";
            }
        }

        private async void LoadLocalFile_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LocalFilePath))
            {
                System.Windows.MessageBox.Show("Por favor, selecione um arquivo primeiro.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(LocalFilePath))
            {
                System.Windows.MessageBox.Show("Arquivo não encontrado.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Carregando arquivo local...";
                
                var content = await File.ReadAllTextAsync(LocalFilePath);
                var entries = _m3uService.ParseFromString(content);

                _allEntries.Clear();
                _allEntries.AddRange(entries);

                _selectedCategoryFilter = null;
                _selectedGroupKeyFilter = null;
                GroupFilterInfoText = string.Empty;

                Entries.Clear();
                foreach (var entry in _allEntries)
                {
                    Entries.Add(entry);
                }

                BuildGroupIndex(_allEntries);

                var (newVod, newLive) = PersistLinkDatabases(entries);
                
                // Salvar no histórico como arquivo local
                if (_databaseService != null)
                {
                    await _databaseService.M3uUrls.SaveOrUpdateAsync(
                        $"[LOCAL] {LocalFilePath}", 
                        $"Arquivo local carregado em {DateTime.Now:dd/MM/yyyy HH:mm}", 
                        isOnline: true, 
                        entryCount: entries.Count);
                }
                
                ApplyFilter();
                
                // Mostrar estatísticas do banco
                var dbCount = _databaseService?.Entries.GetCountAsync().Result ?? 0;
                var urlCount = _databaseService?.M3uUrls.GetAllAsync().Result.Count ?? 0;
                StatusMessage = $"Carregados {entries.Count} itens do arquivo local | SQLite: +{newVod} VOD, +{newLive} canais | Total no banco: {dbCount} | URLs: {urlCount}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao carregar arquivo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Erro ao carregar arquivo";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void EnsureLinkDatabaseFiles()
        {
            if (!Directory.Exists(DownloadPath))
            {
                Directory.CreateDirectory(DownloadPath);
            }

            var vodFilePath = Path.Combine(DownloadPath, VodLinksDatabaseFileName);
            if (!File.Exists(vodFilePath))
            {
                File.WriteAllLines(vodFilePath, new[]
                {
                    "#EXTM3U",
                    "# Banco TXT de links VOD (videos e series) - Formato M3U"
                });
            }

            var liveFilePath = Path.Combine(DownloadPath, LiveLinksDatabaseFileName);
            if (!File.Exists(liveFilePath))
            {
                File.WriteAllLines(liveFilePath, new[]
                {
                    "#EXTM3U",
                    "# Banco TXT de links de canais ao vivo - Formato M3U"
                });
            }
        }

        private (int newVod, int newLive) PersistLinkDatabases(IEnumerable<M3UEntry> entries)
        {
            EnsureLinkDatabaseFiles();

            // Sempre manter ambos sincronizados: SQLite (principal) + TXT (backup)
            var vodFilePath = Path.Combine(DownloadPath, VodLinksDatabaseFileName);
            var liveFilePath = Path.Combine(DownloadPath, LiveLinksDatabaseFileName);

            var vodEntries = entries.Where(IsVodEntry).ToList();
            var liveEntries = entries.Where(e => !IsVodEntry(e)).ToList();

            var newVodEntries = new List<M3UEntry>();
            var newLiveEntries = new List<M3UEntry>();

            // PASSO 1: Salvar no SQLite (banco principal)
            if (_databaseService != null)
            {
                foreach (var entry in vodEntries)
                {
                    if (!_databaseService.Entries.ExistsByUrlAsync(entry.Url).Result)
                    {
                        _databaseService.Entries.AddAsync(entry).Wait();
                        newVodEntries.Add(entry);
                    }
                }

                foreach (var entry in liveEntries)
                {
                    if (!_databaseService.Entries.ExistsByUrlAsync(entry.Url).Result)
                    {
                        _databaseService.Entries.AddAsync(entry).Wait();
                        newLiveEntries.Add(entry);
                    }
                }
            }
            else
            {
                // Fallback se banco não disponível: todas são novas
                newVodEntries = vodEntries;
                newLiveEntries = liveEntries;
            }

            // PASSO 2: Sincronizar com arquivos TXT (backup)
            // Adiciona apenas as entradas que foram inseridas no SQLite
            var addedVod = MergeEntriesIntoDatabase(vodFilePath, newVodEntries);
            var addedLive = MergeEntriesIntoDatabase(liveFilePath, newLiveEntries);

            return (addedVod, addedLive);
        }

        private int MergeEntriesIntoDatabase(string filePath, IEnumerable<M3UEntry> entries)
        {
            var existingUrls = LoadExistingUrls(filePath);
            var linesToAppend = new List<string>();

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Url))
                {
                    continue;
                }

                if (!existingUrls.Add(entry.Url.Trim()))
                {
                    continue;
                }

                // Build M3U EXTINF line with all metadata
                var tvgId = !string.IsNullOrWhiteSpace(entry.TvgId) ? entry.TvgId : entry.Id;
                var tvgName = EscapeM3uAttribute(entry.Name);
                var groupTitle = EscapeM3uAttribute(entry.GroupTitle);
                var logoUrl = EscapeM3uAttribute(entry.LogoUrl);

                var extinf = $"#EXTINF:-1 tvg-id=\"{EscapeM3uAttribute(tvgId)}\" tvg-name=\"{tvgName}\"";
                if (!string.IsNullOrWhiteSpace(logoUrl))
                {
                    extinf += $" tvg-logo=\"{logoUrl}\"";
                }
                extinf += $" group-title=\"{groupTitle}\",{tvgName}";

                linesToAppend.Add(extinf);
                linesToAppend.Add(entry.Url.Trim());
            }

            if (linesToAppend.Count > 0)
            {
                File.AppendAllLines(filePath, linesToAppend);
            }

            return linesToAppend.Count / 2; // Each entry has 2 lines (EXTINF + URL)
        }

        private static string EscapeM3uAttribute(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return value.Replace("\"", "'");
        }

        private static HashSet<string> LoadExistingUrls(string filePath)
        {
            var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(filePath))
            {
                return urls;
            }

            var lines = File.ReadAllLines(filePath);
            var isM3uFormat = lines.Length > 0 && lines[0].Trim().StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase);

            if (isM3uFormat)
            {
                // Parse M3U format
                for (int i = 0; i < lines.Length - 1; i++)
                {
                    var line = lines[i].Trim();
                    if (!line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var nextLine = lines[i + 1].Trim();
                    if (string.IsNullOrWhiteSpace(nextLine) || nextLine.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(nextLine))
                    {
                        urls.Add(nextLine);
                    }
                }
            }
            else
            {
                // Parse old pipe-delimited format (backward compatibility)
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parts = line.Split('|');
                    if (parts.Length == 0)
                    {
                        continue;
                    }

                    var url = parts[^1].Trim();
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        urls.Add(url);
                    }
                }
            }

            return urls;
        }

        private bool IsVodEntry(M3UEntry entry)
        {
            var category = ResolveCategory(entry);
            if (string.Equals(category, "Canais", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(category, "24 Horas", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var url = entry.Url?.ToLowerInvariant() ?? string.Empty;
            if (url.Contains("/live") || url.Contains("/channel") || url.Contains("channels"))
            {
                return false;
            }

            return true;
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

        private async void OpenVodLinksTxt_Click(object sender, RoutedEventArgs e)
        {
            // Mostrar estatísticas do banco
            if (_databaseService != null)
            {
                var count = await _databaseService.Entries.GetCountAsync();
                var result = System.Windows.MessageBox.Show(
                    $"Banco SQLite contém {count} entradas totais.\n\n" +
                    "Deseja:\n" +
                    "- Sim: Exportar banco para arquivo M3U\n" +
                    "- Não: Abrir arquivo TXT legado",
                    "Gerenciar Banco de Dados",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await ExportDatabaseToM3uAsync();
                    return;
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            EnsureLinkDatabaseFiles();
            var filePath = Path.Combine(DownloadPath, VodLinksDatabaseFileName);
            OpenTextFileInNotepad(filePath, "VOD");
        }

        private async void OpenLiveLinksTxt_Click(object sender, RoutedEventArgs e)
        {
            // Reutilizar a mesma lógica do VOD
            OpenVodLinksTxt_Click(sender, e);
        }

        private async Task ExportDatabaseToM3uAsync()
        {
            try
            {
                if (_databaseService == null) return;

                StatusMessage = "Exportando banco para M3U...";
                var entries = await _databaseService.Entries.GetAllAsync();
                
                var outputPath = Path.Combine(DownloadPath, "exportado_banco_completo.m3u");
                var lines = new List<string> { "#EXTM3U" };

                foreach (var entry in entries)
                {
                    var tvgId = !string.IsNullOrWhiteSpace(entry.TvgId) ? entry.TvgId : entry.Id;
                    var extinf = $"#EXTINF:-1 tvg-id=\"{EscapeM3uAttribute(tvgId)}\" " +
                                $"tvg-name=\"{EscapeM3uAttribute(entry.Name)}\" " +
                                $"tvg-logo=\"{EscapeM3uAttribute(entry.LogoUrl)}\" " +
                                $"group-title=\"{EscapeM3uAttribute(entry.GroupTitle)}\"," +
                                EscapeM3uAttribute(entry.Name);
                    
                    lines.Add(extinf);
                    lines.Add(entry.Url);
                }

                await File.WriteAllLinesAsync(outputPath, lines);
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{outputPath}\"",
                    UseShellExecute = true
                });

                StatusMessage = $"Exportado {entries.Count} entradas para M3U";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao exportar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Erro ao exportar banco";
            }
        }

        private void OpenTextFileInNotepad(string filePath, string label)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = true
                });

                StatusMessage = $"Abrindo TXT de {label}...";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Nao foi possivel abrir o arquivo TXT de {label}: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = $"Erro ao abrir TXT de {label}";
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

        private async void ShowDatabaseStats_Click(object sender, RoutedEventArgs e)
        {
            if (_databaseService == null)
            {
                System.Windows.MessageBox.Show(
                    "Banco de dados não está inicializado.",
                    "Estatísticas",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                var totalCount = await _databaseService.Entries.GetCountAsync();
                var allEntries = await _databaseService.Entries.GetAllAsync();
                
                var categories = allEntries
                    .GroupBy(x => x.Category)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => $"  {g.Key}: {g.Count()}")
                    .ToList();

                var stats = $"📊 ESTATÍSTICAS DO BANCO DE DADOS SQLite\n\n" +
                           $"Total de entradas: {totalCount}\n\n" +
                           $"📁 Top 10 Categorias:\n" +
                           string.Join("\n", categories) + "\n\n" +
                           $"📍 Local do banco:\n{Path.Combine(DownloadPath, "database.sqlite")}";

                System.Windows.MessageBox.Show(
                    stats,
                    "Estatísticas do Banco de Dados",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                StatusMessage = $"Banco SQLite: {totalCount} entradas registradas";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Erro ao obter estatísticas: {ex.Message}",
                    "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void PlayDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Obtém o item de download do DataContext
                if (sender is Button button && button.DataContext is DownloadItem downloadItem)
                {
                    StatusMessage = $"Abrindo player: {downloadItem.Name}...";
                    
                    // Cria e mostra a janela do player
                    var playerWindow = new PlayerWindow();
                    playerWindow.Show();
                    
                    // Busca a URL original no banco de dados
                    string? streamUrl = null;
                    if (_databaseService != null)
                    {
                        var entries = await _databaseService.Entries.GetAllAsync();
                        var entry = entries.FirstOrDefault(e => e.Name == downloadItem.Name);
                        if (entry != null)
                        {
                            streamUrl = entry.Url;
                        }
                    }
                    
                    // Se não encontrou no banco, tenta usar o caminho do arquivo local
                    if (string.IsNullOrEmpty(streamUrl))
                    {
                        // Procura o arquivo baixado
                        var possiblePath = Path.Combine(DownloadPath, downloadItem.Name);
                        if (File.Exists(possiblePath))
                        {
                            streamUrl = possiblePath;
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(streamUrl))
                    {
                        await playerWindow.PlayStream(streamUrl, downloadItem.Name);
                        StatusMessage = $"Reproduzindo: {downloadItem.Name}";
                    }
                    else
                    {
                        MessageBox.Show("Não foi possível encontrar a URL do stream.", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
                        playerWindow.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir player: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Erro ao abrir player";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class GroupCategoryItem
    {
        public string CategoryName { get; set; } = string.Empty;
        public ObservableCollection<GroupListItem> Groups { get; set; } = new ObservableCollection<GroupListItem>();
    }

    public class GroupListItem
    {
        public string CategoryName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string GroupKey { get; set; } = string.Empty;
        public int ChannelCount { get; set; }
        public string DisplayName => $"{GroupName} ({ChannelCount})";
    }

    public class DownloadItem : INotifyPropertyChanged
    {
        private string _name = "";
        private double _progress;
        private string _status = "";

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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ProgressToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double progress)
            {
                return progress >= 100 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}