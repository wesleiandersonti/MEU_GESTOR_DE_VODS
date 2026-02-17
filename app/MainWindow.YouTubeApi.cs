using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MeuGestorVODs
{
    public partial class MainWindow
    {
        private async Task<YouTubeApiExportResult> GenerateYouTubeM3uFromApiAsync(string apiBaseUrl, string apiKey)
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(40)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd("MeuGestorVODs");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.Remove("X-API-Key");
                client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            }

            var exportUri = BuildYouTubeApiUri(apiBaseUrl, "/export/m3u");
            using var exportResponse = await client.PostAsync(exportUri, new StringContent(string.Empty, Encoding.UTF8, "application/json"));
            var exportBody = await exportResponse.Content.ReadAsStringAsync();
            if (!exportResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(BuildApiFailureMessage(exportResponse.StatusCode, exportBody));
            }

            var channelsCount = TryExtractChannelsCount(exportBody);

            var latestUri = BuildYouTubeApiUri(apiBaseUrl, "/export/m3u/latest");
            using var latestResponse = await client.GetAsync(latestUri);
            var latestBody = await latestResponse.Content.ReadAsStringAsync();
            if (!latestResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(BuildApiFailureMessage(latestResponse.StatusCode, latestBody));
            }

            var contentText = TryExtractLatestContent(latestBody, out var latestChannelsCount, out var downloadPath);
            if (latestChannelsCount > 0)
            {
                channelsCount = latestChannelsCount;
            }

            if (string.IsNullOrWhiteSpace(contentText) && !string.IsNullOrWhiteSpace(downloadPath))
            {
                var downloadUri = BuildYouTubeApiUri(apiBaseUrl, downloadPath);
                using var downloadResponse = await client.GetAsync(downloadUri);
                var downloadBody = await downloadResponse.Content.ReadAsStringAsync();
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(BuildApiFailureMessage(downloadResponse.StatusCode, downloadBody));
                }

                contentText = downloadBody;
            }

            if (string.IsNullOrWhiteSpace(contentText))
            {
                throw new InvalidOperationException("A API nao retornou o conteudo da playlist M3U.");
            }

            return new YouTubeApiExportResult(contentText, channelsCount);
        }

        private YouTubeLiveApiConfig LoadYouTubeLiveApiConfig()
        {
            var config = new YouTubeLiveApiConfig
            {
                BaseUrl = YouTubeLiveApiDefaultBaseUrl
            };

            try
            {
                var path = GetYouTubeLiveApiConnectionFilePath();
                if (!File.Exists(path))
                {
                    return config;
                }

                var json = File.ReadAllText(path);
                var stored = JsonSerializer.Deserialize<YouTubeLiveApiConfig>(json);
                if (stored == null)
                {
                    return config;
                }

                stored.ApiKey = UnprotectSecret(stored.ApiKeyProtected);
                if (string.IsNullOrWhiteSpace(stored.BaseUrl))
                {
                    stored.BaseUrl = YouTubeLiveApiDefaultBaseUrl;
                }

                return stored;
            }
            catch
            {
                return config;
            }
        }

        private void SaveYouTubeLiveApiConfig(YouTubeLiveApiConfig config)
        {
            var path = GetYouTubeLiveApiConnectionFilePath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            config.ApiKeyProtected = ProtectSecret(config.ApiKey);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        private static string GetYouTubeLiveApiConnectionFilePath()
        {
            var baseFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MeuGestorVODs");
            return Path.Combine(baseFolder, YouTubeLiveApiConnectionFileName);
        }

        private static Uri BuildYouTubeApiUri(string baseUrl, string route)
        {
            var normalizedBase = baseUrl.Trim();
            if (!normalizedBase.EndsWith("/", StringComparison.Ordinal))
            {
                normalizedBase += "/";
            }

            var baseUri = new Uri(normalizedBase, UriKind.Absolute);
            if (Uri.TryCreate(route, UriKind.Absolute, out var absoluteRoute))
            {
                return absoluteRoute;
            }

            var sanitizedRoute = route.StartsWith("/", StringComparison.Ordinal) ? route[1..] : route;
            return new Uri(baseUri, sanitizedRoute);
        }

        private static int TryExtractChannelsCount(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("channels_count", out var countProperty) && countProperty.ValueKind == JsonValueKind.Number && countProperty.TryGetInt32(out var count))
                {
                    return Math.Max(count, 0);
                }

                if (root.TryGetProperty("channelsCount", out var countAltProperty) && countAltProperty.ValueKind == JsonValueKind.Number && countAltProperty.TryGetInt32(out var countAlt))
                {
                    return Math.Max(countAlt, 0);
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private static string? TryExtractLatestContent(string json, out int channelsCount, out string? downloadPath)
        {
            channelsCount = 0;
            downloadPath = null;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("content", out var contentProperty) && contentProperty.ValueKind == JsonValueKind.String)
                {
                    var content = contentProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        if (root.TryGetProperty("export", out var exportProperty) && exportProperty.ValueKind == JsonValueKind.Object)
                        {
                            if (exportProperty.TryGetProperty("channels_count", out var channelsProperty) && channelsProperty.ValueKind == JsonValueKind.Number && channelsProperty.TryGetInt32(out var nestedCount))
                            {
                                channelsCount = Math.Max(nestedCount, 0);
                            }
                        }

                        if (root.TryGetProperty("download_url", out var downloadUrlProperty) && downloadUrlProperty.ValueKind == JsonValueKind.String)
                        {
                            downloadPath = downloadUrlProperty.GetString();
                        }

                        return content;
                    }
                }

                if (root.TryGetProperty("download_url", out var downloadProperty) && downloadProperty.ValueKind == JsonValueKind.String)
                {
                    downloadPath = downloadProperty.GetString();
                }

                if (root.TryGetProperty("export", out var exportNode) && exportNode.ValueKind == JsonValueKind.Object)
                {
                    if (exportNode.TryGetProperty("channels_count", out var channelsProperty) && channelsProperty.ValueKind == JsonValueKind.Number && channelsProperty.TryGetInt32(out var nestedCount))
                    {
                        channelsCount = Math.Max(nestedCount, 0);
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string BuildApiFailureMessage(HttpStatusCode statusCode, string responseBody)
        {
            var message = TryExtractApiMessage(responseBody);
            if (string.IsNullOrWhiteSpace(message))
            {
                message = responseBody;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                message = "Resposta vazia da API.";
            }

            return $"API retornou {(int)statusCode} ({statusCode}): {message}";
        }

        private static string? TryExtractApiMessage(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;
                if (root.TryGetProperty("message", out var messageProperty) && messageProperty.ValueKind == JsonValueKind.String)
                {
                    return messageProperty.GetString();
                }

                if (root.TryGetProperty("error", out var errorProperty) && errorProperty.ValueKind == JsonValueKind.String)
                {
                    return errorProperty.GetString();
                }
            }
            catch
            {
            }

            return responseBody.Length <= 280 ? responseBody : responseBody[..280] + "...";
        }
    }

    internal sealed class YouTubeApiExportResult
    {
        public YouTubeApiExportResult(string content, int channelsCount)
        {
            Content = content;
            ChannelsCount = channelsCount;
        }

        public string Content { get; }
        public int ChannelsCount { get; }
    }

    public class YouTubeLiveApiConfig
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKeyProtected { get; set; } = string.Empty;

        [JsonIgnore]
        public string ApiKey { get; set; } = string.Empty;
    }
}
