using System;

namespace MeuGestorVODs;

internal static class UrlProbeSanitizer
{
    public static string Sanitize(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var trimmed = url.Trim();
        var pipeIndex = trimmed.IndexOf('|');
        if (pipeIndex > 0)
        {
            trimmed = trimmed[..pipeIndex].Trim();
        }

        return trimmed;
    }

    public static bool TryGetHttpProbeUri(string url, out string sanitizedUrl, out Uri? uri)
    {
        sanitizedUrl = Sanitize(url);
        uri = null;

        if (string.IsNullOrWhiteSpace(sanitizedUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(sanitizedUrl, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!parsed.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsed;
        return true;
    }
}
