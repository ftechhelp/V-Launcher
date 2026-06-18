# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

V-Launcher is a Windows-only WPF desktop app (.NET 9, `net9.0-windows`) that launches executables under alternate Active Directory credentials without re-typing passwords. Credentials, the TOTP secret, and a config integrity key are all protected with Windows DPAPI (`CurrentUser` scope), so everything is tied to the current Windows user on the current machine. This makes most of the security-critical code untestable/non-portable off Windows — DPAPI calls will throw elsewhere.

## Commands

```bash
dotnet restore
dotnet build                         # whole solution (V-Launcher.sln)
dotnet build -c Release
dotnet test                          # all xUnit tests in V-LauncherTests
dotnet run --project V-Launcher      # run the app (requires Windows + a desktop session)

# Run a single test / filtered set (xUnit + dotnet test)
dotnet test --filter "FullyQualifiedName~ConfigurationRepositoryTests"
dotnet test --filter "DisplayName~Recovers"
```

Publishing (also produced by CI on tags):
```bash
dotnet publish V-Launcher/V-Launcher.csproj -c Release -r win-x64 --self-contained true  -o publish/win-x64
dotnet publish V-Launcher/V-Launcher.csproj -c Release -r win-x64 --self-contained false -o publish/win-x64-fd
```

CLI flag: `V-Launcher.exe --reset-otp` clears the stored OTP secret (handled in `App.OnStartup` before any window shows) and exits.

## Architecture

Standard MVVM with constructor dependency injection via `Microsoft.Extensions.Hosting`. All wiring lives in **`App.xaml.cs` `CreateHostBuilder()`** — services are singletons, ViewModels are transient. `App.OnStartup` is the real entry point and enforces the security gate *before* the main window exists (see Startup gate below).

Layering (dependencies point downward; never invert):

- **Views** (`Views/*.xaml` + code-behind, `MainWindow.xaml`) — XAML bound to ViewModels. Code-behind is thin (window/tray plumbing). Theme in `Views/RedWhiteTheme.xaml`; converters in `Views/`.
- **ViewModels** (`ViewModels/`) — derive from `ViewModelBase`; use CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `RelayCommand`/`AsyncRelayCommand`). `MainViewModel` is the hub: it constructs the child VMs (Launcher, CredentialManagement, ExecutableManagement, AdHocLauncher, NetworkDriveManagement, Settings), owns navigation (`CurrentViewModel`), and bubbles each child's status/error up via `PropertyChanged` subscriptions. Cross-VM data refresh after edits flows through `MainViewModel.RefreshDataAfterChangesAsync`, which child VMs receive as a callback.
- **Services** (`Services/`) — all behind interfaces (`I*`) for DI and testing. Business logic, P/Invoke, crypto, persistence.
- **Models** (`Models/`) — plain serializable data. `ApplicationConfiguration` is the single root object that gets persisted.

### Persistence — `ConfigurationRepository`

There is **one JSON file** that holds everything (accounts, executables, network drives, settings, OTP secret): `ApplicationConfiguration`. All the per-entity `Load*/Save*` methods are read-modify-write over this single document, guarded by a `SemaphoreSlim`. Key behaviors to preserve when touching this file:

- **Signed envelope**: saved JSON is wrapped in a `ConfigurationEnvelope` with an `HMACSHA256` signature (format version `2.0`). The HMAC key is random 32 bytes, DPAPI-protected, stored beside the config as `configuration.integrity.key`. On load, the signature is verified (`FixedTimeEquals`) before the config is trusted. Legacy unsigned JSON is still readable.
- **Atomic writes**: write to `*.tmp` then `File.Replace`/`Move`.
- **Backups + recovery**: every save also writes backups to `%APPDATA%\V-Launcher\` and `%LOCALAPPDATA%\V-Launcher\`. On load, a missing/empty/corrupt/failed-integrity primary triggers recovery from a backup, then rewrites the primary. If two signed copies agree on normalized payload, it can self-repair.
- **Two constructors**: the default uses `%APPDATA%` paths; the `string configurationFilePath` constructor is the **test seam** (custom path, `.bak` backups, `.integrity.key` sibling). Tests use the latter to avoid touching real user data.

### Launching under alternate credentials — `ProcessLauncher`

P/Invokes `CreateProcessWithLogonW` (advapi32) with `LOGON_WITH_PROFILE`. `GetExecutableAndCommandLine` special-cases file types that can't be exec'd directly: `.msc`→`mmc.exe`, `.vbs`/`.wsf`→`wscript.exe`, `.ps1`→`powershell.exe -File`; everything else launches directly. Passwords arrive decrypted from `CredentialService.DecryptPasswordAsync` and should be short-lived. `NetworkDriveService` similarly P/Invokes `WNetAddConnection2`/`WNetCancelConnection2` (mpr.dll) to map drives.

### Authentication gate — TOTP

`TotpService` (uses `Otp.NET`, `QRCoder` for setup QR) generates/validates 6-digit 30-second TOTP; the secret is DPAPI-encrypted inside the config. The gate is enforced entirely in **`App.xaml.cs`**, not in a ViewModel:

1. On startup: load config → if no OTP configured, force `OtpSetupWindow` (first run); then always require `OtpVerificationWindow` before the main window shows.
2. While running: `SystemEvents.SessionSwitch` — on **lock**, hide to tray; on **unlock**, require OTP again (`OtpVerificationWindow`) before restoring. Failure/cancel at any gate shuts the app down.

When changing startup/auth/window-restore behavior, that logic lives in `App.xaml.cs` (`OnStartup`, `OnSessionSwitch`, `RequireOtpAfterUnlockAsync`), not in `MainViewModel`.

### Updates — `ApplicationUpdateService`

Checks **GitHub releases** for `ftechhelp/V-Launcher` (configurable via `VLAUNCHER_GITHUB_*` env vars). Before launching a downloaded installer it verifies **both** the published SHA-256 and the Authenticode signature (`WinVerifyTrust`, optional signer subject/thumbprint allowlists). The constructor takes optional `installerSignatureVerifier`/`processStarter` delegates purely as **test seams** — tests inject fakes to avoid real downloads/signature checks. Triggered manually ("Check Updates") and on startup via `MainViewModel.CheckForUpdatesAsync`.

## Testing notes

- xUnit. Tests mirror the source tree (`Services/`, `ViewModels/`, `Models/`, `Integration/`).
- Prefer the path-injecting `ConfigurationRepository(string)` ctor and the update-service delegate seams over hitting real files/network/crypto. `FakeApplicationUpdateService` is the existing pattern for a hand-rolled fake.
- DPAPI-dependent tests (`CredentialServiceTests`, `TotpServiceTests`, config integrity) only pass on Windows under the same user that encrypted the data.

## Gotchas

- **Windows-only**: WPF + WinForms (`UseWindowsForms` is on for the system tray / icon extraction) + P/Invoke + DPAPI. Don't expect builds or tests to work on Linux/macOS.
- **CI vs. runtime update channel diverge**: `.gitlab-ci.yml` builds/tests/releases on **GitLab** (tag-triggered, Windows runner), but the in-app updater pulls from **GitHub**. The README still references GitLab in a few update-related spots; the code is the source of truth (GitHub). Keep this in mind before "fixing" an apparent mismatch.
- **`.kiro/specs/ad-user-launcher/`** holds the original requirements/design/tasks spec docs — useful for intent/rationale, but the code has moved beyond them (OTP, network drives, GitHub updates were added later).
- `SECURITY.md` documents the threat model and DPAPI/OTP/integrity design in depth — read it before changing anything security-related.
