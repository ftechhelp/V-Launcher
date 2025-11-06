using System.ComponentModel.DataAnnotations;
using System.IO;

namespace V_Launcher.Models;

/// <summary>
/// Represents a configuration for launching an executable with specific AD credentials
/// </summary>
public class ExecutableConfiguration
{
    /// <summary>
    /// Unique identifier for the executable configuration
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the executable shown in the UI
    /// </summary>
    [Required]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the executable file
    /// </summary>
    [Required]
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Optional path to a custom icon file
    /// </summary>
    public string? CustomIconPath { get; set; }

    /// <summary>
    /// ID of the associated AD account to use for launching
    /// </summary>
    [Required]
    public Guid ADAccountId { get; set; }

    /// <summary>
    /// Optional command-line arguments to pass to the executable
    /// </summary>
    public string? Arguments { get; set; }

    /// <summary>
    /// Optional working directory for the executable
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Gets the filename from the executable path
    /// </summary>
    public string ExecutableFileName => Path.GetFileName(ExecutablePath);

    /// <summary>
    /// Gets the directory containing the executable
    /// </summary>
    public string ExecutableDirectory => Path.GetDirectoryName(ExecutablePath) ?? string.Empty;

    /// <summary>
    /// Determines if the executable file exists
    /// </summary>
    public bool ExecutableExists => File.Exists(ExecutablePath);

    /// <summary>
    /// Determines if the custom icon file exists (if specified)
    /// </summary>
    public bool CustomIconExists => string.IsNullOrEmpty(CustomIconPath) || File.Exists(CustomIconPath);

    public override string ToString()
    {
        return DisplayName;
    }

    public override bool Equals(object? obj)
    {
        return obj is ExecutableConfiguration config && Id.Equals(config.Id);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }
}