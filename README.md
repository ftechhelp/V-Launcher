# V-Launcher - Active Directory User Application Launcher

V-Launcher is a secure WPF application that enables users to launch applications using different Active Directory (AD) user credentials without repeatedly entering passwords. The application provides encrypted credential storage and streamlined application launching with alternate AD accounts.

## Features

### 🔐 Secure Credential Management
- **Encrypted Storage**: Passwords are encrypted using Windows Data Protection API (DPAPI)
- **User-Scoped Security**: Credentials can only be accessed by the current Windows user
- **Multiple Account Support**: Store and manage multiple AD accounts
- **Secure Memory Handling**: Passwords are cleared from memory after use

### 🔑 OTP Two-Factor Authentication
- **Mandatory at Launch**: A valid 6-digit TOTP code is required each time the app starts
- **Required After PC Lock**: Locking and unlocking Windows requires OTP verification again before access is restored
- **First-Run Setup**: If OTP is not configured, setup is required before accessing the app
- **Authenticator Support**: Works with Microsoft Authenticator and other standard TOTP apps
- **Encrypted OTP Secret**: The OTP seed is protected with Windows DPAPI and stored encrypted

### 🚀 Application Launching
- **One-Click Launch**: Launch configured applications with a single click
- **Custom Icons**: Support for custom application icons or automatic icon extraction
- **Command Line Arguments**: Configure applications with specific command-line parameters
- **Working Directory**: Set custom working directories for applications

### 💾 Data Persistence
- **Automatic Saving**: All configurations are automatically saved
- **JSON Storage**: Human-readable configuration files stored in user's AppData folder
- **Data Integrity**: Robust error handling and data validation

### 🎨 User Interface
- **Modern WPF Design**: Clean, intuitive interface built with WPF
- **MVVM Architecture**: Maintainable code structure with proper separation of concerns
- **Real-time Feedback**: Status updates and error messages for all operations
- **Responsive Layout**: Adaptive interface that works on different screen sizes
- **Red & White Theme**: Professional color scheme matching the application logo

### ⚙️ Application Settings
- **Start with Windows**: Automatically launch V-Launcher when Windows starts
- **Start Minimized**: Begin in system tray for unobtrusive startup
- **Minimize to Tray**: Minimize to system tray instead of taskbar
- **System Tray Integration**: Quick access from notification area with context menu

### 🔄 Application Updates
- **GitLab Release Checks**: Checks GitLab for the latest tagged release
- **Manual Update Trigger**: "Check Updates" button in the main window
- **Startup Update Check**: Automatically checks for updates during app startup
- **Installer Launch**: Downloads and starts update installer when a newer version is found

## System Requirements

### Operating System
- **Windows 10** (version 1809 or later) or **Windows 11**
- **Windows Server 2019** or later (for server environments)

### Framework Requirements
- **.NET 9.0 Runtime** (Windows Desktop Runtime)
- **Windows Forms** and **WPF** support

### Hardware Requirements
- **RAM**: Minimum 512 MB available memory
- **Storage**: 50 MB free disk space
- **Display**: 1024x768 minimum resolution

### Network Requirements
- **Active Directory**: Access to domain controller for credential validation
- **Network Connectivity**: Required for AD authentication during application launch

## Installation

### Prerequisites
1. **Install .NET 9.0 Desktop Runtime**
   - Download from: https://dotnet.microsoft.com/download/dotnet/9.0
   - Select "Desktop Runtime" for your system architecture (x64/x86)

### Installation Steps

#### Option 1: Download Release (Recommended)
1. Go to the [Releases](../../releases) page
2. Download the latest `V-Launcher-vX.X.X.zip` file
3. Extract the ZIP file to your desired installation directory
4. Run `V-Launcher.exe`

#### Option 2: Build from Source
1. **Clone the repository**:
   ```bash
   git clone <repository-url>
   cd V-Launcher
   ```

2. **Build the application**:
   ```bash
   dotnet build --configuration Release
   ```

