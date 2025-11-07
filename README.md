# V-Launcher - Active Directory User Application Launcher

V-Launcher is a secure WPF application that enables users to launch applications using different Active Directory (AD) user credentials without repeatedly entering passwords. The application provides encrypted credential storage and streamlined application launching with alternate AD accounts.

## Features

### 🔐 Secure Credential Management
- **Encrypted Storage**: Passwords are encrypted using Windows Data Protection API (DPAPI)
- **User-Scoped Security**: Credentials can only be accessed by the current Windows user
- **Multiple Account Support**: Store and manage multiple AD accounts
- **Secure Memory Handling**: Passwords are cleared from memory after use

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

2. **Add AD Accounts**
   - Click "Manage Accounts" in the navigation bar
   - Click "Add Account" button
   - Fill in the account details:
     - **Display Name**: Friendly name for the account (e.g., "John Doe - Admin")
     - **Username**: AD username (without domain)
     - **Domain**: AD domain name
     - **Password**: AD account password
   - Click "Save" to store the account securely

3. **Configure Applications**
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

1. **Launch Applications**
   - Click "Launcher" in the navigation bar
   - Click on any configured application icon
   - The application will launch automatically with the configured AD credentials
   - Status messages will appear at the bottom of the window
   - If minimized to tray, double-click the system tray icon to restore the window

2. **Manage Configurations**
   - Use "Manage Accounts" to add, edit, or remove AD accounts
   - Use "Manage Apps" to add, edit, or remove application configurations
   - Changes are saved automatically

3. **Configure Application Settings**
   - Locate the settings checkboxes in the main window (near navigation buttons)
   - **Start on Windows Start**: Enable to launch V-Launcher automatically when Windows starts
   - **Start Minimized**: Enable to start the application minimized to system tray
   - **Minimize on Close**: Enable to minimize to system tray instead of closing when clicking X
   - All settings are saved automatically when changed

4. **System Tray Usage**
   - When minimized to tray, V-Launcher appears in the notification area
   - **Double-click** the tray icon to restore the main window
   - **Right-click** the tray icon for quick actions:
     - Show/Hide main window
     - Exit application

### Configuration File Locations

V-Launcher stores its configuration files in the user's AppData folder:

```
%APPDATA%\V-Launcher\
├── config.json          # Main configuration file (includes settings)
└── logs\                 # Application logs (if logging is enabled)
```

The configuration file includes:
- Encrypted AD account credentials
- Executable configurations
- Application settings (startup behavior, minimize options)

## Security Information

### Password Encryption
- **DPAPI Encryption**: All passwords are encrypted using Windows Data Protection API
- **User-Scoped**: Encrypted data can only be decrypted by the same user on the same machine
- **No Plain Text**: Passwords are never stored in plain text
- **Memory Security**: Passwords are cleared from memory immediately after use

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
3. **File Corruption**: Delete `config.json` to reset (will lose all configurations)
4. **Antivirus**: Check if antivirus is blocking file access

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

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please read the contributing guidelines and submit pull requests for any improvements.

---

**Version**: 1.0.0  
**Last Updated**: November 2024  
**Minimum Windows Version**: Windows 10 (1809)  
**Framework**: .NET 9.0