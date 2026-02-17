using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MeuGestorVODs.Repositories;
using MeuGestorVODs.ViewModels;
using MySqlConnector;
using Microsoft.Web.WebView2.Wpf;

namespace MeuGestorVODs
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly MainViewModel _vm;
        private readonly HttpClient _releaseClient = new HttpClient();
        private readonly Dictionary<TabItem, Process> _embeddedTabProcesses = new();
        private readonly Dictionary<TabItem, System.Windows.Forms.Panel> _embeddedTabHosts = new();
        private Dictionary<string, string> _downloadStructure = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool _isUpdateInProgress;
        private readonly List<M3UEntry> _allEntries = new List<M3UEntry>();
        private string? _selectedCategoryFilter;
        private string? _selectedGroupKeyFilter;

        public ObservableCollection<M3UEntry> Entries => _vm.Entries;
        public ObservableCollection<M3UEntry> FilteredEntries => _vm.FilteredEntries;
        public ObservableCollection<DownloadItem> Downloads => _vm.Downloads;
        public ObservableCollection<string> LocalFileHistory => _vm.LocalFileHistory;
        public ObservableCollection<GroupCategoryItem> GroupCategories => _vm.GroupCategories;
        public ObservableCollection<ServerScoreResult> ServerScores => _vm.ServerScores;
        public ObservableCollection<string> AnalysisFilterOptions => _vm.AnalysisFilterOptions;

        public string M3UUrl { get => _vm.M3UUrl; set => _vm.M3UUrl = value; }
        public string DownloadPath { get => _vm.DownloadPath; set => _vm.DownloadPath = value; }
        public string LocalFilePath { get => _vm.LocalFilePath; set => _vm.LocalFilePath = value; }
        public string FilterText { get => _vm.FilterText; set { _vm.FilterText = value; ApplyFilter(); } }
        public string StatusMessage
        {
            get => _vm.StatusMessage;
            set
            {
                _vm.StatusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        public string CurrentVersionText
        {
            get => _vm.CurrentVersionText;
            set
            {
                _vm.CurrentVersionText = value;
                OnPropertyChanged(nameof(CurrentVersionText));
            }
        }

        public bool IsUpdateAvailable
        {
            get => _vm.IsUpdateAvailable;
            set
            {
                _vm.IsUpdateAvailable = value;
                OnPropertyChanged(nameof(IsUpdateAvailable));
            }
        }
        public string ItemCountText { get => _vm.ItemCountText; set => _vm.ItemCountText = value; }
        public string GroupCountText { get => _vm.GroupCountText; set => _vm.GroupCountText = value; }
        public string GroupFilterInfoText { get => _vm.GroupFilterInfoText; set => _vm.GroupFilterInfoText = value; }
        public string AnalysisProgressText { get => _vm.AnalysisProgressText; set => _vm.AnalysisProgressText = value; }
        public string AnalysisSummaryText { get => _vm.AnalysisSummaryText; set => _vm.AnalysisSummaryText = value; }
        public string SelectedAnalysisFilter { get => _vm.SelectedAnalysisFilter; set { _vm.SelectedAnalysisFilter = value; ApplyFilter(); } }
        public string ThemeButtonText { get => _vm.ThemeButtonText; set => _vm.ThemeButtonText = value; }
        public string DownloadActionButtonText { get => _vm.DownloadActionButtonText; set => _vm.DownloadActionButtonText = value; }
        public double AnalysisProgressValue { get => _vm.AnalysisProgressValue; set => _vm.AnalysisProgressValue = value; }
        public bool IsAnalyzingLinks { get => _vm.IsAnalyzingLinks; set => _vm.IsAnalyzingLinks = value; }
        public bool IsLocalFileDragOver { get => _vm.IsLocalFileDragOver; set => _vm.IsLocalFileDragOver = value; }
        public Visibility GroupPanelVisibility { get => _vm.GroupPanelVisibility; set => _vm.GroupPanelVisibility = value; }
        public bool IsLoading { get => _vm.IsLoading; set => _vm.IsLoading = value; }
        public M3UEntry SelectedEntry { get => _vm.SelectedEntry; set => _vm.SelectedEntry = value; }
        public string WindowTitle => _vm.WindowTitle;

        private M3UService _m3uService;
        private DownloadService _downloadService;
        private LinkHealthService _linkHealthService;
        private StreamCheckService _streamCheckService;
        private ServerScoreService _serverScoreService;
        private DuplicateDetectionService _duplicateDetectionService;
        private CheckerOrchestrator _checkerOrchestrator;
        private DatabaseService? _databaseService;
        private MigrationService? _migrationService;
        private readonly DispatcherTimer _linkCheckTimer = new DispatcherTimer();
        private LinkCheckScheduleMode _linkCheckMode = LinkCheckScheduleMode.Manual;
        private bool _isRunningScheduledCheck;
        private MonitorPanelLayout _monitorPanelLayout = MonitorPanelLayout.Normal;
        private AppThemeMode _appThemeMode = AppThemeMode.System;
        private CancellationTokenSource? _downloadCts;
        private CancellationTokenSource? _analysisCts;
        private ManualResetEventSlim _downloadPauseGate = new(initialState: true);
        private bool _isDownloadRunning;
        private bool _isDownloadPaused;

        private const int GwlStyle = -16;
        private const int WsChild = 0x40000000;
        private const int WsVisible = 0x10000000;
        private const int WsCaption = 0x00C00000;
        private const int WsThickFrame = 0x00040000;
        private const int WsMinimizeBox = 0x00020000;
        private const int WsMaximizeBox = 0x00010000;
        private const int WsSysMenu = 0x00080000;

        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;
        private const uint SwpShowWindow = 0x0040;
        private const int SwShow = 5;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr childWindowHandle, IntPtr newParentHandle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr windowHandle, int index);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr windowHandle, int index, int newLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr windowHandle, int x, int y, int width, int height, bool repaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr windowHandle, int command);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr windowHandle,
            IntPtr windowInsertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        public MainWindow()
        {
            _vm = new MainViewModel { GetVersion = GetCurrentAppVersion };
            InitializeComponent();
            DataContext = this;
            _m3uService = new M3UService();
            _downloadService = new DownloadService();
            _linkHealthService = new LinkHealthService();
            _streamCheckService = new StreamCheckService();
            _serverScoreService = new ServerScoreService();
            _duplicateDetectionService = new DuplicateDetectionService();
            _checkerOrchestrator = new CheckerOrchestrator(_streamCheckService, _duplicateDetectionService, _serverScoreService);
            _vm.DownloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Meu Gestor VODs");
            EnsureAndLoadDownloadStructure();
            InitializeDatabase();
            EnsureLinkDatabaseFiles();
            LoadLocalFileHistory();
            _releaseClient.DefaultRequestHeaders.Add("User-Agent", "MeuGestorVODs");
            CurrentVersionText = $"Versao atual: {GetCurrentAppVersion()}";
            _vm.ItemCountText = "Itens: 0";
            _vm.GroupCountText = "Grupos: 0";
            _vm.GroupFilterInfoText = "";
            _vm.SelectedAnalysisFilter = "Todos";

            // Verifica atualizações silenciosamente ao iniciar
            _ = CheckForUpdatesSilentAsync();

            _linkCheckTimer.Tick += LinkCheckTimer_Tick;
            StateChanged += (_, _) =>
            {
                UpdateWindowStateButton();
                ResizeSelectedEmbeddedTab();
            };
            SizeChanged += (_, _) => ResizeSelectedEmbeddedTab();
            ChromeTabControl.SelectionChanged += (_, _) => ResizeSelectedEmbeddedTab();
            ChromeTabControl.SizeChanged += (_, _) => ResizeSelectedEmbeddedTab();
            Closing += MainWindow_Closing;
            ApplyMonitorPanelLayout(MonitorPanelLayout.Normal);
            ApplyTheme(AppThemeMode.System, updateStatus: false);
            UpdateWindowStateButton();
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
                    if (IsRemoteM3uUrl(url.Url))
                    {
                        M3UUrlComboBox.Items.Add(url.Url);
                    }
                }
            }
            catch { }
        }

        private void LoadLocalFileHistory()
        {
            LocalFileHistory.Clear();

            try
            {
                var path = GetLocalFileHistoryFilePath();
                if (!File.Exists(path))
                {
                    return;
                }

                var json = File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                var existing = items
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Where(File.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(30)
                    .ToList();

                foreach (var item in existing)
                {
                    LocalFileHistory.Add(item);
                }

                SaveLocalFileHistory();
            }
            catch
            {
            }
        }

        private void SaveLocalFileHistory()
        {
            try
            {
                var path = GetLocalFileHistoryFilePath();
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var data = LocalFileHistory
                    .Where(File.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(30)
                    .ToList();

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch
            {
            }
        }

        private void AddLocalFileToHistory(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            var normalized = Path.GetFullPath(filePath);
            var existing = LocalFileHistory.FirstOrDefault(x =>
                string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(existing))
            {
                LocalFileHistory.Remove(existing);
            }

            LocalFileHistory.Insert(0, normalized);

            while (LocalFileHistory.Count > 30)
            {
                LocalFileHistory.RemoveAt(LocalFileHistory.Count - 1);
            }

            SaveLocalFileHistory();
        }

        private void RefreshLocalFileHistory()
        {
            var invalid = LocalFileHistory.Where(x => !File.Exists(x)).ToList();
            if (invalid.Count == 0)
            {
                return;
            }

            foreach (var missing in invalid)
            {
                LocalFileHistory.Remove(missing);
            }

            if (!string.IsNullOrWhiteSpace(LocalFilePath) && !File.Exists(LocalFilePath))
            {
                LocalFilePath = string.Empty;
            }

            SaveLocalFileHistory();
        }

        private static string GetLocalFileHistoryFilePath()
        {
            var baseFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MeuGestorVODs");
            return Path.Combine(baseFolder, LocalFileHistoryFileName);
        }

        private static bool IsRemoteM3uUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var text = value.Trim();
            if (text.StartsWith("[LOCAL]", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
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
                var urls = (await _databaseService.M3uUrls.GetAllAsync())
                    .Where(u => IsRemoteM3uUrl(u.Url))
                    .ToList();
                var onlineCount = urls.Count(u => u.IsOnline);
                var offlineCount = urls.Count(u => !u.IsOnline);
                var archive = await _databaseService.M3uUrls.GetOfflineArchiveAsync();

                var message = $"📊 HISTÓRICO DE URLs M3U\n\n" +
                             $"Total: {urls.Count}\n" +
                             $"✅ Online: {onlineCount}\n" +
                             $"❌ Offline: {offlineCount}\n" +
                             $"🗂️ No arquivo de offline: {archive.Count}\n\n" +
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

            var choice = PromptForChoice(
                "Escolha como deseja verificar links offline:",
                "Verificação de Links Offline",
                new[]
                {
                    "Começar agora (manual)",
                    "Agendar a cada 3 horas",
                    "Agendar a cada 6 horas",
                    "Agendar a cada 12 horas"
                },
                "Começar agora (manual)");

            if (string.IsNullOrWhiteSpace(choice))
            {
                return;
            }

            if (choice.StartsWith("Começar agora", StringComparison.OrdinalIgnoreCase))
            {
                await ConfigureLinkCheckScheduleAsync(LinkCheckScheduleMode.Manual, runNow: true);
                return;
            }

            if (choice.Contains("3 horas", StringComparison.OrdinalIgnoreCase))
            {
                await ConfigureLinkCheckScheduleAsync(LinkCheckScheduleMode.Every3Hours, runNow: true);
                return;
            }

            if (choice.Contains("6 horas", StringComparison.OrdinalIgnoreCase))
            {
                await ConfigureLinkCheckScheduleAsync(LinkCheckScheduleMode.Every6Hours, runNow: true);
                return;
            }

            await ConfigureLinkCheckScheduleAsync(LinkCheckScheduleMode.Every12Hours, runNow: true);
        }

        private async void LinkCheckTimer_Tick(object? sender, EventArgs e)
        {
            await RunOfflineMonitoringCycleAsync("agendada");
        }

        private async Task ConfigureLinkCheckScheduleAsync(LinkCheckScheduleMode mode, bool runNow)
        {
            _linkCheckMode = mode;

            if (mode == LinkCheckScheduleMode.Manual)
            {
                _linkCheckTimer.Stop();
                if (runNow)
                {
                    await RunOfflineMonitoringCycleAsync("manual");
                }
                return;
            }

            _linkCheckTimer.Interval = mode switch
            {
                LinkCheckScheduleMode.Every3Hours => TimeSpan.FromHours(3),
                LinkCheckScheduleMode.Every6Hours => TimeSpan.FromHours(6),
                LinkCheckScheduleMode.Every12Hours => TimeSpan.FromHours(12),
                _ => TimeSpan.FromHours(6)
            };

            _linkCheckTimer.Stop();
            _linkCheckTimer.Start();

            if (runNow)
            {
                await RunOfflineMonitoringCycleAsync("manual + agendada");
            }

            StatusMessage = $"Verificação automática configurada: {_linkCheckTimer.Interval.TotalHours:0}h";
        }

        private async Task RunOfflineMonitoringCycleAsync(string source)
        {
            if (_databaseService == null || _isRunningScheduledCheck)
            {
                return;
            }

            _isRunningScheduledCheck = true;

            try
            {
                var now = DateTime.Now;
                var checkedOnline = 0;
                var movedToArchive = 0;
                var recovered = 0;
                var retryFailed = 0;

                var onlineUrls = await _databaseService.M3uUrls.GetOnlineAsync();
                foreach (var item in onlineUrls)
                {
                    var check = await _linkHealthService.CheckAsync(item.Url);
                    checkedOnline++;

                    if (check.IsOnline)
                    {
                        await _databaseService.M3uUrls.UpdateStatusAsync(item.Url, true, item.EntryCount);
                        continue;
                    }

                    await _databaseService.M3uUrls.UpdateStatusAsync(item.Url, false, item.EntryCount);
                    await _databaseService.M3uUrls.EnsureOfflineArchivedAsync(item.Url, item.Name, check.Details, now);
                    movedToArchive++;
                }

                var dueRetries = await _databaseService.M3uUrls.GetDueOfflineRetriesAsync(now);
                foreach (var archived in dueRetries)
                {
                    var check = await _linkHealthService.CheckAsync(archived.Url);

                    if (check.IsOnline)
                    {
                        await _databaseService.M3uUrls.UpdateStatusAsync(archived.Url, true);
                        await _databaseService.M3uUrls.RemoveOfflineArchiveAsync(archived.Url);
                        recovered++;
                        continue;
                    }

                    await _databaseService.M3uUrls.UpdateStatusAsync(archived.Url, false);
                    await _databaseService.M3uUrls.RegisterOfflineRetryFailureAsync(archived.Url, check.Details, now);
                    retryFailed++;
                }

                var removed = await _databaseService.M3uUrls.DeleteExpiredOfflineUrlsAsync(now);
                var archiveCount = (await _databaseService.M3uUrls.GetOfflineArchiveAsync()).Count;

                await LoadM3UUrlHistory();

                StatusMessage =
                    $"Verificação {source}: online testadas {checkedOnline}, movidas {movedToArchive}, recuperadas {recovered}, retestes falhos {retryFailed}, excluídas após 2 dias {removed}, arquivo offline {archiveCount}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro na verificação de links: {ex.Message}";
            }
            finally
            {
                _isRunningScheduledCheck = false;
            }
        }

        private static string? PromptForChoice(string message, string title, IReadOnlyList<string> options, string defaultValue)
        {
            var combo = new System.Windows.Controls.ComboBox
            {
                Margin = new Thickness(0, 10, 0, 10),
                MinWidth = 320,
                ItemsSource = options,
                SelectedItem = defaultValue
            };

            var okButton = new System.Windows.Controls.Button
            {
                Content = "OK",
                IsDefault = true,
                MinWidth = 80,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Cancelar",
                IsCancel = true,
                MinWidth = 80
            };

            var buttons = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Children = { okButton, cancelButton }
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(12),
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    combo,
                    buttons
                }
            };

            var window = new Window
            {
                Title = title,
                Content = panel,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Owner = System.Windows.Application.Current?.MainWindow
            };

            okButton.Click += (_, __) => window.DialogResult = true;
            var result = window.ShowDialog();
            return result == true ? combo.SelectedItem as string : null;
        }

        private async void LoadM3U_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(M3UUrl))
            {
                System.Windows.MessageBox.Show("Por favor, insira a URL do arquivo M3U", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsRemoteM3uUrl(M3UUrl))
            {
                var candidate = M3UUrl.Trim();
                if (candidate.StartsWith("[LOCAL]", StringComparison.OrdinalIgnoreCase))
                {
                    candidate = candidate[7..].Trim();
                }

                var normalizedPath = NormalizePotentialLocalPath(candidate);
                if (!string.IsNullOrWhiteSpace(normalizedPath) && File.Exists(normalizedPath))
                {
                    LocalFilePath = normalizedPath;
                    AddLocalFileToHistory(LocalFilePath);
                }

                System.Windows.MessageBox.Show(
                    "Esse campo aceita apenas URL online (http/https).\nUse o campo 'Arquivo Local' para playlists do seu PC.",
                    "URL invalida para carregamento online",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Carregando lista M3U...";
                
                var entries = await _m3uService.LoadFromUrlAsync(M3UUrl);
                InitializeEntryAnalysisFields(entries);

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

                var (newVod, newLive) = await PersistLinkDatabasesAsync(entries);
                
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
                var dbCount = 0;
                var urlCount = 0;
                if (_databaseService != null)
                {
                    dbCount = await _databaseService.Entries.GetCountAsync();
                    urlCount = (await _databaseService.M3uUrls.GetAllAsync()).Count;
                }
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

            filtered = SelectedAnalysisFilter switch
            {
                "ONLINE" => filtered.Where(x => x.CheckStatus == ItemStatus.Ok),
                "OFFLINE" => filtered.Where(x => x.CheckStatus == ItemStatus.Error),
                "Duplicados" => filtered.Where(x => x.IsDuplicate),
                _ => filtered
            };

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

        private void MonitorMinimize_Click(object sender, RoutedEventArgs e)
        {
            var target = _monitorPanelLayout == MonitorPanelLayout.Minimized
                ? MonitorPanelLayout.Normal
                : MonitorPanelLayout.Minimized;

            ApplyMonitorPanelLayout(target);
        }

        private void MonitorMaximize_Click(object sender, RoutedEventArgs e)
        {
            var target = _monitorPanelLayout == MonitorPanelLayout.Maximized
                ? MonitorPanelLayout.Normal
                : MonitorPanelLayout.Maximized;

            ApplyMonitorPanelLayout(target);
        }

        private void ApplyMonitorPanelLayout(MonitorPanelLayout layout)
        {
            _monitorPanelLayout = layout;

            switch (layout)
            {
                case MonitorPanelLayout.Minimized:
                    EntriesColumn.Width = new GridLength(1, GridUnitType.Star);
                    CenterSpacerColumn.Width = new GridLength(8, GridUnitType.Pixel);
                    MonitorColumn.Width = new GridLength(280, GridUnitType.Pixel);
                    StatusMessage = "Painel de monitoramento minimizado";
                    break;

                case MonitorPanelLayout.Maximized:
                    EntriesColumn.Width = new GridLength(1, GridUnitType.Star);
                    CenterSpacerColumn.Width = new GridLength(10, GridUnitType.Pixel);
                    MonitorColumn.Width = new GridLength(2, GridUnitType.Star);
                    StatusMessage = "Painel de monitoramento maximizado";
                    break;

                default:
                    EntriesColumn.Width = new GridLength(2, GridUnitType.Star);
                    CenterSpacerColumn.Width = new GridLength(10, GridUnitType.Pixel);
                    MonitorColumn.Width = new GridLength(1, GridUnitType.Star);
                    StatusMessage = "Painel de monitoramento restaurado";
                    break;
            }
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.ContextMenu == null)
            {
                return;
            }

            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }

        private void MainMenuHome_Click(object sender, RoutedEventArgs e)
        {
            _selectedCategoryFilter = null;
            _selectedGroupKeyFilter = null;
            GroupFilterInfoText = string.Empty;
            GroupPanelVisibility = Visibility.Collapsed;

            if (!string.Equals(SelectedAnalysisFilter, "Todos", StringComparison.OrdinalIgnoreCase))
            {
                SelectedAnalysisFilter = "Todos";
            }

            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                FilterText = string.Empty;
            }
            else
            {
                ApplyFilter();
            }

            ApplyMonitorPanelLayout(MonitorPanelLayout.Normal);
            if (MainMonitorTabs != null)
            {
                MainMonitorTabs.SelectedIndex = 0;
            }

            M3UUrlComboBox?.Focus();
            StatusMessage = "Inicio carregado.";
        }

        #region Chrome Tabs System
        
        private void NewTab_Click(object sender, RoutedEventArgs e)
        {
            var newTab = new TabItem 
            { 
                Header = $"Aba {ChromeTabControl.Items.Count + 1}",
                Content = new System.Windows.Controls.TextBlock 
                { 
                    Text = "Nova aba criada. Selecione um módulo no menu.", 
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Colors.Gray)
                }
            };
            ChromeTabControl.Items.Add(newTab);
            ChromeTabControl.SelectedItem = newTab;
            StatusMessage = "Nova aba criada.";
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn)
            {
                // Find the parent TabItem
                var tabItem = FindParent<TabItem>(btn);
                if (tabItem != null && ChromeTabControl.Items.Count > 1)
                {
                    CleanupEmbeddedProcessForTab(tabItem);
                    ChromeTabControl.Items.Remove(tabItem);
                    StatusMessage = "Aba fechada.";
                }
                else if (ChromeTabControl.Items.Count == 1)
                {
                    System.Windows.MessageBox.Show("Não é possível fechar a última aba.", "Aviso", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void NewWindow_Click(object sender, RoutedEventArgs e)
        {
            var newWindow = new MainWindow();
            newWindow.Show();
            StatusMessage = "Nova janela aberta.";
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }

        #endregion

        private void MainMenuConnectXuiOne_Click(object sender, RoutedEventArgs e)
        {
            var saved = LoadXuiOneConnectionConfig();

            var window = new System.Windows.Window
            {
                Title = "Conecta XUI-ONE (MariaDB)",
                Width = 520,
                Height = 370,
                MinWidth = 480,
                MinHeight = 340,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(246, 248, 252))
            };

            var root = new Grid { Margin = new Thickness(14) };
            for (var i = 0; i < 8; i++)
            {
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            AddConnectionField(root, 0, "Host:", out var hostBox, saved?.Host ?? "127.0.0.1");
            AddConnectionField(root, 1, "Porta:", out var portBox, saved?.Port.ToString() ?? "3306");
            AddConnectionField(root, 2, "Banco:", out var databaseBox, saved?.Database ?? "xui" );
            AddConnectionField(root, 3, "Usuario:", out var userBox, saved?.User ?? "root");

            var passwordLabel = new TextBlock
            {
                Text = "Senha:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 6),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(passwordLabel, 4);
            root.Children.Add(passwordLabel);

            var passwordBox = new PasswordBox
            {
                Password = saved?.Password ?? string.Empty,
                Height = 28,
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(passwordBox, 4);
            Grid.SetColumn(passwordBox, 1);
            root.Children.Add(passwordBox);

            var info = new TextBlock
            {
                Text = "Conexao para envio de conteudos ao XUI-ONE. Os dados ficam salvos localmente no seu perfil.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 80, 95)),
                Margin = new Thickness(0, 8, 0, 10)
            };
            Grid.SetRow(info, 6);
            Grid.SetColumnSpan(info, 2);
            root.Children.Add(info);

            var statusText = new TextBlock
            {
                Text = "Preencha os dados e clique em Testar.",
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 55, 70)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(statusText, 7);
            Grid.SetColumnSpan(statusText, 2);
            root.Children.Add(statusText);

            var actions = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };

            var testButton = new System.Windows.Controls.Button { Content = "Testar", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            var saveButton = new System.Windows.Controls.Button { Content = "Salvar", Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            var cancelButton = new System.Windows.Controls.Button { Content = "Fechar", Width = 90 };

            actions.Children.Add(testButton);
            actions.Children.Add(saveButton);
            actions.Children.Add(cancelButton);

            Grid.SetRow(actions, 9);
            Grid.SetColumnSpan(actions, 2);
            root.Children.Add(actions);

            window.Content = root;

            async Task<(bool ok, string message)> TestConnectionAsync()
            {
                if (!uint.TryParse(portBox.Text.Trim(), out var port))
                {
                    return (false, "Porta invalida.");
                }

                var host = hostBox.Text.Trim();
                var database = databaseBox.Text.Trim();
                var user = userBox.Text.Trim();
                var password = passwordBox.Password;

                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(user))
                {
                    return (false, "Host, banco e usuario sao obrigatorios.");
                }

                try
                {
                    var connectionString = BuildXuiOneConnectionString(host, port, database, user, password);
                    await using var conn = new MySqlConnection(connectionString);
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await conn.OpenAsync(cts.Token);

                    await using var cmd = new MySqlCommand("SELECT VERSION();", conn);
                    var version = Convert.ToString(await cmd.ExecuteScalarAsync(cts.Token)) ?? "desconhecida";
                    return (true, $"Conexao OK. MariaDB/MySQL: {version}");
                }
                catch (Exception ex)
                {
                    return (false, $"Falha na conexao: {ex.Message}");
                }
            }

            testButton.Click += async (_, _) =>
            {
                testButton.IsEnabled = false;
                saveButton.IsEnabled = false;
                statusText.Text = "Testando conexao...";

                var result = await TestConnectionAsync();
                statusText.Text = result.message;
                statusText.Foreground = new SolidColorBrush(result.ok ? System.Windows.Media.Color.FromRgb(30, 120, 52) : System.Windows.Media.Color.FromRgb(185, 40, 40));

                testButton.IsEnabled = true;
                saveButton.IsEnabled = true;
            };

            saveButton.Click += async (_, _) =>
            {
                testButton.IsEnabled = false;
                saveButton.IsEnabled = false;
                statusText.Text = "Validando e salvando conexao...";

                var result = await TestConnectionAsync();
                if (!result.ok)
                {
                    statusText.Text = result.message;
                    statusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(185, 40, 40));
                    testButton.IsEnabled = true;
                    saveButton.IsEnabled = true;
                    return;
                }

                var config = new XuiOneConnectionConfig
                {
                    Host = hostBox.Text.Trim(),
                    Port = uint.TryParse(portBox.Text.Trim(), out var parsedPort) ? parsedPort : 3306,
                    Database = databaseBox.Text.Trim(),
                    User = userBox.Text.Trim(),
                    PasswordProtected = ProtectSecret(passwordBox.Password)
                };

                SaveXuiOneConnectionConfig(config);

                statusText.Text = "Conexao salva com sucesso.";
                statusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 120, 52));
                StatusMessage = "Conexao XUI-ONE salva e validada.";
                await Task.Delay(400);
                window.Close();
            };

            cancelButton.Click += (_, _) => window.Close();

            window.ShowDialog();
        }

        private void MainMenuIpPort_Click(object sender, RoutedEventArgs e)
        {
            MainMenuIpPortPlaylistFinderInApp_Click(sender, e);
        }

        private async void MainMenuIpPortPlaylistFinderInApp_Click(object sender, RoutedEventArgs e)
        {
            var executablePath = ResolvePlaylistFinderExecutablePath();
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                var rootPath = ResolveIpPortRootDirectory() ?? "(nao localizado)";
                System.Windows.MessageBox.Show(
                    "playlistfinder.app.exe nao foi encontrado no pacote integrado do aplicativo.\n\nBase procurada:\n" + rootPath,
                    "IP E PORTA",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            await OpenEmbeddedExecutableTabAsync("playlistfinder.app", executablePath);
            StatusMessage = "playlistfinder.app aberto dentro do sistema.";
        }

        private void MainMenuIpPortPlaylistFinder_Click(object sender, RoutedEventArgs e)
        {
            var executablePath = ResolvePlaylistFinderExecutablePath();
            if (!TryOpenLocalPath(executablePath, "IP E PORTA", "playlistfinder.app.exe nao foi encontrado no pacote integrado do aplicativo."))
            {
                return;
            }

            StatusMessage = "playlistfinder.app aberto com sucesso.";
        }

        private void MainMenuIpPortAppFolder_Click(object sender, RoutedEventArgs e)
        {
            var rootPath = ResolveIpPortRootDirectory();
            if (!TryOpenLocalPath(rootPath, "IP E PORTA", "Pasta 'ip e porta' nao encontrada."))
            {
                return;
            }

            StatusMessage = "Pasta do aplicativo IP E PORTA aberta.";
        }

        private void MainMenuIpPortCourseFolder_Click(object sender, RoutedEventArgs e)
        {
            var coursePath = ResolveIpPortCourseDirectory();
            if (!TryOpenLocalPath(coursePath, "IP E PORTA", "Pasta do curso nao encontrada dentro de 'ip e porta'."))
            {
                return;
            }

            StatusMessage = "Conteudos do curso abertos.";
        }

        private void MainMenuIpPortAulaCompleta_Click(object sender, RoutedEventArgs e)
        {
            var coursePath = ResolveIpPortCourseDirectory();
            var filePath = ResolveFirstFile(coursePath, "AULA COMPLETA*.avi");
            if (!TryOpenLocalPath(filePath, "IP E PORTA", "Arquivo 'AULA COMPLETA.avi' nao encontrado."))
            {
                return;
            }

            StatusMessage = "Aula completa aberta.";
        }

        private void MainMenuIpPortAulaResumida_Click(object sender, RoutedEventArgs e)
        {
            var coursePath = ResolveIpPortCourseDirectory();
            var filePath = ResolveFirstFile(coursePath, "AULA RESUMIDA*.mp4");
            if (!TryOpenLocalPath(filePath, "IP E PORTA", "Arquivo 'AULA RESUMIDA.mp4' nao encontrado."))
            {
                return;
            }

            StatusMessage = "Aula resumida aberta.";
        }

        private void MainMenuIpPortM3uExample_Click(object sender, RoutedEventArgs e)
        {
            var coursePath = ResolveIpPortCourseDirectory();
            var filePath = ResolveFirstFile(coursePath, "ARQUIVO*.m3u");
            if (!TryOpenLocalPath(filePath, "IP E PORTA", "Arquivo M3U de exemplo nao encontrado."))
            {
                return;
            }

            StatusMessage = "Arquivo M3U de exemplo aberto.";
        }

        private string? ResolveIpPortRootDirectory()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, BundledIpPortFolderName),
                Path.Combine(Environment.CurrentDirectory, BundledIpPortFolderName),
                Path.Combine(AppContext.BaseDirectory, "ip e porta"),
                Path.Combine(Environment.CurrentDirectory, "ip e porta"),
                Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")), "ip e porta"),
                Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..")), "ip e porta")
            };

            return candidates.FirstOrDefault(Directory.Exists);
        }

        private string? ResolveIpPortCourseDirectory()
        {
            var rootPath = ResolveIpPortRootDirectory();
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return null;
            }

            return Directory.GetDirectories(rootPath, "CURSO*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
        }

        private string? ResolvePlaylistFinderExecutablePath()
        {
            var rootPath = ResolveIpPortRootDirectory();
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return null;
            }

            var direct = Path.Combine(rootPath, PlaylistFinderExecutableFileName);
            if (File.Exists(direct))
            {
                return direct;
            }

            return ResolveFirstFile(rootPath, "playlistfinder*.exe", SearchOption.AllDirectories);
        }

        private static string? ResolveFirstFile(string? baseDirectory, string pattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
            {
                return null;
            }

            return Directory.GetFiles(baseDirectory, pattern, searchOption).FirstOrDefault();
        }

        private bool TryOpenLocalPath(string? path, string title, string missingMessage)
        {
            if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
            {
                var rootPath = ResolveIpPortRootDirectory() ?? "(nao localizado)";
                System.Windows.MessageBox.Show(
                    missingMessage + "\n\nBase procurada:\n" + rootPath,
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });

            return true;
        }

        private void MainMenuYouTubeToM3u_Click(object sender, RoutedEventArgs e)
        {
            var window = new System.Windows.Window
            {
                Title = "YouTube para M3U",
                Width = 700,
                Height = 520,
                MinWidth = 640,
                MinHeight = 460,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(246, 248, 252))
            };

            var root = new Grid { Margin = new Thickness(14) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            AddConnectionField(root, 0, "Nome da lista:", out var listNameBox, "YouTube Playlist");
            AddConnectionField(root, 1, "Grupo M3U:", out var groupBox, "YouTube | Conteudos");

            var outputLabel = new TextBlock
            {
                Text = "Arquivo saida:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 6),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(outputLabel, 2);
            root.Children.Add(outputLabel);

            var outputRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            outputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var outputPathBox = new System.Windows.Controls.TextBox
            {
                Text = Path.Combine(DownloadPath, $"youtube_{DateTime.Now:yyyyMMdd_HHmm}.m3u"),
                Height = 28
            };
            outputRow.Children.Add(outputPathBox);

            var browseButton = new System.Windows.Controls.Button
            {
                Content = "Procurar",
                Width = 90,
                Margin = new Thickness(8, 0, 0, 0)
            };
            browseButton.Click += (_, _) =>
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Playlist M3U (*.m3u)|*.m3u|Todos os arquivos (*.*)|*.*",
                    FileName = Path.GetFileName(outputPathBox.Text),
                    InitialDirectory = Path.GetDirectoryName(outputPathBox.Text)
                };

                if (dialog.ShowDialog() == true)
                {
                    outputPathBox.Text = dialog.FileName;
                }
            };
            Grid.SetColumn(browseButton, 1);
            outputRow.Children.Add(browseButton);

            Grid.SetRow(outputRow, 2);
            Grid.SetColumn(outputRow, 1);
            root.Children.Add(outputRow);

            var urlsLabel = new TextBlock
            {
                Text = "Links YouTube:",
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 8, 6),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(urlsLabel, 3);
            root.Children.Add(urlsLabel);

            var urlsBox = new System.Windows.Controls.TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 220
            };
            Grid.SetRow(urlsBox, 4);
            Grid.SetColumn(urlsBox, 1);
            root.Children.Add(urlsBox);

            var info = new TextBlock
            {
                Text = "Use 1 URL por linha. Opcional: Titulo|URL para nome personalizado no M3U.",
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 80, 95)),
                Margin = new Thickness(0, 8, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(info, 5);
            Grid.SetColumnSpan(info, 2);
            root.Children.Add(info);

            var actions = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };

            var generateButton = new System.Windows.Controls.Button { Content = "Gerar M3U", Width = 110, Margin = new Thickness(0, 0, 8, 0) };
            var closeButton = new System.Windows.Controls.Button { Content = "Fechar", Width = 90 };
            actions.Children.Add(generateButton);
            actions.Children.Add(closeButton);
            Grid.SetRow(actions, 6);
            Grid.SetColumnSpan(actions, 2);
            root.Children.Add(actions);

            generateButton.Click += (_, _) =>
            {
                var groupTitle = string.IsNullOrWhiteSpace(groupBox.Text) ? "YouTube | Conteudos" : groupBox.Text.Trim();
                var listName = string.IsNullOrWhiteSpace(listNameBox.Text) ? "YouTube Playlist" : listNameBox.Text.Trim();
                var outputPath = outputPathBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    System.Windows.MessageBox.Show("Informe o arquivo de saida.", "YouTube para M3U", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var rows = urlsBox.Text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (rows.Count == 0)
                {
                    System.Windows.MessageBox.Show("Adicione pelo menos um link do YouTube.", "YouTube para M3U", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var lines = new List<string>
                {
                    "#EXTM3U",
                    $"#PLAYLIST:{EscapeM3uAttribute(listName)}"
                };

                var added = 0;
                var skipped = 0;

                foreach (var row in rows)
                {
                    var title = string.Empty;
                    var url = row;

                    var separatorIndex = row.IndexOf('|');
                    if (separatorIndex > 0)
                    {
                        title = row[..separatorIndex].Trim();
                        url = row[(separatorIndex + 1)..].Trim();
                    }

                    if (!Uri.TryCreate(url, UriKind.Absolute, out _) || !IsYouTubeUrl(url))
                    {
                        skipped++;
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(title))
                    {
                        title = $"YouTube Item {added + 1}";
                    }

                    var safeTitle = EscapeM3uAttribute(title);
                    var safeGroup = EscapeM3uAttribute(groupTitle);

                    lines.Add($"#EXTINF:-1 tvg-name=\"{safeTitle}\" group-title=\"{safeGroup}\",{safeTitle}");
                    lines.Add(url);
                    added++;
                }

                if (added == 0)
                {
                    System.Windows.MessageBox.Show("Nenhum link valido de YouTube foi encontrado.", "YouTube para M3U", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllLines(outputPath, lines);

                StatusMessage = $"M3U YouTube gerado: {added} link(s), {skipped} ignorado(s).";
                var openNow = System.Windows.MessageBox.Show(
                    $"Arquivo criado com sucesso.\n\nAdicionados: {added}\nIgnorados: {skipped}\n\nAbrir no Bloco de Notas agora?",
                    "YouTube para M3U",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (openNow == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "notepad.exe",
                        Arguments = $"\"{outputPath}\"",
                        UseShellExecute = true
                    });
                }
            };

            closeButton.Click += (_, _) => window.Close();

            window.Content = root;
            window.ShowDialog();
        }

        private void MainMenuDarkM3uChecker_Click(object sender, RoutedEventArgs e)
        {
            if (TryOpenIntegratedHtml("DARK M3U CHECKER", DarkBulletHtmlFileName))
            {
                return;
            }

            System.Windows.MessageBox.Show(
                "Arquivo HTML do DARK M3U CHECKER nao encontrado:\n- " + DarkBulletHtmlFileName,
                "DARK M3U CHECKER",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            StatusMessage = "Modulo DARK M3U CHECKER sem HTML integrado no momento.";
        }

        private void MainMenuLisoFlix_Click(object sender, RoutedEventArgs e)
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, LisoFlixHtmlFileName),
                Path.Combine(Environment.CurrentDirectory, LisoFlixHtmlFileName),
                Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")), LisoFlixHtmlFileName)
            };

            var filePath = candidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                System.Windows.MessageBox.Show(
                    "Arquivo HTML do modulo LisoFlix nao encontrado:\n- " + LisoFlixHtmlFileName,
                    "LisoFlix",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                StatusMessage = "Modulo LisoFlix sem HTML integrado no momento.";
                return;
            }

            // Abre LisoFlix em uma aba interna
            OpenModuleInTab("LisoFlix", filePath);
        }

        private void MainMenuDrmPlayer_Click(object sender, RoutedEventArgs e)
        {
            if (TryOpenIntegratedHtml("drm-player", DrmPlayerHtmlFileName))
            {
                return;
            }

            System.Windows.MessageBox.Show(
                "Arquivo HTML do modulo drm-player nao encontrado:\n- " + DrmPlayerHtmlFileName,
                "drm-player",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            StatusMessage = "Modulo drm-player sem HTML integrado no momento.";
        }

        private void MainMenuBotIptv_Click(object sender, RoutedEventArgs e)
        {
            if (TryOpenIntegratedHtml("bot-iptv", BotIptvHtmlFileName))
            {
                return;
            }

            System.Windows.MessageBox.Show(
                "Arquivo HTML do modulo bot-iptv nao encontrado:\n- " + BotIptvHtmlFileName,
                "bot-iptv",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            StatusMessage = "Modulo bot-iptv sem HTML integrado no momento.";
        }

        private void CastToDevice_Click(object sender, RoutedEventArgs e)
        {
            // Verifica se há itens selecionados
            var selectedEntries = EntriesList?.SelectedItems.Cast<M3UEntry>().ToList();
            
            if (selectedEntries == null || selectedEntries.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "Selecione pelo menos um item para fazer cast.\n\n" +
                    "Dica: Use o checkbox ao lado do nome do canal/VOD.",
                    "Cast para Dispositivos",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Abre janela de seleção de dispositivos
            OpenCastDeviceWindow(selectedEntries);
        }

        private void OpenCastDeviceWindow(List<M3UEntry> entries)
        {
            var castWindow = new System.Windows.Window
            {
                Title = "Cast para Dispositivos na Rede",
                Width = 600,
                Height = 500,
                MinWidth = 500,
                MinHeight = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 245))
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var headerPanel = new System.Windows.Controls.StackPanel 
            { 
                Orientation = System.Windows.Controls.Orientation.Vertical,
                Margin = new Thickness(20, 20, 20, 10)
            };
            
            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = "📺 Cast para Dispositivos",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 33, 33))
            };
            
            var subtitleText = new System.Windows.Controls.TextBlock
            {
                Text = $"{entries.Count} item(s) selecionado(s) para reprodução",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 100)),
                Margin = new Thickness(0, 5, 0, 0)
            };
            
            headerPanel.Children.Add(titleText);
            headerPanel.Children.Add(subtitleText);
            Grid.SetRow(headerPanel, 0);

            // Lista de dispositivos simulada (em produção, usar UPnP/DLNA discovery)
            var devicesListBox = new System.Windows.Controls.ListBox
            {
                Margin = new Thickness(20, 10, 20, 10),
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };

            // Dispositivos de exemplo (em produção, isso seria dinâmico)
            var devices = new[]
            {
                "📱 Smart TV - Sala (192.168.1.100)",
                "📺 TV Quarto - Samsung (192.168.1.105)",
                "🔥 Chromecast - Cozinha (192.168.1.110)",
                "📻 Roku - Escritório (192.168.1.115)",
                "🎮 Xbox Series X (192.168.1.120)",
                "🖥️ PC - Escritório (192.168.1.50)"
            };

            foreach (var device in devices)
            {
                devicesListBox.Items.Add(device);
            }
            Grid.SetRow(devicesListBox, 1);

            // Botões de ação
            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(20)
            };

            var scanButton = new System.Windows.Controls.Button
            {
                Content = "🔍 Escanear Rede",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243)),
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold
            };
            scanButton.Click += (s, ev) =>
            {
                System.Windows.MessageBox.Show(
                    "Escaneando dispositivos na rede...\n\n" +
                    "Funcionalidade em desenvolvimento.\n" +
                    "Na versão completa, buscaria dispositivos DLNA/UPnP.",
                    "Escanear Rede",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            };

            var castButton = new System.Windows.Controls.Button
            {
                Content = "▶️ Iniciar Cast",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)),
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.SemiBold
            };
            castButton.Click += (s, ev) =>
            {
                if (devicesListBox.SelectedItem == null)
                {
                    System.Windows.MessageBox.Show(
                        "Selecione um dispositivo para fazer cast.",
                        "Cast",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var selectedDevice = devicesListBox.SelectedItem.ToString();
                var firstEntry = entries.First();
                
                System.Windows.MessageBox.Show(
                    $"Iniciando cast para:\n{selectedDevice}\n\n" +
                    $"Conteúdo: {firstEntry.Name}\n" +
                    $"URL: {firstEntry.Url}\n\n" +
                    "Na versão completa, enviaria o comando de reprodução para o dispositivo.",
                    "Cast Iniciado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Cancelar",
                Padding = new Thickness(15, 8, 15, 8),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158)),
                Foreground = System.Windows.Media.Brushes.White
            };
            cancelButton.Click += (s, ev) => castWindow.Close();

            buttonPanel.Children.Add(scanButton);
            buttonPanel.Children.Add(castButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);

            mainGrid.Children.Add(headerPanel);
            mainGrid.Children.Add(devicesListBox);
            mainGrid.Children.Add(buttonPanel);

            castWindow.Content = mainGrid;
            castWindow.ShowDialog();
        }

        private void OpenLisoFlixInWebView(string htmlPath)
        {
            var window = new System.Windows.Window
            {
                Title = "LisoFlix Pro - Player Interno",
                Width = 1280,
                Height = 800,
                MinWidth = 800,
                MinHeight = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = System.Windows.Media.Brushes.Black
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Barra de ferramentas superior
            var toolbar = new System.Windows.Controls.StackPanel 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 20, 20)),
                Height = 40
            };
            
            var btnVoltar = new System.Windows.Controls.Button 
            { 
                Content = "← Voltar", 
                Width = 100, 
                Margin = new Thickness(5),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 9, 20)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0)
            };
            btnVoltar.Click += (_, _) => window.Close();
            
            var btnRecarregar = new System.Windows.Controls.Button 
            { 
                Content = "↻ Recarregar", 
                Width = 100, 
                Margin = new Thickness(5),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0)
            };
            
            var btnHttp = new System.Windows.Controls.Button 
            { 
                Content = "🔓 Permitir HTTP", 
                Width = 130, 
                Margin = new Thickness(5),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                ToolTip = "Clique se o conteúdo não carregar (permite conteúdo HTTP misto)"
            };
            
            toolbar.Children.Add(btnVoltar);
            toolbar.Children.Add(btnRecarregar);
            toolbar.Children.Add(btnHttp);
            
            Grid.SetRow(toolbar, 0);
            grid.Children.Add(toolbar);

            // WebView2
            var webView = new Microsoft.Web.WebView2.Wpf.WebView2();
            webView.CreationProperties = new Microsoft.Web.WebView2.Wpf.CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeuGestorVODs", "WebView2Data")
            };
            
            Grid.SetRow(webView, 1);
            grid.Children.Add(webView);

            window.Content = grid;

            // Inicializa WebView2 com suporte a HTTP
            var envOptions = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions
            {
                AllowSingleSignOnUsingOSPrimaryAccount = false,
                AdditionalBrowserArguments = "--disable-features=AutoupgradeMixedContent --allow-running-insecure-content --disable-web-security --ignore-certificate-errors"
            };
            
            var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeuGestorVODs", "WebView2Data");
            
            Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataFolder, envOptions)
                .ContinueWith(envTask =>
                {
                    var environment = envTask.Result;
                    Dispatcher.Invoke(async () =>
                    {
                        try
                        {
                            await webView.EnsureCoreWebView2Async(environment);
                            
                            // Configurações para permitir HTTP
                            webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                            webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                            webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                            webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true;
                            webView.CoreWebView2.Settings.IsZoomControlEnabled = true;
                            
                            // Desabilita restrições de conteúdo misto (HTTP em HTTPS)
                            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                            
                            // Navega para o arquivo HTML local
                            webView.Source = new Uri(htmlPath);
                            
                            StatusMessage = "LisoFlix aberto em player interno (HTTP/HTTPS habilitado)";
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show(
                                $"Erro ao carregar LisoFlix: {ex.Message}",
                                "Erro",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    });
                });

            btnRecarregar.Click += (_, _) => webView.Reload();
            
            btnHttp.Click += (_, _) =>
            {
                // Mostra aviso sobre segurança e permite HTTP
                var result = System.Windows.MessageBox.Show(
                    "ATENÇÃO: Permitir conteúdo HTTP não criptografado pode representar risco de segurança.\n\n" +
                    "Use esta opção apenas se o conteúdo não estiver carregando e você confiar na fonte.\n\n" +
                    "Deseja continuar?",
                    "Permitir HTTP",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    // Recarrega com flags menos restritivas
                    webView.Reload();
                    StatusMessage = "Modo HTTP misto ativado para LisoFlix";
                }
            };

            window.Show();
        }

        private bool TryOpenIntegratedHtml(string moduleName, string fileName)
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, fileName),
                Path.Combine(Environment.CurrentDirectory, fileName),
                Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")), fileName)
            };

            var filePath = candidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            // Abre em uma aba interna ao invés do navegador externo
            OpenModuleInTab(moduleName, filePath);

            StatusMessage = $"Modulo {moduleName} aberto na aba: {Path.GetFileName(filePath)}";
            return true;
        }

        private void OpenModuleInTab(string moduleName, string htmlPath)
        {
            // Verifica se já existe uma aba com este módulo
            foreach (TabItem existingTab in ChromeTabControl.Items)
            {
                if (existingTab.Header.ToString() == moduleName)
                {
                    // Seleciona a aba existente
                    ChromeTabControl.SelectedItem = existingTab;
                    return;
                }
            }

            // Cria nova aba
            var newTab = new TabItem 
            { 
                Header = moduleName,
                Tag = htmlPath
            };

            // Cria o container com WebView2
            var grid = new Grid();
            grid.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            grid.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Barra de ferramentas
            var toolbar = new System.Windows.Controls.StackPanel 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)),
                Height = 35
            };

            // WebView2 (declarar antes dos event handlers)
            var webView = new WebView2();
            webView.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            webView.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;

            var btnVoltar = new System.Windows.Controls.Button 
            { 
                Content = "← Voltar", 
                Foreground = System.Windows.Media.Brushes.White, 
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(5, 0, 0, 0)
            };
            btnVoltar.Click += (s, e) => 
            {
                if (webView.CoreWebView2 != null)
                    webView.CoreWebView2.GoBack();
            };

            var btnRecarregar = new System.Windows.Controls.Button 
            { 
                Content = "↻ Recarregar", 
                Foreground = System.Windows.Media.Brushes.White, 
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 5, 10, 5)
            };
            btnRecarregar.Click += (s, e) => webView.Reload();

            var btnPermitirHttp = new System.Windows.Controls.Button 
            { 
                Content = "🔓 Permitir HTTP", 
                Foreground = System.Windows.Media.Brushes.White, 
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 5, 10, 5)
            };
            btnPermitirHttp.Click += (s, e) =>
            {
                if (webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.Navigate(webView.Source.ToString());
                    System.Windows.MessageBox.Show("Página recarregada com permissões HTTP ativadas.");
                }
            };

            toolbar.Children.Add(btnVoltar);
            toolbar.Children.Add(btnRecarregar);
            toolbar.Children.Add(btnPermitirHttp);
            Grid.SetRow(toolbar, 0);

            // Inicializa o WebView2
            InitializeWebView(webView, htmlPath);
            Grid.SetRow(webView, 1);

            grid.Children.Add(toolbar);
            grid.Children.Add(webView);
            newTab.Content = grid;

            // Adiciona a aba e seleciona
            ChromeTabControl.Items.Add(newTab);
            ChromeTabControl.SelectedItem = newTab;

            StatusMessage = $"Módulo {moduleName} aberto em nova aba.";
        }

        private async Task OpenEmbeddedExecutableTabAsync(string moduleName, string executablePath)
        {
            var existing = ChromeTabControl.Items
                .OfType<TabItem>()
                .FirstOrDefault(t => string.Equals(t.Header?.ToString(), moduleName, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                if (_embeddedTabProcesses.TryGetValue(existing, out var runningProcess) && !runningProcess.HasExited)
                {
                    ChromeTabControl.SelectedItem = existing;
                    Dispatcher.BeginInvoke(new Action(() => ResizeEmbeddedTab(existing)), DispatcherPriority.Background);
                    return;
                }

                CleanupEmbeddedProcessForTab(existing);
                ChromeTabControl.Items.Remove(existing);
            }

            var tab = new TabItem
            {
                Header = moduleName,
                Tag = executablePath
            };

            var grid = new Grid();
            var host = new System.Windows.Forms.Integration.WindowsFormsHost();
            var panel = new System.Windows.Forms.Panel
            {
                Dock = System.Windows.Forms.DockStyle.Fill
            };

            host.Child = panel;
            grid.Children.Add(host);
            tab.Content = grid;
            _embeddedTabHosts[tab] = panel;

            ChromeTabControl.Items.Add(tab);
            ChromeTabControl.SelectedItem = tab;

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
                UseShellExecute = true
            });

            if (process == null)
            {
                ChromeTabControl.Items.Remove(tab);
                System.Windows.MessageBox.Show(
                    "Nao foi possivel iniciar o playlistfinder.app.",
                    "IP E PORTA",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            _embeddedTabProcesses[tab] = process;

            var attached = await WaitAndAttachWindowAsync(process, panel);
            if (!attached)
            {
                CleanupEmbeddedProcessForTab(tab);
                ChromeTabControl.Items.Remove(tab);
                System.Windows.MessageBox.Show(
                    "Nao foi possivel anexar a janela do playlistfinder.app dentro da aba.",
                    "IP E PORTA",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            panel.Resize += (_, _) => ResizeEmbeddedWindow(process, panel);
            Dispatcher.BeginInvoke(new Action(() => ResizeEmbeddedWindow(process, panel)), DispatcherPriority.Background);
        }

        private async Task<bool> WaitAndAttachWindowAsync(Process process, System.Windows.Forms.Panel hostPanel)
        {
            try
            {
                for (var i = 0; i < 100; i++)
                {
                    await Task.Delay(120);

                    if (process.HasExited)
                    {
                        return false;
                    }

                    process.Refresh();
                    var windowHandle = process.MainWindowHandle;
                    if (windowHandle == IntPtr.Zero || !hostPanel.IsHandleCreated)
                    {
                        continue;
                    }

                    SetParent(windowHandle, hostPanel.Handle);

                    var style = GetWindowLong(windowHandle, GwlStyle);
                    style &= ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu);
                    style |= WsChild | WsVisible;
                    SetWindowLong(windowHandle, GwlStyle, style);

                    ResizeEmbeddedWindow(process, hostPanel);
                    SetWindowPos(
                        windowHandle,
                        IntPtr.Zero,
                        0,
                        0,
                        Math.Max(hostPanel.ClientSize.Width, 200),
                        Math.Max(hostPanel.ClientSize.Height, 120),
                        SwpNoZOrder | SwpNoActivate | SwpFrameChanged | SwpShowWindow);
                    ShowWindow(windowHandle, SwShow);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static void ResizeEmbeddedWindow(Process process, System.Windows.Forms.Panel hostPanel)
        {
            if (process.HasExited)
            {
                return;
            }

            process.Refresh();
            var windowHandle = process.MainWindowHandle;
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            var width = Math.Max(hostPanel.ClientSize.Width, 200);
            var height = Math.Max(hostPanel.ClientSize.Height, 120);
            MoveWindow(windowHandle, 0, 0, width, height, true);
            SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, width, height, SwpNoZOrder | SwpNoActivate | SwpShowWindow);
        }

        private void CleanupEmbeddedProcessForTab(TabItem tab)
        {
            if (!_embeddedTabProcesses.TryGetValue(tab, out var process))
            {
                _embeddedTabHosts.Remove(tab);
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(1500))
                    {
                        process.Kill(true);
                    }
                }
            }
            catch
            {
            }

            process.Dispose();
            _embeddedTabProcesses.Remove(tab);
            _embeddedTabHosts.Remove(tab);
        }

        private void ResizeEmbeddedTab(TabItem tab)
        {
            if (_embeddedTabProcesses.TryGetValue(tab, out var process) &&
                _embeddedTabHosts.TryGetValue(tab, out var hostPanel))
            {
                ResizeEmbeddedWindow(process, hostPanel);
            }
        }

        private void ResizeSelectedEmbeddedTab()
        {
            if (ChromeTabControl.SelectedItem is TabItem selectedTab)
            {
                ResizeEmbeddedTab(selectedTab);
            }
        }

        private void CleanupAllEmbeddedProcesses()
        {
            foreach (var tab in _embeddedTabProcesses.Keys.ToList())
            {
                CleanupEmbeddedProcessForTab(tab);
            }
        }

        private void InitializeWebView(WebView2 webView, string htmlPath)
        {
            try
            {
                // Configura o WebView2
                webView.CreationProperties = new CoreWebView2CreationProperties
                {
                    UserDataFolder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MeuGestorVODs",
                        "WebView2Data",
                        DateTime.Now.Ticks.ToString())
                };

                // Evento quando o CoreWebView2 estiver pronto
                webView.CoreWebView2InitializationCompleted += (s, e) =>
                {
                    if (e.IsSuccess && webView.CoreWebView2 != null)
                    {
                        // Configurações para permitir conteúdo HTTP e conteúdo misto
                        webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                        webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                        webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                        
                        // Navega para URL externa ou arquivo local
                        if (Uri.TryCreate(htmlPath, UriKind.Absolute, out var destinationUri) &&
                            (destinationUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                             destinationUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                             destinationUri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)))
                        {
                            webView.CoreWebView2.Navigate(destinationUri.AbsoluteUri);
                        }
                        else
                        {
                            var fullPath = Path.GetFullPath(htmlPath);
                            webView.CoreWebView2.Navigate($"file:///{fullPath.Replace("\\", "/")}");
                        }
                    }
                    else if (e.InitializationException != null)
                    {
                        System.Windows.MessageBox.Show($"Erro ao inicializar WebView2: {e.InitializationException.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                // Inicializa o CoreWebView2
                webView.EnsureCoreWebView2Async();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao configurar WebView2: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool IsYouTubeUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            var host = uri.Host.ToLowerInvariant();
            return host.Contains("youtube.com") || host.Contains("youtu.be");
        }

        private static void AddConnectionField(Grid root, int row, string label, out System.Windows.Controls.TextBox textBox, string initialValue)
        {
            var textLabel = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 6),
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(textLabel, row);
            root.Children.Add(textLabel);

            textBox = new System.Windows.Controls.TextBox
            {
                Text = initialValue,
                Height = 28,
                Margin = new Thickness(0, 0, 0, 6)
            };

            Grid.SetRow(textBox, row);
            Grid.SetColumn(textBox, 1);
            root.Children.Add(textBox);
        }

        private static string BuildXuiOneConnectionString(string host, uint port, string database, string user, string password)
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = host,
                Port = port,
                Database = database,
                UserID = user,
                Password = password,
                CharacterSet = "utf8mb4",
                SslMode = MySqlSslMode.Preferred,
                ConnectionTimeout = 8,
                DefaultCommandTimeout = 10,
                AllowUserVariables = true
            };

            return builder.ConnectionString;
        }

        private XuiOneConnectionConfig? LoadXuiOneConnectionConfig()
        {
            try
            {
                var path = GetXuiOneConnectionFilePath();
                if (!File.Exists(path))
                {
                    return null;
                }

                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<XuiOneConnectionConfig>(json);
                if (config == null)
                {
                    return null;
                }

                config.Password = UnprotectSecret(config.PasswordProtected);
                return config;
            }
            catch
            {
                return null;
            }
        }

        private void SaveXuiOneConnectionConfig(XuiOneConnectionConfig config)
        {
            var path = GetXuiOneConnectionFilePath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        private static string GetXuiOneConnectionFilePath()
        {
            var baseFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MeuGestorVODs");
            return Path.Combine(baseFolder, XuiOneConnectionFileName);
        }

        private static string ProtectSecret(string secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                return string.Empty;
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(secret);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        private static string UnprotectSecret(string protectedSecret)
        {
            if (string.IsNullOrWhiteSpace(protectedSecret))
            {
                return string.Empty;
            }

            try
            {
                var bytes = Convert.FromBase64String(protectedSecret);
                var unprotected = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                return System.Text.Encoding.UTF8.GetString(unprotected);
            }
            catch
            {
                return string.Empty;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore();
                return;
            }

            try
            {
                DragMove();
            }
            catch
            {
                // Ignore drag exceptions when mouse state changes abruptly.
            }
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreWindow_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            CleanupAllEmbeddedProcesses();

            _linkCheckTimer.Stop();
            _linkCheckTimer.Tick -= LinkCheckTimer_Tick;

            try
            {
                _downloadPauseGate.Set();
            }
            catch
            {
            }

            if (_downloadCts != null)
            {
                try
                {
                    _downloadCts.Cancel();
                }
                catch
                {
                }

                _downloadCts.Dispose();
                _downloadCts = null;
            }

            foreach (var item in Downloads)
            {
                try
                {
                    item.CancelSource?.Cancel();
                }
                catch
                {
                }

                item.CancelSource?.Dispose();
                item.CancelSource = null;

                try
                {
                    item.PauseGate?.Set();
                }
                catch
                {
                }

                item.PauseGate?.Dispose();
                item.PauseGate = null;
            }

            _databaseService?.Dispose();
            _releaseClient.Dispose();
            _m3uService.Dispose();
            _downloadService.Dispose();
            _linkHealthService.Dispose();
            _streamCheckService.Dispose();
        }

        private void ToggleMaximizeRestore()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            UpdateWindowStateButton();
        }

        private void UpdateWindowStateButton()
        {
            if (MaximizeRestoreWindowButton == null)
            {
                return;
            }

            if (WindowState == WindowState.Maximized)
            {
                MaximizeRestoreWindowButton.Content = "❐";
                MaximizeRestoreWindowButton.ToolTip = "Restaurar janela";
                if (RootLayoutGrid != null)
                {
                    RootLayoutGrid.Margin = new Thickness(6);
                }
            }
            else
            {
                MaximizeRestoreWindowButton.Content = "▢";
                MaximizeRestoreWindowButton.ToolTip = "Maximizar janela";
                if (RootLayoutGrid != null)
                {
                    RootLayoutGrid.Margin = new Thickness(0);
                }
            }
        }

        private void ThemeLight_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(AppThemeMode.Light, updateStatus: true);
        }

        private void ThemeDark_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(AppThemeMode.Dark, updateStatus: true);
        }

        private void ThemeSystem_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme(AppThemeMode.System, updateStatus: true);
        }

        private void ApplyTheme(AppThemeMode mode, bool updateStatus)
        {
            _appThemeMode = mode;
            var useDark = mode == AppThemeMode.Dark || (mode == AppThemeMode.System && IsSystemDarkTheme());

            if (useDark)
            {
                SetThemeResource("AppBackgroundBrush", System.Windows.Media.Color.FromRgb(30, 30, 30));
                SetThemeResource("PanelBackgroundBrush", System.Windows.Media.Color.FromRgb(45, 45, 48));
                SetThemeResource("StatusBackgroundBrush", System.Windows.Media.Color.FromRgb(37, 37, 38));
                SetThemeResource("BaseTextBrush", System.Windows.Media.Color.FromRgb(236, 236, 241));
                SetThemeResource("HeaderBackgroundBrush", System.Windows.Media.Color.FromRgb(31, 41, 55));
                SetThemeResource("HeaderForegroundBrush", System.Windows.Media.Color.FromRgb(243, 244, 246));
            }
            else
            {
                SetThemeResource("AppBackgroundBrush", System.Windows.Media.Color.FromRgb(255, 255, 255));
                SetThemeResource("PanelBackgroundBrush", System.Windows.Media.Color.FromRgb(245, 245, 245));
                SetThemeResource("StatusBackgroundBrush", System.Windows.Media.Color.FromRgb(240, 240, 240));
                SetThemeResource("BaseTextBrush", System.Windows.Media.Color.FromRgb(17, 17, 17));
                SetThemeResource("HeaderBackgroundBrush", System.Windows.Media.Color.FromRgb(0, 122, 204));
                SetThemeResource("HeaderForegroundBrush", System.Windows.Media.Color.FromRgb(255, 255, 255));
            }

            ThemeButtonText = mode switch
            {
                AppThemeMode.Light => "Tema: Claro",
                AppThemeMode.Dark => "Tema: Escuro",
                _ => "Tema: Sistema"
            };

            if (updateStatus)
            {
                var label = mode switch
                {
                    AppThemeMode.Light => "Claro",
                    AppThemeMode.Dark => "Escuro",
                    _ => useDark ? "Sistema (escuro)" : "Sistema (claro)"
                };
                StatusMessage = $"Tema aplicado: {label}";
            }
        }

        private void SetThemeResource(string key, System.Windows.Media.Color color)
        {
            Resources[key] = new System.Windows.Media.SolidColorBrush(color);
        }

        private static bool IsSystemDarkTheme()
        {
            try
            {
                var value = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme",
                    1);

                if (value is int intValue)
                {
                    return intValue == 0;
                }
            }
            catch
            {
                // fallback abaixo
            }

            return false;
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

        private async void DownloadSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloadRunning)
            {
                if (_isDownloadPaused)
                {
                    _isDownloadPaused = false;
                    _downloadPauseGate.Set();
                    DownloadActionButtonText = "Pausar Downloads";
                    foreach (var activeItem in Downloads.Where(d => d.IsActive))
                    {
                        if (activeItem.IsPaused)
                        {
                            SetDownloadStatus(activeItem, "paused", "Pausado");
                        }
                        else
                        {
                            SetDownloadStatus(activeItem, "downloading", "Baixando...");
                        }
                    }
                    StatusMessage = "Downloads retomados.";
                }
                else
                {
                    _isDownloadPaused = true;
                    _downloadPauseGate.Reset();
                    DownloadActionButtonText = "Retomar Downloads";
                    foreach (var activeItem in Downloads.Where(d => d.IsActive))
                    {
                        SetDownloadStatus(activeItem, "paused", "Pausado");
                    }
                    StatusMessage = "Downloads pausados.";
                }

                return;
            }

            var selected = FilteredEntries.Where(x => x.IsSelected).ToList();
            
            if (!selected.Any())
            {
                System.Windows.MessageBox.Show("Selecione pelo menos um item para download", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedLive = selected.Where(IsLiveEntry).ToList();
            var selectedVod = selected.Where(x => !IsLiveEntry(x)).ToList();
            var addedLiveBySelection = await RegisterSelectedLiveChannelsAsync(selectedLive);

            if (!selectedVod.Any())
            {
                StatusMessage = $"Nenhum VOD selecionado para download. Canais ao vivo ignorados: {selectedLive.Count}. Salvos na lista de canais: {addedLiveBySelection}.";
                System.Windows.MessageBox.Show(
                    "Os itens selecionados parecem ser canais ao vivo e nao sao baixados para arquivo.\n\nUse:\n- Baixar txt Canais\n- Reproducao via VLC",
                    "Download de canais ao vivo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (!Directory.Exists(DownloadPath))
                Directory.CreateDirectory(DownloadPath);

            EnsureAndLoadDownloadStructure();
            var skippedExisting = 0;
            var completed = 0;
            var success = 0;
            var failed = 0;
            var tasks = new List<Task>();
            var maxConcurrentDownloads = ComputeDownloadParallelism(selectedVod.Count);
            using var downloadSemaphore = new SemaphoreSlim(maxConcurrentDownloads, maxConcurrentDownloads);

            _downloadCts?.Dispose();
            _downloadCts = new CancellationTokenSource();
            _downloadPauseGate.Set();
            _isDownloadRunning = true;
            _isDownloadPaused = false;
            DownloadActionButtonText = "Pausar Downloads";

            foreach (var entry in selectedVod)
            {
                var outputPath = BuildOutputPath(entry);
                if (File.Exists(outputPath))
                {
                    skippedExisting++;
                    Downloads.Add(new DownloadItem
                    {
                        Name = entry.Name,
                        LogoUrl = entry.LogoUrl,
                        FileType = GetFileType(outputPath),
                        DurationText = "ETA --:--",
                        DownloadedText = "100%",
                        TotalText = "Arquivo local",
                        SpeedText = "-",
                        Progress = 100,
                        Status = "Ja existe - ignorado",
                        StatusKind = "skipped",
                        StatusIcon = "i",
                        IsActive = false
                    });
                    continue;
                }

                var downloadItem = new DownloadItem
                {
                    Name = entry.Name,
                    LogoUrl = entry.LogoUrl,
                    FileType = GetFileType(outputPath),
                    DurationText = "ETA --:--",
                    DownloadedText = "0 B",
                    TotalText = "--",
                    SpeedText = "0 B/s",
                    Progress = 0,
                    Status = "Baixando...",
                    StatusKind = "downloading",
                    StatusIcon = ">",
                    IsActive = true,
                    IsPaused = false
                };
                Downloads.Add(downloadItem);

                var itemCts = CancellationTokenSource.CreateLinkedTokenSource(_downloadCts.Token);
                downloadItem.CancelSource = itemCts;
                downloadItem.PauseGate = new ManualResetEventSlim(initialState: true);

                tasks.Add(Task.Run(async () =>
                {
                    var slotAcquired = false;
                    try
                    {
                        await downloadSemaphore.WaitAsync(itemCts.Token);
                        slotAcquired = true;

                        Dispatcher.Invoke(() =>
                        {
                            if (_isDownloadPaused || downloadItem.IsPaused)
                            {
                                SetDownloadStatus(downloadItem, "paused", "Pausado");
                            }
                            else
                            {
                                SetDownloadStatus(downloadItem, "downloading", "Baixando...");
                            }
                        });

                        var progress = new Progress<DownloadService.DownloadProgressInfo>(p =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                downloadItem.Progress = p.Percent;
                                downloadItem.DownloadedText = FormatBytes(p.DownloadedBytes);
                                downloadItem.TotalText = p.TotalBytes > 0 ? FormatBytes(p.TotalBytes) : "--";
                                downloadItem.SpeedText = p.SpeedBytesPerSecond > 0 ? $"{FormatBytes((long)p.SpeedBytesPerSecond)}/s" : "0 B/s";
                                downloadItem.DurationText = FormatEta(p.DownloadedBytes, p.TotalBytes, p.SpeedBytesPerSecond);
                            });
                        });

                        await _downloadService.DownloadFileAsync(entry.Url, outputPath, progress, itemCts.Token, _downloadPauseGate, downloadItem.PauseGate);
                        
                        Dispatcher.Invoke(() =>
                        {
                            downloadItem.Progress = 100;
                            if (downloadItem.TotalText == "--")
                            {
                                downloadItem.TotalText = downloadItem.DownloadedText;
                            }
                            downloadItem.DurationText = "ETA 00:00";
                            downloadItem.SpeedText = "-";
                            downloadItem.IsActive = false;
                            downloadItem.IsPaused = false;
                            downloadItem.PauseGate?.Set();
                            downloadItem.PauseGate?.Dispose();
                            downloadItem.PauseGate = null;
                            downloadItem.CancelSource = null;
                            SetDownloadStatus(downloadItem, "completed", "Concluído");
                        });
                        Interlocked.Increment(ref success);
                    }
                    catch (OperationCanceledException)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            downloadItem.IsActive = false;
                            downloadItem.CancelSource = null;
                            downloadItem.DurationText = "ETA --:--";
                            downloadItem.SpeedText = "-";
                            SetDownloadStatus(downloadItem, "canceled", "Cancelado");
                            downloadItem.IsPaused = false;
                            downloadItem.PauseGate?.Set();
                            downloadItem.PauseGate?.Dispose();
                            downloadItem.PauseGate = null;
                        });
                        Interlocked.Increment(ref failed);
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            downloadItem.IsActive = false;
                            downloadItem.CancelSource = null;
                            downloadItem.DurationText = "ETA --:--";
                            downloadItem.SpeedText = "-";
                            SetDownloadStatus(downloadItem, "error", $"Erro: {ex.Message}");
                            downloadItem.IsPaused = false;
                            downloadItem.PauseGate?.Set();
                            downloadItem.PauseGate?.Dispose();
                            downloadItem.PauseGate = null;
                        });
                        Interlocked.Increment(ref failed);
                    }
                    finally
                    {
                        if (slotAcquired)
                        {
                            downloadSemaphore.Release();
                        }

                        itemCts.Dispose();
                        var done = Interlocked.Increment(ref completed);
                        Dispatcher.Invoke(() =>
                        {
                            var totalDownloadLocal = selectedVod.Count - skippedExisting;
                            if (totalDownloadLocal > 0)
                            {
                                StatusMessage = _isDownloadPaused
                                    ? $"Downloads pausados: {done}/{totalDownloadLocal} concluídos"
                                    : $"Baixando: {done}/{totalDownloadLocal} concluídos";
                            }
                        });
                    }
                }));
            }

            var totalDownload = selectedVod.Count - skippedExisting;
            if (totalDownload == 0)
            {
                _isDownloadRunning = false;
                _isDownloadPaused = false;
                DownloadActionButtonText = "Baixar Selecionados";
                StatusMessage = $"Nada para baixar. Ja existentes: {skippedExisting}. Canais ao vivo ignorados: {selectedLive.Count}. Salvos na lista de canais: {addedLiveBySelection}.";
                return;
            }

            StatusMessage = $"Iniciando download de {totalDownload} arquivo(s). Ja existentes: {skippedExisting}. Canais ao vivo ignorados: {selectedLive.Count}. Salvos na lista de canais: {addedLiveBySelection}.";

            try
            {
                await Task.WhenAll(tasks);
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    _isDownloadRunning = false;
                    _isDownloadPaused = false;
                    _downloadPauseGate.Set();
                    DownloadActionButtonText = "Baixar Selecionados";
                    StatusMessage = $"Downloads finalizados. Sucesso: {success}, Falha: {failed}, Ja existentes: {skippedExisting}, Canais ao vivo ignorados: {selectedLive.Count}, Salvos na lista de canais: {addedLiveBySelection}.";
                });
            }
        }

        private void RemoveDownloadItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: DownloadItem item })
            {
                if (item.IsActive)
                {
                    item.CancelSource?.Cancel();
                    SetDownloadStatus(item, "canceling", "Cancelando...");
                    return;
                }

                Downloads.Remove(item);
            }
        }

        private void ToggleDownloadPause_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: DownloadItem item } || !item.IsActive)
            {
                return;
            }

            if (item.IsPaused)
            {
                item.IsPaused = false;
                item.PauseGate?.Set();
                if (_isDownloadPaused)
                {
                    SetDownloadStatus(item, "paused", "Pausado");
                }
                else
                {
                    SetDownloadStatus(item, "downloading", "Baixando...");
                }
            }
            else
            {
                item.IsPaused = true;
                item.PauseGate?.Reset();
                SetDownloadStatus(item, "paused", "Pausado");
            }
        }

        private static string FormatEta(long downloadedBytes, long totalBytes, double speedBytesPerSecond)
        {
            if (totalBytes <= 0 || speedBytesPerSecond <= 0)
            {
                return "ETA --:--";
            }

            var remainingBytes = Math.Max(totalBytes - downloadedBytes, 0);
            var remainingSeconds = (int)Math.Round(remainingBytes / speedBytesPerSecond);
            if (remainingSeconds < 0)
            {
                remainingSeconds = 0;
            }

            var eta = TimeSpan.FromSeconds(remainingSeconds);
            var text = eta.TotalHours >= 1
                ? eta.ToString(@"hh\:mm\:ss")
                : eta.ToString(@"mm\:ss");
            return $"ETA {text}";
        }

        private static void SetDownloadStatus(DownloadItem item, string kind, string message)
        {
            item.StatusKind = kind;
            item.Status = message;
            item.StatusIcon = kind switch
            {
                "downloading" => ">",
                "paused" => "||",
                "completed" => "OK",
                "error" => "!",
                "canceled" => "X",
                "canceling" => "...",
                "skipped" => "i",
                _ => "-"
            };
        }

        private static string GetFileType(string outputPath)
        {
            var ext = Path.GetExtension(outputPath);
            if (string.IsNullOrWhiteSpace(ext))
            {
                return "FILE";
            }

            return ext.Trim('.').ToUpperInvariant();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 B";
            }

            string[] units = { "B", "KB", "MB", "GB", "TB" };
            var value = (double)bytes;
            var unitIndex = 0;

            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            return unitIndex == 0 ? $"{value:F0} {units[unitIndex]}" : $"{value:F2} {units[unitIndex]}";
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
                Filter = "Playlists IPTV/VLC|*.m3u;*.m3u8;*.txt;*.xspf;*.pls;*.asx;*.wpl;*.zpl;*.vlc;*.url|Todos os arquivos|*.*",
                Title = "Selecionar arquivo de playlist",
                CheckFileExists = true,
                CheckPathExists = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LocalFilePath = dialog.FileName;
                AddLocalFileToHistory(LocalFilePath);
                IsLocalFileDragOver = false;
                StatusMessage = $"Arquivo selecionado: {Path.GetFileName(LocalFilePath)}";
            }
        }

        private void LocalFileComboBox_DropDownOpened(object sender, EventArgs e)
        {
            RefreshLocalFileHistory();
        }

        private void LocalFileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox comboBox && comboBox.SelectedItem is string selectedPath)
            {
                if (File.Exists(selectedPath))
                {
                    LocalFilePath = selectedPath;
                    AddLocalFileToHistory(selectedPath);
                }
                else
                {
                    RefreshLocalFileHistory();
                }
            }
        }

        private void LocalFileDropZone_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            IsLocalFileDragOver = TryGetDroppedAnyFile(e.Data, out _);
            e.Effects = IsLocalFileDragOver ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
            e.Handled = true;
        }

        private void LocalFileDropZone_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            IsLocalFileDragOver = TryGetDroppedAnyFile(e.Data, out _);
            e.Effects = IsLocalFileDragOver ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
            e.Handled = true;
        }

        private void LocalFileDropZone_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            IsLocalFileDragOver = false;
            e.Handled = true;
        }

        private void LocalFileDropZone_Drop(object sender, System.Windows.DragEventArgs e)
        {
            IsLocalFileDragOver = false;

            if (!TryGetDroppedSupportedFile(e.Data, out var filePath))
            {
                System.Windows.MessageBox.Show(
                    "Arquivo invalido. Use playlist local valida (.m3u, .m3u8, .txt, .xspf, .pls, .asx, .wpl, .zpl, .vlc, .url).",
                    "Arrastar e Soltar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            LocalFilePath = filePath;
            AddLocalFileToHistory(LocalFilePath);
            StatusMessage = $"Arquivo local recebido por arrastar e soltar: {Path.GetFileName(LocalFilePath)}";
            e.Handled = true;
        }

        private static bool TryGetDroppedAnyFile(System.Windows.IDataObject dataObject, out string filePath)
        {
            filePath = string.Empty;

            if (dataObject.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                if (dataObject.GetData(System.Windows.DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    var firstExisting = files.FirstOrDefault(File.Exists);
                    if (!string.IsNullOrWhiteSpace(firstExisting))
                    {
                        filePath = firstExisting;
                        return true;
                    }
                }
            }

            var textPayload = (dataObject.GetData(System.Windows.DataFormats.UnicodeText) as string)
                              ?? (dataObject.GetData(System.Windows.DataFormats.Text) as string)
                              ?? string.Empty;

            if (string.IsNullOrWhiteSpace(textPayload))
            {
                return false;
            }

            foreach (var part in textPayload
                         .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(x => x.Trim().Trim('"')))
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                var localPath = NormalizePotentialLocalPath(part);
                if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
                {
                    filePath = localPath;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetDroppedSupportedFile(System.Windows.IDataObject dataObject, out string filePath)
        {
            filePath = string.Empty;

            if (dataObject.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                if (dataObject.GetData(System.Windows.DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    var candidate = files.FirstOrDefault(IsSupportedPlaylistFile);
                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        filePath = candidate;
                        return true;
                    }

                    // fallback: aceitar o primeiro arquivo existente (inclusive sem extensao padrao)
                    var firstExisting = files.FirstOrDefault(File.Exists);
                    if (!string.IsNullOrWhiteSpace(firstExisting))
                    {
                        filePath = firstExisting;
                        return true;
                    }
                }
            }

            var textPayload = (dataObject.GetData(System.Windows.DataFormats.UnicodeText) as string)
                              ?? (dataObject.GetData(System.Windows.DataFormats.Text) as string)
                              ?? string.Empty;

            if (string.IsNullOrWhiteSpace(textPayload))
            {
                return false;
            }

            foreach (var part in textPayload
                         .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(x => x.Trim().Trim('"')))
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                var localPath = NormalizePotentialLocalPath(part);
                if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath) && IsSupportedPlaylistFile(localPath))
                {
                    filePath = localPath;
                    return true;
                }
            }

            return false;
        }

        private static string NormalizePotentialLocalPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim().Trim('"');

            if (trimmed.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(trimmed, UriKind.Absolute, out var fileUri) && fileUri.IsFile)
            {
                return fileUri.LocalPath;
            }

            return trimmed;
        }

        private List<M3UEntry> ParseLocalPlaylistContent(string content, string filePath)
        {
            var parsed = _m3uService.ParseFromString(content);
            if (parsed.Count > 0)
            {
                return parsed;
            }

            var urls = Regex.Matches(content, "https?://[^\\s\\\"'<>]+", RegexOptions.IgnoreCase)
                .Select(x => x.Value.Trim())
                .Where(x => Uri.TryCreate(x, UriKind.Absolute, out _))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (urls.Count == 0)
            {
                return new List<M3UEntry>();
            }

            var baseName = Path.GetFileNameWithoutExtension(filePath);
            var result = new List<M3UEntry>(urls.Count);

            for (var i = 0; i < urls.Count; i++)
            {
                var url = urls[i];
                result.Add(new M3UEntry
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    TvgId = string.Empty,
                    Name = $"{baseName} {i + 1}",
                    Url = url,
                    GroupTitle = "Importado Local",
                    Category = "Importado",
                    SubCategory = "Playlist Local",
                    LogoUrl = string.Empty
                });
            }

            return result;
        }

        private static bool IsSupportedPlaylistFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var ext = Path.GetExtension(path);
            return ext.Equals(".m3u", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".m3u8", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".xspf", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".pls", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".asx", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".wpl", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".zpl", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".vlc", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".url", StringComparison.OrdinalIgnoreCase);
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
                RefreshLocalFileHistory();
                System.Windows.MessageBox.Show("Arquivo não encontrado.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            AddLocalFileToHistory(LocalFilePath);

            try
            {
                IsLoading = true;
                StatusMessage = "Carregando arquivo local...";
                
                var content = await File.ReadAllTextAsync(LocalFilePath);
                var entries = ParseLocalPlaylistContent(content, LocalFilePath);
                if (entries.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        "Não foi possível extrair links desta playlist local.\n\n" +
                        "Formatos suportados: M3U/M3U8/TXT e playlists VLC (XSPF/PLS/ASX/WPL/ZPL/URL).",
                        "Arquivo local",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    StatusMessage = "Nenhum link encontrado na playlist local";
                    return;
                }
                InitializeEntryAnalysisFields(entries);

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

                var (newVod, newLive) = await PersistLinkDatabasesAsync(entries);

                ApplyFilter();
                
                // Mostrar estatísticas do banco
                var dbCount = 0;
                var urlCount = 0;
                if (_databaseService != null)
                {
                    dbCount = await _databaseService.Entries.GetCountAsync();
                    urlCount = (await _databaseService.M3uUrls.GetAllAsync()).Count;
                }
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

        private void InitializeEntryAnalysisFields(IEnumerable<M3UEntry> entries)
        {
            foreach (var entry in entries)
            {
                entry.CheckStatus = ItemStatus.Checking;
                entry.ServerHost = string.Empty;
                entry.ResponseTimeMs = 0;
                entry.LastCheckedAt = null;
                entry.CheckDetails = string.Empty;
                entry.IsDuplicate = false;
                entry.NormalizedUrl = DuplicateDetectionService.NormalizeUrl(entry.Url);
            }

            AnalysisProgressValue = 0;
            AnalysisProgressText = "Pronto para analisar";
            AnalysisSummaryText = "Analisados: 0 | ONLINE: 0 | OFFLINE: 0 | Duplicados: 0";
            ServerScores.Clear();
        }

        private async void AnalyzeLinks_Click(object sender, RoutedEventArgs e)
        {
            if (IsAnalyzingLinks)
            {
                _analysisCts?.Cancel();
                StatusMessage = "Cancelando IPTV Checker...";
                DiagnosticsLogger.Warn("Checker", "Cancelamento solicitado pelo usuario.");
                return;
            }

            if (_allEntries.Count == 0)
            {
                System.Windows.MessageBox.Show("Carregue uma lista antes de analisar os links.", "Analisar Link", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsAnalyzingLinks = true;
            IsLoading = true;
            StatusMessage = "Iniciando IPTV Checker...";

            _analysisCts?.Dispose();
            _analysisCts = new CancellationTokenSource();

            var correlationId = Guid.NewGuid().ToString("N")[..8];
            var total = _allEntries.Count;
            var checkedCount = 0;
            var onlineCount = 0;
            var offlineCount = 0;
            var duplicateCount = 0;
            DispatcherTimer? uiTimer = null;

            try
            {
                DiagnosticsLogger.Info("Checker", $"[{correlationId}] Inicio da analise. Total={total}");

                var queue = new ConcurrentQueue<StreamCheckItemResult>();

                uiTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(120)
                };

                uiTimer.Tick += (_, _) =>
                {
                    var updated = 0;
                    while (updated < 600 && queue.TryDequeue(out var item))
                    {
                        item.Entry.CheckStatus = item.Status;
                        item.Entry.ServerHost = item.ServerHost;
                        item.Entry.ResponseTimeMs = item.ResponseTimeMs;
                        item.Entry.LastCheckedAt = item.CheckedAt;
                        item.Entry.CheckDetails = item.Details;
                        updated++;
                    }

                    var checkedSnapshot = Volatile.Read(ref checkedCount);
                    var onlineSnapshot = Volatile.Read(ref onlineCount);
                    var offlineSnapshot = Volatile.Read(ref offlineCount);
                    var duplicateSnapshot = Volatile.Read(ref duplicateCount);

                    AnalysisProgressValue = total == 0 ? 0 : checkedSnapshot * 100.0 / total;
                    AnalysisProgressText = $"Analisando {checkedSnapshot} de {total} links...";
                    AnalysisSummaryText = $"Analisados: {checkedSnapshot} | ONLINE: {onlineSnapshot} | OFFLINE: {offlineSnapshot} | Duplicados: {duplicateSnapshot}";
                };

                uiTimer.Start();

                var options = new StreamCheckOptions
                {
                    TimeoutSeconds = 8,
                    MaxParallelism = ComputeParallelism(total),
                    RetryCount = 1,
                    RetryDelayMilliseconds = 300
                };

                var runResult = await _checkerOrchestrator.RunAsync(
                    _allEntries,
                    options,
                    (result, snapshot) =>
                    {
                        queue.Enqueue(result);

                        Interlocked.Exchange(ref checkedCount, snapshot.CheckedCount);
                        Interlocked.Exchange(ref onlineCount, snapshot.OnlineCount);
                        Interlocked.Exchange(ref offlineCount, snapshot.OfflineCount);
                        Interlocked.Exchange(ref duplicateCount, snapshot.DuplicateCount);
                    },
                    _analysisCts.Token);

                uiTimer.Stop();

                while (queue.TryDequeue(out var item))
                {
                    item.Entry.CheckStatus = item.Status;
                    item.Entry.ServerHost = item.ServerHost;
                    item.Entry.ResponseTimeMs = item.ResponseTimeMs;
                    item.Entry.LastCheckedAt = item.CheckedAt;
                    item.Entry.CheckDetails = item.Details;
                }

                ServerScores.Clear();
                foreach (var score in runResult.ServerScores)
                {
                    ServerScores.Add(score);
                }

                if (_databaseService != null)
                {
                    await _databaseService.M3uUrls.AddStreamCheckLogsAsync(runResult.Logs);
                    await _databaseService.M3uUrls.AddServerScoreSnapshotsAsync(
                        ServerScores.Select(x => new ServerScoreSnapshotEntry
                        {
                            ServerHost = x.Host,
                            Score = x.Score,
                            Quality = x.Quality.ToString(),
                            SuccessRate = x.SuccessRate,
                            AverageResponseMs = x.AverageResponseMs,
                            TotalLinks = x.TotalLinks,
                            OnlineLinks = x.OnlineLinks,
                            OfflineLinks = x.OfflineLinks
                        }),
                        DateTime.Now);

                    if (_databaseService.M3uUrls is SqliteM3uUrlRepository sqliteRepository)
                    {
                        try
                        {
                            await sqliteRepository.ApplyAnalysisRetentionPolicyAsync(
                                streamCheckRetentionDays: StreamCheckLogRetentionDays,
                                serverScoreRetentionDays: ServerScoreRetentionDays,
                                maxStreamCheckRows: MaxStreamCheckLogRows);
                        }
                        catch (Exception retentionEx)
                        {
                            DiagnosticsLogger.Warn("Checker", $"[{correlationId}] Falha na politica de retencao de logs: {retentionEx.Message}");
                        }
                    }
                }

                AnalysisProgressValue = 100;
                AnalysisProgressText = $"Análise concluída: {runResult.CheckedCount} de {runResult.TotalCount}";
                AnalysisSummaryText = $"Analisados: {runResult.CheckedCount} | ONLINE: {runResult.OnlineCount} | OFFLINE: {runResult.OfflineCount} | Duplicados: {runResult.DuplicateCount}";

                ApplyFilter();
                EntriesList.Items.Refresh();
                StatusMessage = "IPTV Checker concluído com sucesso.";
                DiagnosticsLogger.Info(
                    "Checker",
                    $"[{correlationId}] Concluido. Checked={runResult.CheckedCount} Online={runResult.OnlineCount} Offline={runResult.OfflineCount} Duplicados={runResult.DuplicateCount}");
            }
            catch (OperationCanceledException)
            {
                AnalysisProgressText = $"Analise cancelada: {checkedCount} de {total}";
                StatusMessage = "IPTV Checker cancelado.";
                DiagnosticsLogger.Warn("Checker", $"[{correlationId}] Cancelado. Checked={checkedCount}/{total}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao analisar links: {ex.Message}", "Analisar Link", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Erro durante análise de links";
                DiagnosticsLogger.Error("Checker", $"[{correlationId}] Falha durante analise.", ex);
            }
            finally
            {
                uiTimer?.Stop();
                _analysisCts?.Dispose();
                _analysisCts = null;
                IsLoading = false;
                IsAnalyzingLinks = false;
            }
        }

        private static int ComputeParallelism(int total)
        {
            if (total <= 1000) return 16;
            if (total <= 10000) return 24;
            if (total <= 50000) return 28;
            return 32;
        }

        private static int ComputeDownloadParallelism(int total)
        {
            if (total <= 0)
            {
                return 1;
            }

            if (total <= 3)
            {
                return total;
            }

            return Math.Min(8, total);
        }

        private void RemoveDuplicates_Click(object sender, RoutedEventArgs e)
        {
            if (_allEntries.Count == 0)
            {
                return;
            }

            var duplicates = _allEntries.Count(x => x.IsDuplicate);
            if (duplicates == 0)
            {
                System.Windows.MessageBox.Show("Nenhum link duplicado encontrado.", "Duplicados", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = System.Windows.MessageBox.Show(
                $"Foram encontrados {duplicates} links duplicados.\n\nDeseja remover da lista carregada?",
                "Remover Duplicados",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            _allEntries.RemoveAll(x => x.IsDuplicate);
            BuildGroupIndex(_allEntries);
            ApplyFilter();
            StatusMessage = $"{duplicates} duplicados removidos da lista atual.";
        }

        private async void ExportAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (_allEntries.Count == 0)
            {
                return;
            }

            var choice = PromptForChoice(
                "Escolha o tipo de exportação:",
                "Exportar resultado da análise",
                new[]
                {
                    "M3U apenas ONLINE",
                    "M3U sem duplicados",
                    "M3U limpo (ONLINE + sem duplicados)"
                },
                "M3U limpo (ONLINE + sem duplicados)");

            if (string.IsNullOrWhiteSpace(choice))
            {
                return;
            }

            IEnumerable<M3UEntry> source = _allEntries;
            var fileSuffix = "completo";

            if (choice.Contains("apenas ONLINE", StringComparison.OrdinalIgnoreCase))
            {
                source = source.Where(x => x.CheckStatus == ItemStatus.Ok);
                fileSuffix = "online";
            }
            else if (choice.Contains("sem duplicados", StringComparison.OrdinalIgnoreCase) && !choice.Contains("ONLINE", StringComparison.OrdinalIgnoreCase))
            {
                source = source.Where(x => !x.IsDuplicate);
                fileSuffix = "sem_duplicados";
            }
            else
            {
                source = source.Where(x => x.CheckStatus == ItemStatus.Ok && !x.IsDuplicate);
                fileSuffix = "limpo";
            }

            var list = source.ToList();
            if (list.Count == 0)
            {
                System.Windows.MessageBox.Show("Nenhum item atende ao critério de exportação.", "Exportar M3U", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var path = Path.Combine(DownloadPath, $"export_checker_{fileSuffix}_{DateTime.Now:yyyyMMdd_HHmmss}.m3u");
            var lines = new List<string> { "#EXTM3U" };

            foreach (var entry in list)
            {
                var extinf = $"#EXTINF:-1 tvg-id=\"{EscapeM3uAttribute(entry.TvgId)}\" " +
                             $"tvg-name=\"{EscapeM3uAttribute(entry.Name)}\" " +
                             $"tvg-logo=\"{EscapeM3uAttribute(entry.LogoUrl)}\" " +
                             $"group-title=\"{EscapeM3uAttribute(entry.GroupTitle)}\",{EscapeM3uAttribute(entry.Name)}";

                lines.Add(extinf);
                lines.Add(entry.Url);
            }

            await File.WriteAllLinesAsync(path, lines);
            StatusMessage = $"Exportado: {list.Count} links em {Path.GetFileName(path)}";

            System.Windows.MessageBox.Show(
                $"Exportação concluída!\n\nArquivo: {path}\nItens: {list.Count}",
                "Exportar M3U",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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

        private async Task<(int newVod, int newLive)> PersistLinkDatabasesAsync(IEnumerable<M3UEntry> entries)
        {
            EnsureLinkDatabaseFiles();

            // Sempre manter ambos sincronizados: SQLite (principal) + TXT (backup)
            var vodFilePath = Path.Combine(DownloadPath, VodLinksDatabaseFileName);
            var liveFilePath = Path.Combine(DownloadPath, LiveLinksDatabaseFileName);

            var vodEntries = entries.Where(IsVodEntry).ToList();
            var liveEntries = entries.Where(e => !IsVodEntry(e)).ToList();

            var addedVodInSqlite = 0;
            var addedLiveInSqlite = 0;

            // PASSO 1: Salvar no SQLite (banco principal)
            if (_databaseService != null)
            {
                addedVodInSqlite = await _databaseService.Entries.AddRangeAsync(vodEntries);
                addedLiveInSqlite = await _databaseService.Entries.AddRangeAsync(liveEntries);
            }
            else
            {
                // Fallback se banco não disponível
                addedVodInSqlite = vodEntries.Count;
                addedLiveInSqlite = liveEntries.Count;
            }

            // PASSO 2: Sincronizar com arquivos TXT (backup)
            _ = MergeEntriesIntoDatabase(vodFilePath, vodEntries);
            _ = MergeEntriesIntoDatabase(liveFilePath, liveEntries);

            return (addedVodInSqlite, addedLiveInSqlite);
        }

        private async Task<int> RegisterSelectedLiveChannelsAsync(IEnumerable<M3UEntry> liveEntries)
        {
            var normalizedEntries = liveEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.Url))
                .Select(NormalizeLiveEntryForDatabase)
                .ToList();

            if (normalizedEntries.Count == 0)
            {
                return 0;
            }

            EnsureLinkDatabaseFiles();

            if (_databaseService != null)
            {
                await _databaseService.Entries.AddRangeAsync(normalizedEntries);
            }

            var liveFilePath = Path.Combine(DownloadPath, LiveLinksDatabaseFileName);
            return MergeEntriesIntoDatabase(liveFilePath, normalizedEntries);
        }

        private static M3UEntry NormalizeLiveEntryForDatabase(M3UEntry source)
        {
            var category = string.IsNullOrWhiteSpace(source.Category) ? "Canais" : source.Category;
            if (!category.Contains("canal", StringComparison.OrdinalIgnoreCase) &&
                !category.Contains("live", StringComparison.OrdinalIgnoreCase) &&
                !category.Contains("ao vivo", StringComparison.OrdinalIgnoreCase))
            {
                category = "Canais";
            }

            var subCategory = string.IsNullOrWhiteSpace(source.SubCategory) ? "Adicionado pelo usuario" : source.SubCategory;
            var groupTitle = string.IsNullOrWhiteSpace(source.GroupTitle)
                ? $"{category} | {subCategory}"
                : source.GroupTitle;

            return new M3UEntry
            {
                Id = string.IsNullOrWhiteSpace(source.Id) ? Guid.NewGuid().ToString("N")[..8] : source.Id,
                Name = string.IsNullOrWhiteSpace(source.Name) ? "Canal ao vivo" : source.Name,
                Url = source.Url,
                GroupTitle = groupTitle,
                Category = category,
                SubCategory = subCategory,
                LogoUrl = source.LogoUrl,
                TvgId = source.TvgId
            };
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
            return !IsLiveEntry(entry);
        }

        private bool IsLiveEntry(M3UEntry entry)
        {
            var category = (entry.Category ?? string.Empty).Trim();
            if (LiveCategoryNames.Contains(category))
            {
                return true;
            }

            // Fallback apenas quando a categoria vier vazia.
            if (string.IsNullOrWhiteSpace(category))
            {
                var resolvedCategory = ResolveCategory(entry);
                return LiveCategoryNames.Contains(resolvedCategory);
            }

            return false;
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
            var text = $"{entry.Category} {entry.SubCategory} {entry.GroupTitle} {entry.Name} {entry.Url}".ToLowerInvariant();

            if (text.Contains("serie") || text.Contains("series") || text.Contains("/series")) return "Series";
            if (text.Contains("filme") || text.Contains("movie") || text.Contains("cinema") || text.Contains("/movie")) return "Filmes";
            if (text.Contains("24 horas") || text.Contains("24h")) return "24 Horas";
            if (text.Contains("canal") || text.Contains("canais") || text.Contains("channel") || text.Contains("channels") || text.Contains("/live") || text.Contains("/channel")) return "Canais";
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

        private async void OpenVodLinksTxt_Click(object sender, RoutedEventArgs e)
        {
            await ExportPlaylistForVlcAsync(isVod: true);
        }

        private async void OpenLiveLinksTxt_Click(object sender, RoutedEventArgs e)
        {
            await ExportPlaylistForVlcAsync(isVod: false);
        }

        private async Task ExportPlaylistForVlcAsync(bool isVod)
        {
            try
            {
                EnsureLinkDatabaseFiles();

                var label = isVod ? "VODs" : "Canais";
                var filePrefix = isVod ? "vods" : "canais";

                var entries = await LoadEntriesForExportAsync(isVod);
                if (entries.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        $"Nenhum item de {label} foi encontrado para exportação.",
                        "Exportação",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var lines = new List<string> { "#EXTM3U" };
                foreach (var entry in entries)
                {
                    var tvgId = !string.IsNullOrWhiteSpace(entry.TvgId) ? entry.TvgId : entry.Id;
                    var extinf = $"#EXTINF:-1 tvg-id=\"{EscapeM3uAttribute(tvgId)}\" " +
                                 $"tvg-name=\"{EscapeM3uAttribute(entry.Name)}\" " +
                                 $"tvg-logo=\"{EscapeM3uAttribute(entry.LogoUrl)}\" " +
                                 $"group-title=\"{EscapeM3uAttribute(entry.GroupTitle)}\",{EscapeM3uAttribute(entry.Name)}";

                    lines.Add(extinf);
                    lines.Add(entry.Url.Trim());
                }

                var outputPath = Path.Combine(DownloadPath, $"playlist_{filePrefix}_vlc_{DateTime.Now:yyyyMMdd_HHmmss}.m3u");
                await File.WriteAllLinesAsync(outputPath, lines);

                var isCompatible = ValidateM3uForVlc(lines, out var validationDetails);
                var compatMessage = isCompatible
                    ? "Compatível com VLC: Sim (formato #EXTM3U/#EXTINF válido)."
                    : "Compatível com VLC: Parcial (arquivo exportado, mas com alertas).";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{outputPath}\"",
                    UseShellExecute = true
                });

                System.Windows.MessageBox.Show(
                    $"Arquivo de {label} exportado com sucesso!\n\n" +
                    $"Arquivo: {outputPath}\n" +
                    $"Itens: {entries.Count}\n" +
                    $"{compatMessage}\n\n" +
                    $"Detalhes: {validationDetails}",
                    "Exportação para VLC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                StatusMessage = $"Playlist {label} exportada para VLC ({entries.Count} itens).";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao exportar playlist: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Erro ao exportar playlist para VLC";
            }
        }

        private async Task<List<M3UEntry>> LoadEntriesForExportAsync(bool isVod)
        {
            if (_databaseService != null)
            {
                var allEntries = await _databaseService.Entries.GetAllAsync();
                return allEntries.Where(e => isVod ? IsVodEntry(e) : !IsVodEntry(e)).ToList();
            }

            var legacyPath = Path.Combine(DownloadPath, isVod ? VodLinksDatabaseFileName : LiveLinksDatabaseFileName);
            if (!File.Exists(legacyPath))
            {
                return new List<M3UEntry>();
            }

            var content = await File.ReadAllTextAsync(legacyPath);
            var parsed = _m3uService.ParseFromString(content);
            if (parsed.Count > 0)
            {
                return parsed;
            }

            var fallback = new List<M3UEntry>();
            foreach (var raw in File.ReadLines(legacyPath))
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var parts = line.Split('|');
                if (parts.Length < 3)
                {
                    continue;
                }

                fallback.Add(new M3UEntry
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Name = parts[0].Trim(),
                    GroupTitle = parts[1].Trim(),
                    Url = parts[2].Trim(),
                    Category = parts[1].Split('|').FirstOrDefault()?.Trim() ?? "Sem Categoria",
                    SubCategory = parts[1].Contains('|') ? string.Join(" | ", parts[1].Split('|').Skip(1).Select(x => x.Trim())) : "Geral"
                });
            }

            return fallback;
        }

        private static bool ValidateM3uForVlc(IReadOnlyList<string> lines, out string details)
        {
            if (lines.Count == 0)
            {
                details = "Arquivo vazio.";
                return false;
            }

            if (!lines[0].Trim().Equals("#EXTM3U", StringComparison.OrdinalIgnoreCase))
            {
                details = "Cabeçalho #EXTM3U ausente.";
                return false;
            }

            var extinfCount = lines.Count(x => x.TrimStart().StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase));
            var urlCount = lines.Count(x => !string.IsNullOrWhiteSpace(x) && !x.TrimStart().StartsWith("#", StringComparison.Ordinal));

            if (extinfCount == 0 || urlCount == 0)
            {
                details = "Entradas #EXTINF/URL insuficientes.";
                return false;
            }

            var validUrlCount = lines
                .Where(x => !string.IsNullOrWhiteSpace(x) && !x.TrimStart().StartsWith("#", StringComparison.Ordinal))
                .Count(x => Uri.TryCreate(x.Trim(), UriKind.Absolute, out _));

            details = $"#EXTINF: {extinfCount}, URLs: {urlCount}, URLs válidas: {validUrlCount}";
            return validUrlCount > 0 && extinfCount == urlCount;
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
                    .Select(g => new CategoryStatRow
                    {
                        Category = g.Key,
                        Count = g.Count()
                    })
                    .ToList();

                ShowDatabaseStatsWindow(totalCount, categories, Path.Combine(DownloadPath, "database.sqlite"));

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

        private void ShowDatabaseStatsWindow(int totalCount, List<CategoryStatRow> categories, string dbPath)
        {
            var window = new System.Windows.Window
            {
                Title = "Estatísticas do Banco de Dados",
                Width = 620,
                Height = 500,
                MinWidth = 560,
                MinHeight = 420,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                ResizeMode = System.Windows.ResizeMode.CanResize,
                Owner = this,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 246, 248))
            };

            var root = new System.Windows.Controls.Grid { Margin = new System.Windows.Thickness(14) };
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            root.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });

            var title = new System.Windows.Controls.TextBlock
            {
                Text = "Estatísticas do Banco de Dados SQLite",
                FontSize = 18,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 48, 75)),
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            };
            System.Windows.Controls.Grid.SetRow(title, 0);
            root.Children.Add(title);

            var summaryBorder = new System.Windows.Controls.Border
            {
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(210, 214, 220)),
                BorderThickness = new System.Windows.Thickness(1),
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new System.Windows.CornerRadius(6),
                Padding = new System.Windows.Thickness(12),
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            };
            var summaryGrid = new System.Windows.Controls.Grid();
            summaryGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(180) });
            summaryGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            summaryGrid.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Total de entradas:",
                FontWeight = System.Windows.FontWeights.SemiBold,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            summaryGrid.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = totalCount.ToString("N0"),
                FontSize = 22,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 125, 50)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            System.Windows.Controls.Grid.SetColumn(summaryGrid.Children[1], 1);
            summaryBorder.Child = summaryGrid;
            System.Windows.Controls.Grid.SetRow(summaryBorder, 1);
            root.Children.Add(summaryBorder);

            var section = new System.Windows.Controls.TextBlock
            {
                Text = "Top 10 Categorias",
                FontWeight = System.Windows.FontWeights.SemiBold,
                FontSize = 13,
                Margin = new System.Windows.Thickness(0, 0, 0, 6)
            };
            System.Windows.Controls.Grid.SetRow(section, 2);
            root.Children.Add(section);

            var grid = new System.Windows.Controls.DataGrid
            {
                ItemsSource = categories,
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                HeadersVisibility = System.Windows.Controls.DataGridHeadersVisibility.Column,
                GridLinesVisibility = System.Windows.Controls.DataGridGridLinesVisibility.Horizontal,
                RowHeaderWidth = 0,
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            };

            grid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
            {
                Header = "Categoria",
                Binding = new System.Windows.Data.Binding(nameof(CategoryStatRow.Category)),
                Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)
            });

            grid.Columns.Add(new System.Windows.Controls.DataGridTextColumn
            {
                Header = "Quantidade",
                Binding = new System.Windows.Data.Binding(nameof(CategoryStatRow.Count)) { StringFormat = "N0" },
                Width = 140,
                ElementStyle = new System.Windows.Style(typeof(System.Windows.Controls.TextBlock))
                {
                    Setters =
                    {
                        new System.Windows.Setter(System.Windows.Controls.TextBlock.TextAlignmentProperty, System.Windows.TextAlignment.Right),
                        new System.Windows.Setter(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Right)
                    }
                }
            });

            System.Windows.Controls.Grid.SetRow(grid, 3);
            root.Children.Add(grid);

            var footer = new System.Windows.Controls.Grid { Margin = new System.Windows.Thickness(0, 4, 0, 0) };
            footer.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });

            var dbPathText = new System.Windows.Controls.TextBlock
            {
                Text = $"Local do banco: {dbPath}",
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 90, 96)),
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                ToolTip = dbPath
            };
            System.Windows.Controls.Grid.SetColumn(dbPathText, 0);
            footer.Children.Add(dbPathText);

            var closeButton = new System.Windows.Controls.Button
            {
                Content = "Fechar",
                MinWidth = 90,
                Padding = new System.Windows.Thickness(10, 4, 10, 4),
                Margin = new System.Windows.Thickness(8, 0, 0, 0),
                IsDefault = true
            };
            closeButton.Click += (_, _) => window.Close();
            System.Windows.Controls.Grid.SetColumn(closeButton, 1);
            footer.Children.Add(closeButton);

            System.Windows.Controls.Grid.SetRow(footer, 4);
            root.Children.Add(footer);

            window.Content = root;
            window.ShowDialog();
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

    public class CategoryStatRow
    {
        public string Category { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class XuiOneConnectionConfig
    {
        public string Host { get; set; } = "127.0.0.1";
        public uint Port { get; set; } = 3306;
        public string Database { get; set; } = "xui";
        public string User { get; set; } = "root";
        public string PasswordProtected { get; set; } = string.Empty;

        [JsonIgnore]
        public string Password { get; set; } = string.Empty;
    }

    public class DownloadItem : INotifyPropertyChanged
    {
        private string _name = "";
        private double _progress;
        private string _status = "";
        private string _logoUrl = "";
        private string _fileType = "FILE";
        private string _durationText = "ETA --:--";
        private string _downloadedText = "0 B";
        private string _totalText = "--";
        private string _speedText = "0 B/s";
        private string _statusKind = "downloading";
        private string _statusIcon = ">";
        private bool _isActive;
        private bool _isPaused;
        private CancellationTokenSource? _cancelSource;
        private ManualResetEventSlim? _pauseGate;

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

        public string LogoUrl
        {
            get => _logoUrl;
            set { _logoUrl = value; OnPropertyChanged(nameof(LogoUrl)); }
        }

        public string FileType
        {
            get => _fileType;
            set { _fileType = value; OnPropertyChanged(nameof(FileType)); }
        }

        public string DurationText
        {
            get => _durationText;
            set { _durationText = value; OnPropertyChanged(nameof(DurationText)); }
        }

        public string DownloadedText
        {
            get => _downloadedText;
            set { _downloadedText = value; OnPropertyChanged(nameof(DownloadedText)); }
        }

        public string TotalText
        {
            get => _totalText;
            set { _totalText = value; OnPropertyChanged(nameof(TotalText)); }
        }

        public string SpeedText
        {
            get => _speedText;
            set { _speedText = value; OnPropertyChanged(nameof(SpeedText)); }
        }

        public string StatusKind
        {
            get => _statusKind;
            set { _statusKind = value; OnPropertyChanged(nameof(StatusKind)); }
        }

        public string StatusIcon
        {
            get => _statusIcon;
            set { _statusIcon = value; OnPropertyChanged(nameof(StatusIcon)); }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(PauseButtonText));
                OnPropertyChanged(nameof(PauseButtonToolTip));
            }
        }

        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                _isPaused = value;
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(PauseButtonText));
                OnPropertyChanged(nameof(PauseButtonToolTip));
            }
        }

        public CancellationTokenSource? CancelSource
        {
            get => _cancelSource;
            set { _cancelSource = value; OnPropertyChanged(nameof(CancelSource)); }
        }

        public ManualResetEventSlim? PauseGate
        {
            get => _pauseGate;
            set { _pauseGate = value; OnPropertyChanged(nameof(PauseGate)); }
        }

        public string PauseButtonText => !IsActive ? "-" : (IsPaused ? "▶" : "❚❚");
        public string PauseButtonToolTip => !IsActive ? "Download finalizado" : (IsPaused ? "Retomar download" : "Pausar download");

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class StringNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var text = value as string;
            return string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

}
