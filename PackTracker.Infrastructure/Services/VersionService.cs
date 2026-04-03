using System;
using System.IO;
using System.Linq;
using System.Reflection;
using PackTracker.Application.Interfaces;

namespace PackTracker.Infrastructure.Services;

/// <summary>
/// Provides application version information from assembly metadata.
/// </summary>
public class VersionService : IVersionService
{
    /// <summary>
    /// Gets the current application version in format "vX.Y".
    /// </summary>
    public string GetVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;

            if (version != null)
            {
                return $"v{version.Major}.{version.Minor}";
            }

            // Fallback
            return "v1.0";
        }
        catch
        {
            return "v1.0";
        }
    }

    /// <summary>
    /// Gets the build date of the application from assembly metadata or file system.
    /// </summary>
    public string GetBuildDate()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Try to get build date from assembly metadata
            var buildDate = assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
                .OfType<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "BuildDate")?.Value;

            if (!string.IsNullOrEmpty(buildDate))
            {
                return buildDate;
            }

            // Fallback to file creation date
            var baseDirectory = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDirectory))
            {
                var fileInfo = new FileInfo(baseDirectory);
                return fileInfo.CreationTime.ToString("yyyy-MM-dd");
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the full version string with build date (e.g., "v1.0 (2024-12-01)").
    /// </summary>
    public string GetFullVersionString()
    {
        var version = GetVersion();
        var buildDate = GetBuildDate();

        if (!string.IsNullOrEmpty(buildDate))
        {
            return $"{version} ({buildDate})";
        }

        return version;
    }
}
