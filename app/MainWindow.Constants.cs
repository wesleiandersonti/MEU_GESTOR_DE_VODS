using System;
using System.Collections.Generic;

namespace MeuGestorVODs
{
    /// <summary>
    /// Constantes e enums compartilhados da janela principal (partial).
    /// </summary>
    public partial class MainWindow
    {
        private enum LinkCheckScheduleMode
        {
            Manual,
            Every3Hours,
            Every6Hours,
            Every12Hours
        }

        private enum MonitorPanelLayout
        {
            Normal,
            Minimized,
            Maximized
        }

        private enum AppThemeMode
        {
            System,
            Light,
            Dark
        }

        private const string DownloadStructureFileName = "estrutura_downloads.txt";
        private const string VodLinksDatabaseFileName = "banco_vod_links.txt";
        private const string LiveLinksDatabaseFileName = "banco_canais_ao_vivo.txt";
        private const string LocalFileHistoryFileName = "local_file_history.json";
        private const string XuiOneConnectionFileName = "xui_one_connection.json";
        private const string LisoFlixHtmlFileName = "LisoFlix - Copia.html";
        private const string DarkBulletHtmlFileName = "DARK BULLET COM PLAYER INTEGRADO.HTML";
        private const string DrmPlayerHtmlFileName = "drm-player/index.html";
        private const string BotIptvHtmlFileName = "bot-iptv/index.html";
        private const string BundledIpPortFolderName = "ip-e-porta";
        private const string PlaylistFinderExecutableFileName = "playlistfinder.app.exe";
        private static readonly HashSet<string> LiveCategoryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Canais",
            "24 Horas"
        };
        private const string RepoApiBase = "https://api.github.com/repos/wesleiandersonti/MEU_GESTOR_DE_VODS";
        private const string UpdateManifestPrimaryUrl = "https://raw.githubusercontent.com/wesleiandersonti/MEU_GESTOR_DE_VODS/main/app/update.json";
        private const string UpdateManifestFallbackUrl = "https://wesleiandersonti.github.io/MEU_GESTOR_DE_VODS/update.json";
        private const int StreamCheckLogRetentionDays = 14;
        private const int ServerScoreRetentionDays = 30;
        private const int MaxStreamCheckLogRows = 250000;
        private const bool RequireUpdateSha256 = false;
        private const bool RequireSignedInstaller = false;
        private const string ExpectedInstallerPublisher = "";
        private const string PlaylistFinderExecutableSha256 = "5e5185d9cba76e81e1c68adb9d4ba328f722c4989cd2c1aa2371b6e947eedc3b";
        private static readonly HashSet<string> TrustedUpdateHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "github.com",
            "objects.githubusercontent.com",
            "raw.githubusercontent.com",
            "wesleiandersonti.github.io"
        };
    }
}
