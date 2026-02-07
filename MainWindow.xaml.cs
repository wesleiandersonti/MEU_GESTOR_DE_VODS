using System;
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
        }

        private async void LoadM3U_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(M3UUrl))
            {
                MessageBox.Show("Por favor, insira a URL do arquivo M3U", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"Erro ao carregar M3U: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Selecione pelo menos um item para download", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(DownloadPath))
                Directory.CreateDirectory(DownloadPath);

            foreach (var entry in selected)
            {
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
                        var outputPath = Path.Combine(DownloadPath, entry.SanitizedName + ".mp4");
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

            StatusMessage = $"Iniciando download de {selected.Count} arquivo(s)";
        }

        private void BrowsePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DownloadPath = dialog.SelectedPath;
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
