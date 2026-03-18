using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using V_Launcher.Services;

namespace V_LauncherTests.Services;

public class ApplicationUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_WhenNewerReleaseHasInstaller_ShouldReportUpdateAvailable()
    {
        // Arrange
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("installer-bits")));
        var json = """
        {
          "tag_name": "v99.0.0",
          "assets": [
            {
              "name": "V-Launcher-setup.exe",
              "browser_download_url": "https://example.test/V-Launcher-setup.exe",
              "digest": "sha256:EXPECTED_HASH"
            }
          ]
        }
        """.Replace("EXPECTED_HASH", expectedHash, StringComparison.Ordinal);

        using var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, json));
        var service = new ApplicationUpdateService(httpClient, new TestLogger<ApplicationUpdateService>());

        // Act
        var result = await service.CheckForUpdatesAsync();

        // Assert
        Assert.True(result.IsUpdateAvailable);
        Assert.NotNull(result.LatestVersion);
        Assert.Equal(new Version(99, 0, 0), result.LatestVersion);
        Assert.Equal("https://example.test/V-Launcher-setup.exe", result.InstallerUrl);
        Assert.Equal(expectedHash, result.InstallerSha256);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenReleaseHasNoIntegrityMetadata_ShouldReportNoUpdate()
    {
        // Arrange
        var json = """
        {
          "tag_name": "v99.0.0",
          "assets": [
            {
              "name": "V-Launcher-setup.exe",
              "browser_download_url": "https://example.test/V-Launcher-setup.exe"
            }
          ]
        }
        """;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, json));
        var service = new ApplicationUpdateService(httpClient, new TestLogger<ApplicationUpdateService>());

        // Act
        var result = await service.CheckForUpdatesAsync();

        // Assert
        Assert.False(result.IsUpdateAvailable);
        Assert.NotNull(result.LatestVersion);
        Assert.Equal(new Version(99, 0, 0), result.LatestVersion);
        Assert.Equal("https://example.test/V-Launcher-setup.exe", result.InstallerUrl);
        Assert.Null(result.InstallerSha256);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenReleaseUsesChecksumAsset_ShouldReturnChecksumUrl()
    {
        // Arrange
        var json = """
        {
          "tag_name": "v99.0.0",
          "assets": [
            {
              "name": "V-Launcher-setup.exe",
              "browser_download_url": "https://example.test/V-Launcher-setup.exe"
            },
            {
              "name": "V-Launcher-setup.exe.sha256",
              "browser_download_url": "https://example.test/V-Launcher-setup.exe.sha256"
            }
          ]
        }
        """;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, json));
        var service = new ApplicationUpdateService(httpClient, new TestLogger<ApplicationUpdateService>());

        // Act
        var result = await service.CheckForUpdatesAsync();

        // Assert
        Assert.True(result.IsUpdateAvailable);
        Assert.Null(result.InstallerSha256);
        Assert.Equal("https://example.test/V-Launcher-setup.exe.sha256", result.InstallerChecksumUrl);
    }

    [Fact]
    public async Task InstallUpdateAsync_WhenResultIsNotInstallable_ShouldReturnFalse()
    {
        // Arrange
        using var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, "{}"));
        var service = new ApplicationUpdateService(httpClient, new TestLogger<ApplicationUpdateService>());

        var checkResult = new UpdateCheckResult(
            IsUpdateAvailable: false,
            CurrentVersion: new Version(1, 0, 0, 0),
            LatestVersion: null,
            LatestTag: null,
            InstallerUrl: null,
            InstallerSha256: null,
            InstallerChecksumUrl: null);

        // Act
        var started = await service.InstallUpdateAsync(checkResult);

        // Assert
        Assert.False(started);
    }

    [Fact]
    public async Task InstallUpdateAsync_WhenChecksumDoesNotMatch_ShouldReturnFalse()
    {
        // Arrange
        const string installerUrl = "https://example.test/V-Launcher-setup.exe";
        using var httpClient = new HttpClient(new RoutedHttpMessageHandler(request =>
        {
            return request.RequestUri?.AbsoluteUri switch
            {
                installerUrl => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("installer-bits"))
                },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        }));

        var service = new ApplicationUpdateService(
            httpClient,
            new TestLogger<ApplicationUpdateService>(),
            _ => true,
            _ => new Process());

        var checkResult = new UpdateCheckResult(
            IsUpdateAvailable: true,
            CurrentVersion: new Version(1, 0, 0, 0),
            LatestVersion: new Version(2, 0, 0, 0),
            LatestTag: "v2.0.0",
            InstallerUrl: installerUrl,
            InstallerSha256: Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("different-bits"))),
            InstallerChecksumUrl: null);

        // Act
        var started = await service.InstallUpdateAsync(checkResult);

        // Assert
        Assert.False(started);
    }

    [Fact]
    public async Task InstallUpdateAsync_WhenSignatureVerificationFails_ShouldReturnFalse()
    {
        // Arrange
        const string installerUrl = "https://example.test/V-Launcher-setup.exe";
        var installerBytes = Encoding.UTF8.GetBytes("installer-bits");

        using var httpClient = new HttpClient(new RoutedHttpMessageHandler(request =>
        {
            return request.RequestUri?.AbsoluteUri switch
            {
                installerUrl => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(installerBytes)
                },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        }));

        var service = new ApplicationUpdateService(
            httpClient,
            new TestLogger<ApplicationUpdateService>(),
            _ => false,
            _ => new Process());

        var checkResult = new UpdateCheckResult(
            IsUpdateAvailable: true,
            CurrentVersion: new Version(1, 0, 0, 0),
            LatestVersion: new Version(2, 0, 0, 0),
            LatestTag: "v2.0.0",
            InstallerUrl: installerUrl,
            InstallerSha256: Convert.ToHexString(SHA256.HashData(installerBytes)),
            InstallerChecksumUrl: null);

        // Act
        var started = await service.InstallUpdateAsync(checkResult);

        // Assert
        Assert.False(started);
    }

    [Fact]
    public async Task InstallUpdateAsync_WhenChecksumAndSignatureAreValid_ShouldReturnTrue()
    {
        // Arrange
        const string installerUrl = "https://example.test/V-Launcher-setup.exe";
        var installerBytes = Encoding.UTF8.GetBytes("installer-bits");

        using var httpClient = new HttpClient(new RoutedHttpMessageHandler(request =>
        {
            return request.RequestUri?.AbsoluteUri switch
            {
                installerUrl => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(installerBytes)
                },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        }));

        var service = new ApplicationUpdateService(
            httpClient,
            new TestLogger<ApplicationUpdateService>(),
            _ => true,
            _ => new Process());

        var checkResult = new UpdateCheckResult(
            IsUpdateAvailable: true,
            CurrentVersion: new Version(1, 0, 0, 0),
            LatestVersion: new Version(2, 0, 0, 0),
            LatestTag: "v2.0.0",
            InstallerUrl: installerUrl,
            InstallerSha256: Convert.ToHexString(SHA256.HashData(installerBytes)),
            InstallerChecksumUrl: null);

        // Act
        var started = await service.InstallUpdateAsync(checkResult);

        // Assert
        Assert.True(started);
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }

    private sealed class RoutedHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