3. **Run the application**:
   ```bash
   dotnet run --project V-Launcher
   ```

#### Option 3: Publish Self-Contained
1. **Publish for Windows x64**:
   ```bash
   dotnet publish V-Launcher -c Release -r win-x64 --self-contained true
   ```

2. **Navigate to publish directory**:
   ```bash
   cd V-Launcher\bin\Release\net9.0-windows\win-x64\publish
   ```

3. **Run the executable**:
   ```bash
   V-Launcher.exe
   ```

## Usage Guide

### First Time Setup

1. **Launch V-Launcher**
   - Run `V-Launcher.exe`
   - The application will create necessary configuration directories automatically

2. **Complete OTP Setup (required on first launch)**
   - Scan the QR code with Microsoft Authenticator (or another TOTP app)
   - Or manually enter the provided secret key
   - Enter a valid 6-digit code to confirm setup
   - OTP setup must complete before the main window is available

3. **Add AD Accounts**
   - Click "Manage Accounts" in the navigation bar
   - Click "Add Account" button
   - Fill in the account details:
     - **Display Name**: Friendly name for the account (e.g., "John Doe - Admin")
     - **Username**: AD username (without domain)
     - **Domain**: AD domain name
     - **Password**: AD account password
   - Click "Save" to store the account securely

4. **Configure Applications**
   - Click "Manage Apps" in the navigation bar
   - Click "Add Application" button
   - Configure the application:
     - **Display Name**: Name shown in launcher (e.g., "SQL Server Management Studio")
     - **Executable Path**: Browse to select the .exe file
     - **AD Account**: Select from your saved accounts
     - **Custom Icon** (optional): Browse to select a custom icon file
     - **Arguments** (optional): Command-line arguments
     - **Working Directory** (optional): Starting directory for the application
   - Click "Save" to store the configuration

### Daily Usage

1. **Authenticate with OTP at startup**
   - Enter the 6-digit code from your authenticator app
   - Up to 5 attempts are allowed before startup is blocked

2. **Re-authenticate after locking Windows**
   - If your workstation is locked while V-Launcher is running, the app hides to the system tray
   - After you unlock Windows, V-Launcher requires another valid 6-digit OTP code before access is restored
   - If OTP verification is cancelled or fails after unlock, the application closes

3. **Launch Applications**
   - Click "Launcher" in the navigation bar
   - Click on any configured application icon
   - The application will launch automatically with the configured AD credentials
   - Status messages will appear at the bottom of the window
   - If minimized to tray, double-click the system tray icon to restore the window

4. **Manage Configurations**
   - Use "Manage Accounts" to add, edit, or remove AD accounts
   - Use "Manage Apps" to add, edit, or remove application configurations
   - Changes are saved automatically

5. **Configure Application Settings**
   - Locate the settings checkboxes in the main window (near navigation buttons)
   - **Start on Windows Start**: Enable to launch V-Launcher automatically when Windows starts
   - **Start Minimized**: Enable to start the application minimized to system tray
   - **Minimize on Close**: Enable to minimize to system tray instead of closing when clicking X
   - **Check Updates**: Click to manually check GitLab for newer versions and launch installer if available
   - All settings are saved automatically when changed

6. **System Tray Usage**
   - When minimized to tray, V-Launcher appears in the notification area
   - **Double-click** the tray icon to restore the main window
   - **Right-click** the tray icon for quick actions:
     - Show/Hide main window
     - Exit application

### Configuration File Locations

V-Launcher stores its configuration files in the user's AppData folder:

```
%APPDATA%\V-Launcher\
├── configuration.json          # Main configuration file (includes settings)
├── configuration.backup.json   # Backup copy for recovery
└── logs\                       # Application logs (if logging is enabled)

%LOCALAPPDATA%\V-Launcher\
└── configuration.backup.json   # Additional backup copy for recovery
```

The configuration file includes:
- Encrypted AD account credentials
- Encrypted OTP secret (when OTP is enabled)
- Executable configurations
- Application settings (startup behavior, minimize options)

