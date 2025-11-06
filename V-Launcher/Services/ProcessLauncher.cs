using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using V_Launcher.Models;

namespace V_Launcher.Services;

/// <summary>
/// Service for launching processes with alternate AD credentials using Windows APIs
/// </summary>
public class ProcessLauncher : IProcessLauncher
{
    #region Windows API Declarations

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessWithLogonW(
        string lpUsername,
        string lpDomain,
        string lpPassword,
        LogonFlags dwLogonFlags,
        string? lpApplicationName,
        string? lpCommandLine,
        ProcessCreationFlags dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [Flags]
    private enum LogonFlags : uint
    {
        LOGON_WITH_PROFILE = 0x00000001,
        LOGON_NETCREDENTIALS_ONLY = 0x00000002
    }

    [Flags]
    private enum ProcessCreationFlags : uint
    {
        CREATE_DEFAULT_ERROR_MODE = 0x04000000,
        CREATE_NEW_CONSOLE = 0x00000010,
        CREATE_NEW_PROCESS_GROUP = 0x00000200,
        CREATE_NO_WINDOW = 0x08000000,
        CREATE_PROTECTED_PROCESS = 0x00040000,
        CREATE_PRESERVE_CODE_AUTHZ_LEVEL = 0x02000000,
        CREATE_SEPARATE_WOW_VDM = 0x00000800,
        CREATE_SHARED_WOW_VDM = 0x00001000,
        CREATE_SUSPENDED = 0x00000004,
        CREATE_UNICODE_ENVIRONMENT = 0x00000400,
        DEBUG_ONLY_THIS_PROCESS = 0x00000002,
        DEBUG_PROCESS = 0x00000001,
        DETACHED_PROCESS = 0x00000008,
        EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
        INHERIT_PARENT_AFFINITY = 0x00010000
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public uint cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public ushort wShowWindow;
        public ushort cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    #endregion

    /// <summary>
    /// Launches an executable with the specified AD account credentials
    /// </summary>
    public async Task<bool> LaunchAsync(ExecutableConfiguration config, ADAccount account, string password)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Validate inputs
                if (!ValidateConfiguration(config))
                {
                    throw new ArgumentException("Invalid executable configuration", nameof(config));
                }

                if (!ValidateCredentials(account, password))
                {
                    throw new ArgumentException("Invalid AD account credentials", nameof(account));
                }

                // Prepare startup info
                var startupInfo = new STARTUPINFO
                {
                    cb = (uint)Marshal.SizeOf<STARTUPINFO>(),
                    dwFlags = 0,
                    wShowWindow = 1 // SW_SHOWNORMAL
                };

                // Build command line
                var commandLine = BuildCommandLine(config);
                var workingDirectory = GetWorkingDirectory(config);

                // Launch process with alternate credentials
                var success = CreateProcessWithLogonW(
                    lpUsername: account.Username,
                    lpDomain: account.Domain,
                    lpPassword: password,
                    dwLogonFlags: LogonFlags.LOGON_WITH_PROFILE,
                    lpApplicationName: config.ExecutablePath,
                    lpCommandLine: commandLine,
                    dwCreationFlags: ProcessCreationFlags.CREATE_DEFAULT_ERROR_MODE,
                    lpEnvironment: IntPtr.Zero,
                    lpCurrentDirectory: workingDirectory,
                    lpStartupInfo: ref startupInfo,
                    lpProcessInformation: out var processInfo);

                if (!success)
                {
                    var error = Marshal.GetLastWin32Error();
                    var errorMessage = new Win32Exception(error).Message;
                    throw new InvalidOperationException($"Failed to launch process: {errorMessage} (Error code: {error})");
                }

                // Clean up handles
                CloseHandle(processInfo.hProcess);
                CloseHandle(processInfo.hThread);

                return true;
            }
            catch (Exception ex)
            {
                // Log the exception (in a real application, use proper logging)
                Debug.WriteLine($"Process launch failed: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// Validates that an executable configuration can be launched
    /// </summary>
    public bool ValidateConfiguration(ExecutableConfiguration config)
    {
        if (config == null)
            return false;

        // Check if executable path is provided and not empty
        if (string.IsNullOrWhiteSpace(config.ExecutablePath))
            return false;

        // Check if executable file exists
        if (!File.Exists(config.ExecutablePath))
            return false;

        // Validate working directory if specified
        if (!string.IsNullOrWhiteSpace(config.WorkingDirectory) && !Directory.Exists(config.WorkingDirectory))
            return false;

        return true;
    }

    /// <summary>
    /// Validates that an AD account has the required information for process launching
    /// </summary>
    public bool ValidateCredentials(ADAccount account, string password)
    {
        if (account == null)
            return false;

        // Check required fields
        if (string.IsNullOrWhiteSpace(account.Username))
            return false;

        if (string.IsNullOrWhiteSpace(account.Domain))
            return false;

        if (string.IsNullOrWhiteSpace(password))
            return false;

        return true;
    }

    /// <summary>
    /// Builds the command line string for process execution
    /// </summary>
    private static string? BuildCommandLine(ExecutableConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.Arguments))
            return null;

        // Ensure executable path is quoted if it contains spaces
        var executablePath = config.ExecutablePath.Contains(' ') && !config.ExecutablePath.StartsWith('"')
            ? $"\"{config.ExecutablePath}\""
            : config.ExecutablePath;

        return $"{executablePath} {config.Arguments}";
    }

    /// <summary>
    /// Gets the working directory for process execution
    /// </summary>
    private static string? GetWorkingDirectory(ExecutableConfiguration config)
    {
        // Use specified working directory if provided and valid
        if (!string.IsNullOrWhiteSpace(config.WorkingDirectory) && Directory.Exists(config.WorkingDirectory))
        {
            return config.WorkingDirectory;
        }

        // Fall back to executable directory
        var executableDirectory = Path.GetDirectoryName(config.ExecutablePath);
        return !string.IsNullOrWhiteSpace(executableDirectory) && Directory.Exists(executableDirectory)
            ? executableDirectory
            : null;
    }
}