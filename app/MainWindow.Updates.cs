using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MeuGestorVODs
{
    /// <summary>
    /// Lógica de atualização automática e rollback (partial).
    /// </summary>
    public partial class MainWindow
    {
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

                var manifest = await GetLatestUpdateManifestAsync();
                if (manifest != null)
                {
                    if (!IsNewerRelease(manifest.Version, currentVersion))
                    {
                        System.Windows.MessageBox.Show(
                            $"Voce ja esta na versao mais recente ({currentVersion}).\nVersao disponivel no manifesto: {manifest.Version}.",
                            "Atualizacao",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        StatusMessage = "Aplicativo ja esta atualizado";
                        IsUpdateAvailable = false;
                        return;
                    }

                    IsUpdateAvailable = true;
                    var notes = BuildManifestNotesPreview(manifest);
                    var confirmManifest = System.Windows.MessageBox.Show(
                        $"Nova versao encontrada: {manifest.Version}.\nVersao atual: {currentVersion}.\n\n" +
                        $"Melhorias:\n{notes}\n\n" +
                        "Deseja baixar e atualizar agora automaticamente?",
                        "Atualizacao disponivel",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (confirmManifest != MessageBoxResult.Yes)
                    {
                        StatusMessage = "Atualizacao cancelada pelo usuario";
                        return;
                    }

                    await InstallManifestAsync(manifest, "Atualizacao");
                    return;
                }

                var latest = await GetLatestInstallableReleaseAsync();
                if (latest == null)
                {
                    var latestTag = await GetLatestTagNameAsync();
                    if (!string.IsNullOrWhiteSpace(latestTag) && IsNewerRelease(latestTag, currentVersion))
                    {
                        System.Windows.MessageBox.Show(
                            $"Foi encontrada uma tag mais nova ({latestTag}), mas ainda sem release instalavel.\n\n" +
                            "Publique uma release no GitHub com o instalador Setup.exe para aparecer na atualizacao automatica.",
                            "Atualizacao",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        StatusMessage = "Tag nova encontrada, aguardando release instalavel";
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Nao foi possivel obter informacoes da ultima versao.", "Atualizacao", MessageBoxButton.OK, MessageBoxImage.Warning);
                        StatusMessage = "Falha ao verificar atualizacoes";
                    }

                    return;
                }

                if (!IsNewerRelease(latest.TagName, currentVersion))
                {
                    System.Windows.MessageBox.Show(
                        $"Voce ja esta na versao mais recente ({currentVersion}).\nUltima release publicada: {latest.TagName}.",
                        "Atualizacao",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    StatusMessage = "Aplicativo ja esta atualizado";
                    IsUpdateAvailable = false;
                    return;
                }

                IsUpdateAvailable = true;
                var releaseNotes = BuildReleaseNotesPreview(latest.Body);
                var confirm = System.Windows.MessageBox.Show(
                    $"Nova versao encontrada: {latest.TagName}.\nVersao atual: {currentVersion}.\n\n" +
                    $"Melhorias:\n{releaseNotes}\n\n" +
                    "Deseja baixar e atualizar agora automaticamente?",
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

        private async System.Threading.Tasks.Task CheckForUpdatesSilentAsync()
        {
            try
            {
                var currentVersion = GetCurrentAppVersion();

                var manifest = await GetLatestUpdateManifestAsync();
                if (manifest != null)
                {
                    if (IsNewerRelease(manifest.Version, currentVersion))
                    {
                        IsUpdateAvailable = true;
                        StatusMessage = $"Nova versao disponivel: {manifest.Version}";
                        return;
                    }
                }

                var latest = await GetLatestInstallableReleaseAsync();
                if (latest != null && IsNewerRelease(latest.TagName, currentVersion))
                {
                    IsUpdateAvailable = true;
                    StatusMessage = $"Nova versao disponivel: {latest.TagName}";
                    return;
                }

                IsUpdateAvailable = false;
            }
            catch
            {
                IsUpdateAvailable = false;
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

        private async Task InstallManifestAsync(UpdateManifest manifest, string operation)
        {
            if (!Uri.TryCreate(manifest.InstallerUrl, UriKind.Absolute, out var installerUri))
            {
                throw new InvalidOperationException("Manifesto de atualizacao sem URL valida de instalador.");
            }

            var fileName = Path.GetFileName(installerUri.LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"MeuGestorVODs.Setup.{NormalizeTag(manifest.Version)}.exe";
            }

            var downloadPath = Path.Combine(Path.GetTempPath(), "MeuGestorVODs", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(downloadPath)!);

            StatusMessage = $"{operation}: baixando {manifest.Version}...";
            await DownloadFileWithProgressAsync(manifest.InstallerUrl, downloadPath);

            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                var calculated = ComputeFileSha256(downloadPath);
                if (!string.Equals(calculated, NormalizeHex(manifest.Sha256), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Falha na validacao de integridade (SHA256 divergente).");
                }
            }

            StatusMessage = $"{operation}: abrindo instalador...";
            Process.Start(new ProcessStartInfo
            {
                FileName = downloadPath,
                UseShellExecute = true
            });

            System.Windows.MessageBox.Show(
                $"Instalador da versao {manifest.Version} aberto com sucesso.\n\nFinalize o assistente para concluir.",
                operation,
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            System.Windows.Application.Current.Shutdown();
        }

        private async Task<UpdateManifest?> GetLatestUpdateManifestAsync()
        {
            var urls = new[] { UpdateManifestPrimaryUrl, UpdateManifestFallbackUrl };
            foreach (var url in urls)
            {
                try
                {
                    using var response = await _releaseClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (manifest == null)
                        continue;

                    manifest.Version = NormalizeTag(manifest.Version);
                    if (string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.InstallerUrl))
                        continue;

                    if (!Uri.TryCreate(manifest.InstallerUrl, UriKind.Absolute, out var installerUri))
                        continue;

                    if (!installerUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                        !installerUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                        continue;

                    return manifest;
                }
                catch
                {
                }
            }

            return null;
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
                    break;

                await output.WriteAsync(buffer.AsMemory(0, read));
                readTotal += read;

                if (totalBytes > 0)
                {
                    var pct = (int)Math.Round((double)readTotal / totalBytes * 100);
                    StatusMessage = $"Baixando atualizacao... {pct}%";
                }
            }
        }

        private static string BuildManifestNotesPreview(UpdateManifest manifest)
        {
            if (manifest.Notes.Count > 0)
            {
                var selected = manifest.Notes
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => $"- {x.Trim()}")
                    .Take(6)
                    .ToList();

                if (selected.Count > 0)
                    return string.Join("\n", selected);
            }

            return BuildReleaseNotesPreview(manifest.Body);
        }

        private static string ComputeFileSha256(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash);
        }

        private static string NormalizeHex(string value)
        {
            return value.Replace(" ", string.Empty).Replace("-", string.Empty).Trim();
        }

        private async Task<GitHubRelease?> GetLatestReleaseAsync()
        {
            try
            {
                using var response = await _releaseClient.GetAsync($"{RepoApiBase}/releases/latest");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<GitHubRelease>(json);
            }
            catch
            {
                return null;
            }
        }

        private async Task<GitHubRelease?> GetLatestInstallableReleaseAsync()
        {
            var latest = await GetLatestReleaseAsync();
            if (latest != null && HasSetupInstaller(latest))
                return latest;

            var releases = await GetStableReleasesAsync();
            return releases.FirstOrDefault(HasSetupInstaller);
        }

        private async Task<List<GitHubRelease>> GetStableReleasesAsync()
        {
            using var response = await _releaseClient.GetAsync($"{RepoApiBase}/releases?per_page=30");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var all = JsonSerializer.Deserialize<List<GitHubRelease>>(json) ?? new List<GitHubRelease>();
            return all.Where(r => !r.Draft && !r.Prerelease).ToList();
        }

        private async Task<string?> GetLatestTagNameAsync()
        {
            try
            {
                using var response = await _releaseClient.GetAsync($"{RepoApiBase}/tags?per_page=20");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var tags = JsonSerializer.Deserialize<List<GitHubTag>>(json) ?? new List<GitHubTag>();
                return tags.FirstOrDefault()?.Name;
            }
            catch
            {
                return null;
            }
        }

        private static bool HasSetupInstaller(GitHubRelease release)
        {
            return release.Assets.Any(a =>
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                a.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildReleaseNotesPreview(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "- Sem notas de melhorias publicadas.";

            var selected = new List<string>();
            var lines = body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("```", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("+ ", StringComparison.Ordinal))
                    line = line[2..].Trim();
                else if (line.Length > 3 && char.IsDigit(line[0]) && line[1] == '.' && line[2] == ' ')
                    line = line[3..].Trim();

                line = line.Replace("**", string.Empty).Replace("`", string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                selected.Add($"- {line}");
                if (selected.Count >= 6)
                    break;
            }

            return selected.Count == 0
                ? "- Sem notas de melhorias publicadas."
                : string.Join("\n", selected);
        }

        private static bool IsNewerRelease(string releaseTag, string currentVersion)
        {
            var releaseVersion = ParseVersion(NormalizeTag(releaseTag));
            var installedVersion = ParseVersion(NormalizeTag(currentVersion));

            if (releaseVersion != null && installedVersion != null)
                return releaseVersion > installedVersion;

            return !string.Equals(NormalizeTag(releaseTag), NormalizeTag(currentVersion), StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareReleaseTags(string leftTag, string rightTag)
        {
            var left = ParseVersion(NormalizeTag(leftTag));
            var right = ParseVersion(NormalizeTag(rightTag));

            if (left != null && right != null)
                return left.CompareTo(right);

            return string.Compare(NormalizeTag(leftTag), NormalizeTag(rightTag), StringComparison.OrdinalIgnoreCase);
        }

        private static Version? ParseVersion(string value)
        {
            var cleaned = value.Trim();
            var idx = cleaned.IndexOf('-', StringComparison.Ordinal);
            if (idx > 0)
                cleaned = cleaned[..idx];

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
                    return txt;
            }

            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
            {
                var info = FileVersionInfo.GetVersionInfo(processPath);
                if (!string.IsNullOrWhiteSpace(info.ProductVersion))
                    return info.ProductVersion!;
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

            [JsonPropertyName("body")]
            public string Body { get; set; } = string.Empty;

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

        private sealed class GitHubTag
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;
        }

        private sealed class UpdateManifest
        {
            [JsonPropertyName("version")]
            public string Version { get; set; } = string.Empty;

            [JsonPropertyName("installerUrl")]
            public string InstallerUrl { get; set; } = string.Empty;

            [JsonPropertyName("sha256")]
            public string Sha256 { get; set; } = string.Empty;

            [JsonPropertyName("notes")]
            public List<string> Notes { get; set; } = new List<string>();

            [JsonPropertyName("body")]
            public string Body { get; set; } = string.Empty;
        }
    }
}