### Configuration Resilience During Updates

To help protect data during upgrades or installer changes, V-Launcher now:

- Writes configuration atomically (temp file + replace)
- Maintains backup copies of configuration in both `%APPDATA%` and `%LOCALAPPDATA%`
- Automatically recovers from backup if the primary configuration is missing, empty, or corrupted
- Restores the primary `configuration.json` after successful recovery

This means configuration is significantly more resilient if an update process affects the primary config file.

### Update Configuration (GitLab)

V-Launcher supports optional environment variables for update checks:

- `VLAUNCHER_GITLAB_HOST` (default: `https://gitlab.abbotsford.ca`)
- `VLAUNCHER_GITLAB_PROJECT` (default: `vfontaine/v-launcher`)
- `VLAUNCHER_GITLAB_TOKEN` (optional, for private projects)

## Security Information

### Password Encryption
- **DPAPI Encryption**: All passwords are encrypted using Windows Data Protection API
- **User-Scoped**: Encrypted data can only be decrypted by the same user on the same machine
- **No Plain Text**: Passwords are never stored in plain text
- **Memory Security**: Passwords are cleared from memory immediately after use

### OTP Security
- **TOTP Standard**: Uses RFC-compatible 6-digit, time-based one-time passwords (30-second step)
- **Encrypted Secret Storage**: OTP secret is stored encrypted with DPAPI (`CurrentUser` scope)
- **Launch Gate**: Access to the main app requires successful OTP verification
- **Unlock Gate**: Locking and unlocking the workstation requires successful OTP verification again before the window is restored
- **Limited Attempts**: Verification allows a limited number of attempts per startup session

### Data Protection
- **Local Storage Only**: All data is stored locally on the user's machine
- **No Network Transmission**: Credentials are never transmitted over the network except during authentication
- **File Permissions**: Configuration files inherit Windows user permissions

### Security Best Practices
- **Regular Password Updates**: Update stored passwords when AD passwords change
- **Secure Workstation**: Ensure your workstation is properly secured and locked when unattended
- **Account Management**: Regularly review and remove unused AD accounts from the application
- **Antivirus**: Keep your antivirus software up to date

## Troubleshooting

### Common Issues

#### Application Won't Start
**Problem**: V-Launcher.exe doesn't start or crashes immediately

**Solutions**:
1. **Check .NET Runtime**: Ensure .NET 9.0 Desktop Runtime is installed
2. **Run as Administrator**: Try running the application as administrator
3. **Check Event Logs**: Look in Windows Event Viewer for error details
4. **Antivirus**: Temporarily disable antivirus to check for interference

#### OTP Verification Issues
**Problem**: App closes or denies access after entering OTP code

**Solutions**:
1. **Check Device Time**: Ensure Windows and phone time are set automatically and in sync
2. **Use Current Code Quickly**: Enter the latest 6-digit code before it rotates
3. **Verify Correct Account**: Confirm you are reading the code for the V-Launcher entry in your authenticator
4. **Retry Startup**: Close and reopen app after failed attempts
5. **Reconfigure OTP**: If authenticator was reset/reinstalled, complete setup again

#### Authentication Failures
**Problem**: "Authentication failed" or "Access denied" when launching applications

**Solutions**:
1. **Verify Credentials**: Ensure AD username, domain, and password are correct
2. **Test Manually**: Try logging into the domain with the same credentials manually
3. **Domain Connectivity**: Verify network connection to domain controller
4. **Account Status**: Check if the AD account is locked or disabled
5. **Password Expiry**: Verify the AD password hasn't expired

#### Application Launch Failures
**Problem**: Configured applications don't start or show errors

**Solutions**:
1. **File Path**: Verify the executable path is correct and file exists
2. **Permissions**: Ensure the AD account has permission to run the application
3. **Dependencies**: Check if the application has required dependencies installed
4. **Working Directory**: Verify the working directory exists and is accessible
5. **Arguments**: Check command-line arguments for syntax errors

