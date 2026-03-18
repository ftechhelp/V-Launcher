using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace V_Launcher.Services;

/// <summary>
/// Checks GitHub releases and starts installer-based updates.
/// </summary>
public class ApplicationUpdateService : IApplicationUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Guid WinTrustActionGenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private readonly HttpClient _httpClient;
    private readonly ILogger<ApplicationUpdateService> _logger;
    private readonly string _gitHubApiBase;
    private readonly string _repository;
    private readonly string? _accessToken;
    private readonly HashSet<string> _allowedSignerSubjects;
    private readonly HashSet<string> _allowedSignerThumbprints;
    private readonly Func<string, bool> _installerSignatureVerifier;
    private readonly Func<ProcessStartInfo, Process?> _processStarter;

    public ApplicationUpdateService(HttpClient httpClient, ILogger<ApplicationUpdateService> logger)
        : this(httpClient, logger, null, null)
    {
    }

    public ApplicationUpdateService(
        HttpClient httpClient,
        ILogger<ApplicationUpdateService> logger,
        Func<string, bool>? installerSignatureVerifier,
        Func<ProcessStartInfo, Process?>? processStarter)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _gitHubApiBase = GetRequiredEnvironmentValue("VLAUNCHER_GITHUB_API_BASE", "https://api.github.com");
        _repository = GetRequiredEnvironmentValue("VLAUNCHER_GITHUB_REPOSITORY", "ftechhelp/V-Launcher");
        _accessToken = GetOptionalEnvironmentValue("VLAUNCHER_GITHUB_TOKEN")
            ?? GetOptionalEnvironmentValue("VLAUNCHER_GITLAB_TOKEN");

        _allowedSignerSubjects = ParseDelimitedEnvironmentValue("VLAUNCHER_ALLOWED_SIGNER_SUBJECTS");
        _allowedSignerThumbprints = ParseDelimitedEnvironmentValue("VLAUNCHER_ALLOWED_SIGNER_THUMBPRINTS")
            .Select(NormalizeThumbprint)
            .Where(static thumbprint => !string.IsNullOrWhiteSpace(thumbprint))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _installerSignatureVerifier = installerSignatureVerifier ?? VerifyInstallerSignature;
        _processStarter = processStarter ?? Process.Start;
    }

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersion();

        try
        {
            using var request = CreateApiRequest(HttpMethod.Get, BuildLatestReleaseUrl());
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Update check request failed with status code {StatusCode}", response.StatusCode);
                return new UpdateCheckResult(false, currentVersion, null, null, null, null, null);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var latestRelease = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(stream, JsonOptions, cancellationToken);

            if (latestRelease is null || string.IsNullOrWhiteSpace(latestRelease.TagName))
            {
                _logger.LogWarning("Latest release payload was empty or missing a tag name.");
                return new UpdateCheckResult(false, currentVersion, null, null, null, null, null);
            }

            if (!TryParseVersion(latestRelease.TagName, out var latestVersion))
            {
                _logger.LogWarning("Could not parse latest tag version: {TagName}", latestRelease.TagName);
                return new UpdateCheckResult(false, currentVersion, null, latestRelease.TagName, null, null, null);
            }

            var installerAsset = GetInstallerAsset(latestRelease);
            if (installerAsset is null || string.IsNullOrWhiteSpace(installerAsset.BrowserDownloadUrl))
            {
                return new UpdateCheckResult(false, currentVersion, latestVersion, latestRelease.TagName, null, null, null);
            }

            var installerSha256 = ParseSha256Digest(installerAsset.Digest);
            var installerChecksumUrl = string.IsNullOrWhiteSpace(installerSha256)
                ? GetChecksumAssetUrl(latestRelease, installerAsset.Name)
                : null;

            if (string.IsNullOrWhiteSpace(installerSha256) && string.IsNullOrWhiteSpace(installerChecksumUrl))
            {
                _logger.LogWarning("Latest release installer asset {AssetName} did not include SHA-256 integrity metadata.", installerAsset.Name);
                return new UpdateCheckResult(false, currentVersion, latestVersion, latestRelease.TagName, installerAsset.BrowserDownloadUrl, null, null);
            }

            var isUpdateAvailable = latestVersion > currentVersion;

            return new UpdateCheckResult(
                isUpdateAvailable,
                currentVersion,
                latestVersion,
                latestRelease.TagName,
                installerAsset.BrowserDownloadUrl,
                installerSha256,
                installerChecksumUrl);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error while checking for updates.");
            return new UpdateCheckResult(false, currentVersion, null, null, null, null, null);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid GitHub release response while checking for updates.");
            return new UpdateCheckResult(false, currentVersion, null, null, null, null, null);
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

        string? installerPath = null;

        try
        {
            using var response = await _httpClient.GetAsync(updateCheckResult.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Installer download failed with status code {StatusCode}", response.StatusCode);
                return false;
            }

            installerPath = BuildInstallerPath(updateCheckResult.InstallerUrl);

            await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = File.Create(installerPath))
            {
                await stream.CopyToAsync(fileStream, cancellationToken);
            }

            var expectedSha256 = await ResolveInstallerSha256Async(updateCheckResult, cancellationToken);
            if (string.IsNullOrWhiteSpace(expectedSha256))
            {
                _logger.LogWarning("Installer metadata did not provide a usable SHA-256 checksum.");
                DeleteInstallerIfPresent(installerPath);
                return false;
            }

            var actualSha256 = await ComputeFileSha256Async(installerPath, cancellationToken);
            if (!HashesEqual(expectedSha256, actualSha256))
            {
                _logger.LogWarning("Installer checksum verification failed for path {InstallerPath}", installerPath);
                DeleteInstallerIfPresent(installerPath);
                return false;
            }

            if (!_installerSignatureVerifier(installerPath))
            {
                _logger.LogWarning("Installer signature verification failed for path {InstallerPath}", installerPath);
                DeleteInstallerIfPresent(installerPath);
                return false;
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            };

            var process = _processStarter(processStartInfo);
            var started = process is not null;

            if (!started)
            {
                _logger.LogWarning("Installer process failed to start for path {InstallerPath}", installerPath);
                DeleteInstallerIfPresent(installerPath);
            }

            return started;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or InvalidDataException or Win32Exception or CryptographicException)
        {
            _logger.LogError(ex, "Failed to download or start the installer update.");
            DeleteInstallerIfPresent(installerPath);
            return false;
        }
    }

    private Uri BuildLatestReleaseUrl()
    {
        var encodedRepository = string.Join('/', _repository
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));

        return new Uri($"{_gitHubApiBase.TrimEnd('/')}/repos/{encodedRepository}/releases/latest");
    }

    private HttpRequestMessage CreateApiRequest(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.UserAgent.ParseAdd("V-Launcher");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        }

        return request;
    }

    private static GitHubReleaseAssetDto? GetInstallerAsset(GitHubReleaseDto latestRelease)
    {
        return latestRelease.Assets?
            .FirstOrDefault(asset =>
                !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl)
                && !string.IsNullOrWhiteSpace(asset.Name)
                && (asset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
                    || asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)));
    }

    private static string? GetChecksumAssetUrl(GitHubReleaseDto latestRelease, string? installerAssetName)
    {
        if (latestRelease.Assets is null || string.IsNullOrWhiteSpace(installerAssetName))
        {
            return null;
        }

        var checksumAsset = latestRelease.Assets.FirstOrDefault(asset =>
            !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl)
            && !string.IsNullOrWhiteSpace(asset.Name)
            && (
                string.Equals(asset.Name, installerAssetName + ".sha256", StringComparison.OrdinalIgnoreCase)
                || string.Equals(asset.Name, installerAssetName + ".sha256.txt", StringComparison.OrdinalIgnoreCase)
                || (asset.Name.Contains(Path.GetFileNameWithoutExtension(installerAssetName), StringComparison.OrdinalIgnoreCase)
                    && asset.Name.Contains("sha256", StringComparison.OrdinalIgnoreCase))));

        return checksumAsset?.BrowserDownloadUrl;
    }

    private async Task<string?> ResolveInstallerSha256Async(UpdateCheckResult updateCheckResult, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(updateCheckResult.InstallerSha256))
        {
            return NormalizeSha256(updateCheckResult.InstallerSha256);
        }

        if (string.IsNullOrWhiteSpace(updateCheckResult.InstallerChecksumUrl))
        {
            return null;
        }

        using var request = CreateApiRequest(HttpMethod.Get, new Uri(updateCheckResult.InstallerChecksumUrl));
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Checksum download failed with status code {StatusCode}", response.StatusCode);
            return null;
        }

        var checksumContent = await response.Content.ReadAsStringAsync(cancellationToken);
        foreach (var line in checksumContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var firstToken = line.Split(new[] { ' ', '\t', '*' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            var parsedChecksum = ParseSha256Digest(firstToken);

            if (!string.IsNullOrWhiteSpace(parsedChecksum))
            {
                return parsedChecksum;
            }
        }

        return null;
    }

    private static string? ParseSha256Digest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            return null;
        }

        var normalized = digest.Trim();
        if (normalized.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["sha256:".Length..];
        }

        return IsSha256Hex(normalized) ? normalized.ToUpperInvariant() : null;
    }

    private static async Task<string> ComputeFileSha256Async(string installerPath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(installerPath);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private bool VerifyInstallerSignature(string installerPath)
    {
        if (!HasValidAuthenticodeSignature(installerPath))
        {
            return false;
        }

        if (_allowedSignerSubjects.Count == 0 && _allowedSignerThumbprints.Count == 0)
        {
            return true;
        }

        try
        {
            using var signerCertificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(installerPath));

            if (_allowedSignerThumbprints.Count > 0)
            {
                var thumbprint = NormalizeThumbprint(signerCertificate.Thumbprint);
                if (!_allowedSignerThumbprints.Contains(thumbprint))
                {
                    _logger.LogWarning("Installer signer thumbprint {Thumbprint} is not allowed.", thumbprint);
                    return false;
                }
            }

            if (_allowedSignerSubjects.Count > 0)
            {
                var subject = signerCertificate.Subject;
                if (!_allowedSignerSubjects.Any(allowedSubject => subject.Contains(allowedSubject, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Installer signer subject {Subject} is not allowed.", subject);
                    return false;
                }
            }

            return true;
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Could not read installer signer certificate.");
            return false;
        }
    }

    private static bool HasValidAuthenticodeSignature(string installerPath)
    {
        using var fileInfo = new WinTrustFileInfo(installerPath);
        using var trustData = new WinTrustData(fileInfo);

        return WinVerifyTrust(IntPtr.Zero, WinTrustActionGenericVerifyV2, trustData) == 0;
    }

    private static bool HashesEqual(string expectedHash, string actualHash)
    {
        var normalizedExpected = NormalizeSha256(expectedHash);
        var normalizedActual = NormalizeSha256(actualHash);

        return normalizedExpected is not null
            && normalizedActual is not null
            && CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(normalizedExpected),
                Convert.FromHexString(normalizedActual));
    }

    private static string? NormalizeSha256(string? value)
    {
        return ParseSha256Digest(value);
    }

    private static bool IsSha256Hex(string value)
    {
        return value.Length == 64 && value.All(Uri.IsHexDigit);
    }

    private static string NormalizeThumbprint(string? thumbprint)
    {
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            return string.Empty;
        }

        return thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }

    private static HashSet<string> ParseDelimitedEnvironmentValue(string variableName)
    {
        var value = GetOptionalEnvironmentValue(variableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return value
            .Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetOptionalEnvironmentValue(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private void DeleteInstallerIfPresent(string? installerPath)
    {
        if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
        {
            return;
        }

        try
        {
            File.Delete(installerPath);
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Could not delete temporary installer file {InstallerPath}", installerPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogDebug(ex, "Could not delete temporary installer file {InstallerPath}", installerPath);
        }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid actionId, WinTrustData winTrustData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class WinTrustFileInfo : IDisposable
    {
        public WinTrustFileInfo(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            StructSize = (uint)Marshal.SizeOf(typeof(WinTrustFileInfo));
            FilePathPointer = Marshal.StringToCoTaskMemUni(filePath);
        }

        public uint StructSize;
        public IntPtr FilePathPointer;
        public IntPtr FileHandle = IntPtr.Zero;
        public IntPtr KnownSubject = IntPtr.Zero;

        public void Dispose()
        {
            if (FilePathPointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(FilePathPointer);
                FilePathPointer = IntPtr.Zero;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class WinTrustData : IDisposable
    {
        public WinTrustData(WinTrustFileInfo fileInfo)
        {
            ArgumentNullException.ThrowIfNull(fileInfo);

            StructSize = (uint)Marshal.SizeOf(typeof(WinTrustData));
            UnionChoice = WinTrustUnionChoice.File;
            UiChoice = WinTrustUiChoice.None;
            RevocationChecks = WinTrustRevocationChecks.None;
            StateAction = WinTrustStateAction.Ignore;
            ProviderFlags = WinTrustProviderFlags.RevocationCheckChainExcludeRoot | WinTrustProviderFlags.CacheOnlyUrlRetrieval;
            UiContext = WinTrustUiContext.Execute;

            FileInfoPointer = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(WinTrustFileInfo)));
            Marshal.StructureToPtr(fileInfo, FileInfoPointer, false);
        }

        public uint StructSize;
        public IntPtr PolicyCallbackData = IntPtr.Zero;
        public IntPtr SipClientData = IntPtr.Zero;
        public WinTrustUiChoice UiChoice;
        public WinTrustRevocationChecks RevocationChecks;
        public WinTrustUnionChoice UnionChoice;
        public IntPtr FileInfoPointer;
        public WinTrustStateAction StateAction;
        public IntPtr StateData = IntPtr.Zero;
        public string? UrlReference = null;
        public WinTrustProviderFlags ProviderFlags;
        public WinTrustUiContext UiContext;

        public void Dispose()
        {
            if (FileInfoPointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(FileInfoPointer);
                FileInfoPointer = IntPtr.Zero;
            }
        }
    }

    private enum WinTrustUiChoice : uint
    {
        None = 2
    }

    private enum WinTrustRevocationChecks : uint
    {
        None = 0
    }

    private enum WinTrustUnionChoice : uint
    {
        File = 1
    }

    private enum WinTrustStateAction : uint
    {
        Ignore = 0
    }

    [Flags]
    private enum WinTrustProviderFlags : uint
    {
        RevocationCheckChainExcludeRoot = 0x00000080,
        CacheOnlyUrlRetrieval = 0x00001000
    }

    private enum WinTrustUiContext : uint
    {
        Execute = 0
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
        return GetOptionalEnvironmentValue(variableName) ?? fallbackValue;
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        public List<GitHubReleaseAssetDto>? Assets { get; set; }
    }

    private sealed class GitHubReleaseAssetDto
    {
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        public string? Digest { get; set; }
    }
}
