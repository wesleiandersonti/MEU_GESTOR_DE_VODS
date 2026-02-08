using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using LibVLCSharp.Shared;

namespace MeuGestorVODs
{
    public partial class PlayerWindow : Window
    {
        private LibVLC? _libVLC;
        private MediaPlayer? _mediaPlayer;
        private string? _streamUrl;
        private bool _isPlaying = false;

        public string? StreamName { get; set; }
        public string? ServerUrl { get; set; }

        public PlayerWindow()
        {
            InitializeComponent();
            InitializeVLC();
            this.Closing += PlayerWindow_Closing;
        }

        private void InitializeVLC()
        {
            try
            {
                // Inicializa o LibVLC
                _libVLC = new LibVLC();
                _mediaPlayer = new MediaPlayer(_libVLC);
                
                // Conecta o player ao VideoView
                VideoPlayer.MediaPlayer = _mediaPlayer;

                // Configura volume
                VolumeSlider.Value = _mediaPlayer.Volume;
                VolumeSlider.ValueChanged += (s, e) =>
                {
                    if (_mediaPlayer != null)
                        _mediaPlayer.Volume = (int)VolumeSlider.Value;
                };

                // Eventos do player
                _mediaPlayer.Playing += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _isPlaying = true;
                        PlayPauseButton.Content = "⏸ Pause";
                    });
                };

                _mediaPlayer.Paused += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _isPlaying = false;
                        PlayPauseButton.Content = "▶ Play";
                    });
                };

                _mediaPlayer.Stopped += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _isPlaying = false;
                        PlayPauseButton.Content = "▶ Play";
                    });
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao inicializar player: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task PlayStream(string url, string? name = null)
        {
            _streamUrl = url;
            StreamName = name ?? "Canal/VOD";

            // Atualiza título
            TitleTextBlock.Text = $"🎬 PLAYER - {StreamName}";

            // Extrai informações
            UpdateTechnicalInfo(url);

            // Mede latência
            await MeasureLatencyAsync(url);

            try
            {
                if (_libVLC == null || _mediaPlayer == null)
                {
                    MessageBox.Show("Player não inicializado corretamente.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Cria media e reproduz
                using var media = new Media(_libVLC, url, FromType.FromLocation);
                _mediaPlayer.Play(media);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao reproduzir: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateTechnicalInfo(string url)
        {
            try
            {
                // Nome
                InfoName.Text = StreamName ?? "Desconhecido";

                // Servidor (extrai hostname da URL)
                var uri = new Uri(url);
                ServerUrl = uri.Host;
                InfoServer.Text = ServerUrl;

                // Formato (detecta pelo protocolo e extensão)
                string format = DetectFormat(url);
                InfoFormat.Text = format;
            }
            catch
            {
                InfoName.Text = StreamName ?? "Desconhecido";
                InfoServer.Text = "N/A";
                InfoFormat.Text = "Desconhecido";
            }
        }

        private string DetectFormat(string url)
        {
            url = url.ToLower();

            if (url.Contains(".m3u8") || url.Contains("hls"))
                return "HLS (m3u8)";
            if (url.Contains(".mpd") || url.Contains("dash"))
                return "DASH";
            if (url.Contains("rtmp"))
                return "RTMP";
            if (url.Contains("rtsp"))
                return "RTSP";
            if (url.Contains(".mp4"))
                return "MP4";
            if (url.Contains(".ts"))
                return "MPEG-TS";
            if (url.Contains("http"))
                return "HTTP Stream";

            return "Stream";
        }

        private async Task MeasureLatencyAsync(string url)
        {
            try
            {
                var uri = new Uri(url);
                var host = uri.Host;

                // Executa ping
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, 2000);

                if (reply.Status == IPStatus.Success)
                {
                    InfoLatency.Text = $"{reply.RoundtripTime}ms";
                    
                    // Cor baseada na latência
                    if (reply.RoundtripTime < 50)
                        InfoLatency.Foreground = System.Windows.Media.Brushes.Green;
                    else if (reply.RoundtripTime < 150)
                        InfoLatency.Foreground = System.Windows.Media.Brushes.Orange;
                    else
                        InfoLatency.Foreground = System.Windows.Media.Brushes.Red;
                }
                else
                {
                    InfoLatency.Text = "Timeout";
                    InfoLatency.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch
            {
                InfoLatency.Text = "N/A";
                InfoLatency.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_mediaPlayer == null) return;

            if (_isPlaying)
            {
                _mediaPlayer.Pause();
            }
            else
            {
                _mediaPlayer.Play();
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer?.Stop();
        }

        private void PlayerWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Libera recursos
            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
        }
    }
}