#### Configuration Issues
**Problem**: Settings not saving or loading incorrectly

**Solutions**:
1. **File Permissions**: Check write permissions to `%APPDATA%\V-Launcher\`
2. **Disk Space**: Ensure sufficient disk space is available
3. **Backup Recovery**: If `configuration.json` is missing/corrupt, restart app to trigger automatic recovery from backup
4. **Reset (Last Resort)**: Delete `configuration.json` and backup files only if you intentionally want a clean reset
5. **Antivirus**: Check if antivirus is blocking file access

#### Update Check Issues
**Problem**: "Check Updates" does not find updates or fails

**Solutions**:
1. **Network Access**: Ensure machine can access your GitLab host
2. **Project/Tag Format**: Confirm releases use semantic version tags (e.g., `v1.2.3`)
3. **Private GitLab**: Set `VLAUNCHER_GITLAB_TOKEN` when release API requires auth
4. **Installer Asset**: Ensure release contains a downloadable `.exe` or `.msi` asset link

#### Windows Startup Issues
**Problem**: Application doesn't start with Windows despite setting enabled

**Solutions**:
1. **Registry Permissions**: Ensure user has permission to modify startup registry keys
2. **Check Registry**: Verify entry exists in `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`
3. **Executable Path**: Ensure the application hasn't been moved after enabling startup
4. **Task Manager**: Check Startup tab in Task Manager to verify the entry
5. **UAC Settings**: Verify User Account Control isn't blocking the startup entry

#### System Tray Issues
**Problem**: Application doesn't appear in system tray or tray icon not working

**Solutions**:
1. **Notification Area Settings**: Check Windows notification area settings
2. **Hidden Icons**: Click the up arrow in system tray to show hidden icons
3. **Windows Explorer**: Restart Windows Explorer process if tray icons are not displaying
4. **Restore Window**: Use Alt+Tab to find and restore the window if tray icon is unresponsive
5. **Restart Application**: Close and restart V-Launcher to reinitialize system tray integration

#### Minimize to Tray Not Working
**Problem**: Clicking X closes the application instead of minimizing to tray

**Solutions**:
1. **Check Setting**: Verify "Minimize on Close" checkbox is enabled in settings
2. **Force Close**: Use right-click on tray icon and select "Exit" to fully close the application
3. **Settings Persistence**: Ensure settings are being saved (check config.json file)
4. **Restart Required**: Restart the application after changing the minimize setting

### Error Messages

#### "Failed to encrypt password using DPAPI"
- **Cause**: Windows DPAPI service issue
- **Solution**: Restart Windows or contact system administrator

#### "Executable file not found"
- **Cause**: Application executable has been moved or deleted
- **Solution**: Update the executable path in application configuration

#### "Invalid AD credentials during launch"
- **Cause**: Stored credentials are incorrect or expired
- **Solution**: Update the AD account password in credential management

#### "Access denied"
- **Cause**: Insufficient permissions for the AD account
- **Solution**: Verify account permissions or contact domain administrator

#### "Failed to set Windows startup registry entry"
- **Cause**: Insufficient permissions to modify registry startup keys
- **Solution**: Run application as administrator or contact system administrator

#### "System tray icon initialization failed"
- **Cause**: Windows notification area service issue
- **Solution**: Restart Windows Explorer or reboot the system

### Getting Help

If you continue to experience issues:

1. **Check Logs**: Look for detailed error information in the application logs
2. **Event Viewer**: Check Windows Event Viewer for system-level errors
3. **Documentation**: Review this README and the application's built-in help
4. **Support**: Contact your system administrator or IT support team

### Reporting Issues

When reporting issues, please include:
- **Windows Version**: Your Windows version and build number
- **Application Version**: V-Launcher version number
- **Error Messages**: Exact error messages or screenshots
- **Steps to Reproduce**: Detailed steps that led to the issue
- **System Information**: Any relevant system configuration details

## Development

### Building from Source

#### Prerequisites
- **Visual Studio 2022** (17.8 or later) with .NET 9.0 SDK
- **Git** for version control

#### Development Setup
1. **Clone Repository**:
   ```bash
   git clone <repository-url>
   cd V-Launcher
   ```

2. **Restore Dependencies**:
   ```bash
   dotnet restore
   ```

3. **Build Solution**:
   ```bash
   dotnet build
   ```

4. **Run Tests**:
   ```bash
   dotnet test
   ```

#### Project Structure
```
V-Launcher/
├── V-Launcher/              # Main WPF application
│   ├── Models/              # Data models (ADAccount, ExecutableConfiguration)
│   ├── Services/            # Business logic services
│   ├── ViewModels/          # MVVM ViewModels
│   ├── Views/               # WPF Views and UserControls
│   ├── Helpers/             # Utility classes
│   └── Validation/          # Input validation logic
├── V-LauncherTests/         # Unit and integration tests
└── .kiro/                   # Kiro IDE specifications and documentation
```

#### Key Dependencies
- **CommunityToolkit.Mvvm**: MVVM framework and base classes
- **Microsoft.Extensions.Hosting**: Dependency injection and hosting
- **System.Text.Json**: JSON serialization for configuration
- **System.Drawing.Common**: Icon extraction and image handling

### Architecture Overview

V-Launcher follows the MVVM (Model-View-ViewModel) architectural pattern:

- **Models**: Data structures for AD accounts and executable configurations
- **Views**: WPF user interface components with XAML
- **ViewModels**: Business logic and data binding for views
- **Services**: Core functionality (encryption, process launching, data persistence)

The application uses dependency injection for service management and follows SOLID principles for maintainable, testable code.

### GitLab CI/CD Pipeline

The project includes automated CI/CD pipeline configuration (`.gitlab-ci.yml`) that handles building, testing, and releasing.

#### Pipeline Stages

1. **Build**: Compiles the solution on every commit
2. **Test**: Runs all unit and integration tests
3. **Release**: Creates release artifacts (only triggered by tags)

#### Creating a Release

To create a new release, push a version tag:

```bash
# Create a semantic version tag
git tag -a v1.0.0 -m "Release version 1.0.0"

