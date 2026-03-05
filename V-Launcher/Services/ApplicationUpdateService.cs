using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace V_Launcher.Services;

/// <summary>
/// Checks GitLab releases and starts installer-based updates.
/// </summary>
public class ApplicationUpdateService : IApplicationUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<ApplicationUpdateService> _logger;
    private readonly string _gitLabHost;
    private readonly string _projectPath;
    private readonly string? _privateToken;

    public ApplicationUpdateService(HttpClient httpClient, ILogger<ApplicationUpdateService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _gitLabHost = GetRequiredEnvironmentValue("VLAUNCHER_GITLAB_HOST", "https://gitlab.abbotsford.ca");
        _projectPath = GetRequiredEnvironmentValue("VLAUNCHER_GITLAB_PROJECT", "vfontaine/v-launcher");
        _privateToken = Environment.GetEnvironmentVariable("VLAUNCHER_GITLAB_TOKEN");
    }

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersion();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildLatestReleaseUrl());
            request.Headers.UserAgent.ParseAdd("V-Launcher");

            if (!string.IsNullOrWhiteSpace(_privateToken))
            {
                request.Headers.Add("PRIVATE-TOKEN", _privateToken);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Update check request failed with status code {StatusCode}", response.StatusCode);
                return new UpdateCheckResult(false, currentVersion, null, null, null);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var latestRelease = await JsonSerializer.DeserializeAsync<GitLabReleaseDto>(stream, JsonOptions, cancellationToken);

            if (latestRelease is null || string.IsNullOrWhiteSpace(latestRelease.TagName))
            {
                _logger.LogWarning("Latest release payload was empty or missing a tag name.");
                return new UpdateCheckResult(false, currentVersion, null, null, null);
            }

            if (!TryParseVersion(latestRelease.TagName, out var latestVersion))
            {
                _logger.LogWarning("Could not parse latest tag version: {TagName}", latestRelease.TagName);
                return new UpdateCheckResult(false, currentVersion, null, latestRelease.TagName, null);
            }

            var installerUrl = GetInstallerUrl(latestRelease);
            var isUpdateAvailable = latestVersion > currentVersion && !string.IsNullOrWhiteSpace(installerUrl);

            return new UpdateCheckResult(isUpdateAvailable, currentVersion, latestVersion, latestRelease.TagName, installerUrl);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error while checking for updates.");
            return new UpdateCheckResult(false, currentVersion, null, null, null);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid GitLab release response while checking for updates.");
            return new UpdateCheckResult(false, currentVersion, null, null, null);
        }
    }

    /// <inheritdoc />
    public async Task<bool> InstallUpdateAsync(UpdateCheckResult updateCheckResult, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updateCheckResult);

        if (!updateCheckResult.IsUpdateAvailable || string.IsNullOrWhiteSpace(updateCheckResult.InstallerUrl))
        {
            return false;
        }

        try
        {
            using var response = await _httpClient.GetAsync(updateCheckResult.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Installer download failed with status code {StatusCode}", response.StatusCode);
                return false;
            }

            var installerPath = BuildInstallerPath(updateCheckResult.InstallerUrl);

            await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = File.Create(installerPath))
            {
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            };

            var process = Process.Start(processStartInfo);
            var started = process is not null;

            if (!started)
            {
                _logger.LogWarning("Installer process failed to start for path {InstallerPath}", installerPath);
            }

            return started;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException)
        {
            _logger.LogError(ex, "Failed to download or start the installer update.");
            return false;
        }
    }

    private Uri BuildLatestReleaseUrl()
    {
        var encodedProjectPath = Uri.EscapeDataString(_projectPath);
        return new Uri($"{_gitLabHost.TrimEnd('/')}/api/v4/projects/{encodedProjectPath}/releases/permalink/latest");
    }

    private static string GetInstallerUrl(GitLabReleaseDto latestRelease)
    {
        if (latestRelease.Assets?.Links is null)
        {
            return string.Empty;
        }

        var installerLink = latestRelease.Assets.Links.FirstOrDefault(link =>
            !string.IsNullOrWhiteSpace(link.Url) &&
            (link.Url.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
             link.Url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)));

        return installerLink?.DirectAssetUrl ?? installerLink?.Url ?? string.Empty;
    }

    private static string BuildInstallerPath(string installerUrl)
    {
        var uri = new Uri(installerUrl);
        var fileName = Path.GetFileName(uri.LocalPath);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"V-Launcher-Update-{Guid.NewGuid():N}.exe";
        }

        return Path.Combine(Path.GetTempPath(), fileName);
    }

    private static Version GetCurrentVersion()
    {
        var assemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version
                              ?? Assembly.GetExecutingAssembly().GetName().Version;

        return assemblyVersion ?? new Version(1, 0, 0, 0);
    }

    private static bool TryParseVersion(string tagName, out Version version)
    {
        var normalized = tagName.Trim();

        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        var prereleaseSeparator = normalized.IndexOf('-');
        if (prereleaseSeparator >= 0)
        {
            normalized = normalized[..prereleaseSeparator];
        }

        return Version.TryParse(normalized, out version!);
    }

    private static string GetRequiredEnvironmentValue(string variableName, string fallbackValue)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? fallbackValue : value.Trim();
    }

    private sealed class GitLabReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        public GitLabAssetsDto? Assets { get; set; }
    }

    private sealed class GitLabAssetsDto
    {
        public List<GitLabAssetLinkDto>? Links { get; set; }
    }

    private sealed class GitLabAssetLinkDto
    {
        public string? Url { get; set; }

        [JsonPropertyName("direct_asset_url")]
        public string? DirectAssetUrl { get; set; }
    }
}
