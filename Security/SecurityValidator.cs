using System;
using System.IO;
using System.Linq;

namespace MeuGestorVODs.Security;

public static class SecurityValidator
{
    // Whitelist of allowed URL schemes
    private static readonly string[] AllowedSchemes = { "http", "https" };

    // Blacklist of suspicious patterns in URLs
    private static readonly string[] SuspiciousPatterns =
    {
        "..")
        , "//", "../", "..\\", "%2e%2e", "%252e", 
        "0x2e0x2e", "0x2e", "%c0%ae", "%e0%80%ae"
    };

    // Invalid characters for file names
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    // Invalid characters for paths
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();

    /// <summary>
    /// Validates an M3U URL for security
    /// </summary>
    public static ValidationResult ValidateM3UUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return ValidationResult.Failure("URL cannot be empty");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return ValidationResult.Failure("Invalid URL format");
        }

        if (!AllowedSchemes.Contains(uri.Scheme.ToLowerInvariant()))
        {
            return ValidationResult.Failure($"URL scheme '{uri.Scheme}' is not allowed. Only HTTP and HTTPS are supported.");
        }

        // Check for suspicious patterns
        var urlLower = url.ToLowerInvariant();
        foreach (var pattern in SuspiciousPatterns)
        {
            if (urlLower.Contains(pattern))
            {
                return ValidationResult.Failure("URL contains suspicious patterns");
            }
        }

        // Validate host (basic check)
        if (string.IsNullOrEmpty(uri.Host) || uri.Host.Length > 253)
        {
            return ValidationResult.Failure("Invalid host in URL");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a VOD entry URL
    /// </summary>
    public static ValidationResult ValidateVodUrl(string? url)
    {
        var result = ValidateM3UUrl(url);
        if (!result.IsValid)
        {
            return result;
        }

        // Additional checks for VOD URLs
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            // Check for very long paths (potential DoS)
            if (uri.AbsolutePath.Length > 2048)
            {
                return ValidationResult.Failure("URL path is too long");
            }

            // Check for null bytes
            if (url.Contains('\0'))
            {
                return ValidationResult.Failure("URL contains null bytes");
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Sanitizes a file name to prevent directory traversal
    /// </summary>
    public static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "unnamed";
        }

        // Remove invalid characters
        foreach (var c in InvalidFileNameChars)
        {
            fileName = fileName.Replace(c, '_');
        }

        // Remove path traversal attempts
        fileName = fileName.Replace("..", "_");
        fileName = fileName.Replace("/", "_");
        fileName = fileName.Replace("\\", "_");

        // Trim and limit length
        fileName = fileName.Trim();
        if (fileName.Length > 200)
        {
            fileName = fileName[..200];
        }

        // Ensure it's not empty after sanitization
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "unnamed";
        }

        return fileName;
    }

    /// <summary>
    /// Validates and sanitizes a download path
    /// </summary>
    public static ValidationResult<string> ValidateDownloadPath(string? basePath, string fileName)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return ValidationResult<string>.Failure("Download path cannot be empty");
        }

        // Check for path traversal in base path
        var normalizedBase = Path.GetFullPath(basePath);
        if (!Directory.Exists(normalizedBase))
        {
            try
            {
                Directory.CreateDirectory(normalizedBase);
            }
            catch (Exception ex)
            {
                return ValidationResult<string>.Failure($"Cannot create download directory: {ex.Message}");
            }
        }

        // Sanitize filename
        var sanitizedFileName = SanitizeFileName(fileName);

        // Combine and verify
        var fullPath = Path.Combine(normalizedBase, sanitizedFileName);
        var normalizedFullPath = Path.GetFullPath(fullPath);

        // Ensure the final path is within the base directory
        if (!normalizedFullPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult<string>.Failure("Path traversal detected");
        }

        // Check path length
        if (normalizedFullPath.Length > 260)
        {
            return ValidationResult<string>.Failure("File path is too long");
        }

        return ValidationResult<string>.Success(normalizedFullPath);
    }

    /// <summary>
    /// Validates configuration values
    /// </summary>
    public static ValidationResult ValidateConfig(AppConfig config)
    {
        if (config.MaxParallelDownloads < 1 || config.MaxParallelDownloads > 10)
        {
            return ValidationResult.Failure("Max parallel downloads must be between 1 and 10");
        }

        if (config.CacheTtlMinutes < 1 || config.CacheTtlMinutes > 1440)
        {
            return ValidationResult.Failure("Cache TTL must be between 1 and 1440 minutes");
        }

        if (!string.IsNullOrEmpty(config.VlcPath) && !File.Exists(config.VlcPath))
        {
            return ValidationResult.Failure("Configured VLC path does not exist");
        }

        return ValidationResult.Success();
    }
}

public class ValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    private ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult Success() => new(true, null);
    public static ValidationResult Failure(string message) => new(false, message);
}

public class ValidationResult<T>
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }
    public T? Value { get; }

    private ValidationResult(bool isValid, T? value, string? errorMessage)
    {
        IsValid = isValid;
        Value = value;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult<T> Success(T value) => new(true, value, null);
    public static ValidationResult<T> Failure(string message) => new(false, default, message);
}