# Push the tag to GitLab
git push origin v1.0.0
```

The pipeline will automatically:
- Build and test the application
- Create two release variants:
  - **Self-contained** (~150MB): Includes .NET runtime, no installation required
  - **Framework-dependent** (~5MB): Requires .NET 9.0 Desktop Runtime
- Generate ZIP archives
- Create a GitLab Release with downloadable artifacts

#### Release Variants

**Self-Contained Build** (`V-Launcher-vX.X.X-win-x64.zip`):
- Includes .NET 9.0 runtime
- Works on any Windows 10+ machine
- Recommended for end users

**Framework-Dependent Build** (`V-Launcher-vX.X.X-win-x64-framework-dependent.zip`):
- Requires .NET 9.0 Desktop Runtime installed
- Smaller download size
- Recommended when .NET is already installed

#### Runner Requirements

The GitLab Runner must have:
- Windows 10 or Windows Server 2019+
- .NET 9.0 SDK installed
- PowerShell 5.1 or later
- Tagged with `windows`

#### Manual Build for Release

If you need to build manually:

```bash
# Self-contained build
dotnet publish V-Launcher/V-Launcher.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64

# Framework-dependent build
dotnet publish V-Launcher/V-Launcher.csproj -c Release -r win-x64 --self-contained false -o publish/win-x64-fd

# Create ZIP archive (PowerShell)
Compress-Archive -Path publish/win-x64/* -DestinationPath V-Launcher-v1.0.0-win-x64.zip
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please read the contributing guidelines and submit pull requests for any improvements.

---

**Version**: 1.0.0  
**Last Updated**: November 2024  
**Minimum Windows Version**: Windows 10 (1809)  
**Framework**: .NET 9.0