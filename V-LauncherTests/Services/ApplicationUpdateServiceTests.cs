using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using V_Launcher.Services;

namespace V_LauncherTests.Services;

public class ApplicationUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_WhenNewerReleaseHasInstaller_ShouldReportUpdateAvailable()
    {
        // Arrange
        var json = """
        {
          "tag_name": "v99.0.0",
          "assets": {
            "links": [
              { "url": "https://example.test/V-Launcher-setup.exe", "direct_asset_url": "https://example.test/V-Launcher-setup.exe" }
            ]
          }
        }
        """;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, json));
        var service = new ApplicationUpdateService(httpClient, new TestLogger<ApplicationUpdateService>());

        // Act
        var result = await service.CheckForUpdatesAsync();

        // Assert
        Assert.True(result.IsUpdateAvailable);
        Assert.NotNull(result.LatestVersion);
        Assert.Equal(new Version(99, 0, 0), result.LatestVersion);
        Assert.Equal("https://example.test/V-Launcher-setup.exe", result.InstallerUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenReleaseHasNoInstaller_ShouldReportNoUpdate()
    {
        // Arrange
        var json = """
        {
          "tag_name": "v99.0.0",
          "assets": {
            "links": [
              { "url": "https://example.test/release-notes.txt" }
            ]
          }
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
        Assert.True(string.IsNullOrWhiteSpace(result.InstallerUrl));
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
            InstallerUrl: null);

        // Act
        var started = await service.InstallUpdateAsync(checkResult);

        // Assert
        Assert.False(started);
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

    private sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
