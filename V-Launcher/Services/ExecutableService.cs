using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using V_Launcher.Models;

namespace V_Launcher.Services;

/// <summary>
/// Service for managing executable configurations and icon handling with caching
/// </summary>
public class ExecutableService : IExecutableService
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly ConcurrentDictionary<string, BitmapImage?> _iconCache = new();
    private readonly SemaphoreSlim _iconCacheLock = new(1, 1);

    // Windows API for icon extraction
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public ExecutableService(IConfigurationRepository configurationRepository)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
    }

    public async Task<IEnumerable<ExecutableConfiguration>> GetConfigurationsAsync()
    {
        return await _configurationRepository.LoadExecutableConfigurationsAsync();
    }

    public async Task<ExecutableConfiguration> SaveConfigurationAsync(ExecutableConfiguration config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        if (string.IsNullOrWhiteSpace(config.DisplayName))
            throw new ArgumentException("Display name is required", nameof(config));

        if (string.IsNullOrWhiteSpace(config.ExecutablePath))
            throw new ArgumentException("Executable path is required", nameof(config));

        if (!ValidateExecutablePath(config.ExecutablePath))
            throw new ArgumentException($"Executable path is not valid or accessible: {config.ExecutablePath}", nameof(config));

        // Validate custom icon path if provided
        if (!string.IsNullOrEmpty(config.CustomIconPath) && !File.Exists(config.CustomIconPath))
            throw new ArgumentException($"Custom icon path does not exist: {config.CustomIconPath}", nameof(config));

        var configurations = (await GetConfigurationsAsync()).ToList();
        
        var existingIndex = configurations.FindIndex(c => c.Id == config.Id);
        if (existingIndex >= 0)
        {
            configurations[existingIndex] = config;
        }
        else
        {
            configurations.Add(config);
        }

        await _configurationRepository.SaveExecutableConfigurationsAsync(configurations);
        
        // Clear cached icon for this configuration
        await ClearConfigurationIconFromCache(config);
        
        return config;
    }

    public async Task DeleteConfigurationAsync(Guid configId)
    {
        var configurations = (await GetConfigurationsAsync()).ToList();
        var configToRemove = configurations.FirstOrDefault(c => c.Id == configId);
        
        if (configToRemove != null)
        {
            configurations.Remove(configToRemove);
            await _configurationRepository.SaveExecutableConfigurationsAsync(configurations);
            
            // Clear cached icon for this configuration
            await ClearConfigurationIconFromCache(configToRemove);
        }
    }

    public async Task<BitmapImage?> GetIconAsync(ExecutableConfiguration config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        // Try custom icon first
        if (!string.IsNullOrEmpty(config.CustomIconPath))
        {
            var customIcon = await LoadCustomIconAsync(config.CustomIconPath);
            if (customIcon != null)
                return customIcon;
        }

        // Fall back to executable icon
        return await ExtractExecutableIconAsync(config.ExecutablePath);
    }

    public bool ValidateExecutablePath(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return false;

        try
        {
            // Check if file exists
            if (!File.Exists(executablePath))
                return false;

            // Check if it's an executable file
            var extension = Path.GetExtension(executablePath).ToLowerInvariant();
            var executableExtensions = new[] { ".exe", ".com", ".bat", ".cmd", ".msi" };
            
            if (!executableExtensions.Contains(extension))
                return false;

            // Try to access the file to ensure it's readable
            using var stream = File.OpenRead(executablePath);
            return stream.CanRead;
        }
        catch
        {
            return false;
        }
    }

    public async Task<BitmapImage?> ExtractExecutableIconAsync(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return null;

        // Check cache first
        var cacheKey = $"exe:{executablePath}";
        if (_iconCache.TryGetValue(cacheKey, out var cachedIcon))
            return cachedIcon;

        await _iconCacheLock.WaitAsync();
        try
        {
            // Double-check cache after acquiring lock
            if (_iconCache.TryGetValue(cacheKey, out cachedIcon))
                return cachedIcon;

            BitmapImage? icon = null;

            try
            {
                if (!File.Exists(executablePath))
                {
                    _iconCache[cacheKey] = null;
                    return null;
                }

                // Extract icon using Windows API
                var hIcon = ExtractIcon(IntPtr.Zero, executablePath, 0);
                
                if (hIcon != IntPtr.Zero && hIcon != new IntPtr(1)) // 1 indicates no icon
                {
                    try
                    {
                        // Convert to managed icon and then to BitmapImage
                        using var managedIcon = Icon.FromHandle(hIcon);
                        using var bitmap = managedIcon.ToBitmap();
                        
                        icon = ConvertBitmapToBitmapImage(bitmap);
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception in a real application
                System.Diagnostics.Debug.WriteLine($"Failed to extract icon from {executablePath}: {ex.Message}");
            }

            _iconCache[cacheKey] = icon;
            return icon;
        }
        finally
        {
            _iconCacheLock.Release();
        }
    }

    public async Task<BitmapImage?> LoadCustomIconAsync(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
            return null;

        // Check cache first
        var cacheKey = $"custom:{iconPath}";
        if (_iconCache.TryGetValue(cacheKey, out var cachedIcon))
            return cachedIcon;

        await _iconCacheLock.WaitAsync();
        try
        {
            // Double-check cache after acquiring lock
            if (_iconCache.TryGetValue(cacheKey, out cachedIcon))
                return cachedIcon;

            BitmapImage? icon = null;

            try
            {
                if (!File.Exists(iconPath))
                {
                    _iconCache[cacheKey] = null;
                    return null;
                }

                // Load image file
                icon = new BitmapImage();
                icon.BeginInit();
                icon.CacheOption = BitmapCacheOption.OnLoad;
                icon.UriSource = new Uri(iconPath, UriKind.Absolute);
                icon.EndInit();
                icon.Freeze(); // Make it thread-safe
            }
            catch (Exception ex)
            {
                // Log the exception in a real application
                System.Diagnostics.Debug.WriteLine($"Failed to load custom icon from {iconPath}: {ex.Message}");
                icon = null;
            }

            _iconCache[cacheKey] = icon;
            return icon;
        }
        finally
        {
            _iconCacheLock.Release();
        }
    }

    public void ClearIconCache()
    {
        _iconCache.Clear();
    }

    private async Task ClearConfigurationIconFromCache(ExecutableConfiguration config)
    {
        await _iconCacheLock.WaitAsync();
        try
        {
            // Remove executable icon from cache
            var executableCacheKey = $"exe:{config.ExecutablePath}";
            _iconCache.TryRemove(executableCacheKey, out _);

            // Remove custom icon from cache if it exists
            if (!string.IsNullOrEmpty(config.CustomIconPath))
            {
                var customCacheKey = $"custom:{config.CustomIconPath}";
                _iconCache.TryRemove(customCacheKey, out _);
            }
        }
        finally
        {
            _iconCacheLock.Release();
        }
    }

    private static BitmapImage? ConvertBitmapToBitmapImage(Bitmap bitmap)
    {
        try
        {
            using var memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = memory;
            bitmapImage.EndInit();
            bitmapImage.Freeze(); // Make it thread-safe

            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }
}