# Security Documentation

## Overview

The AD User Launcher implements multiple layers of security to protect stored credentials and ensure secure process launching. This document details the security mechanisms, implementation specifics, and important limitations users should understand.

## Password Storage Security

### DPAPI Encryption Implementation

The application uses Windows Data Protection API (DPAPI) for credential encryption, providing enterprise-grade security for stored passwords.

#### How DPAPI Works
- **Encryption Scope**: CurrentUser - credentials can only be decrypted by the same Windows user account
- **Machine Binding**: Encrypted data is tied to the specific machine where it was created
- **Key Management**: Windows automatically manages encryption keys using the user's login credentials
- **Algorithm**: Uses AES-256 encryption with machine and user-specific entropy

#### Implementation Details
```csharp
// Encryption process
byte[] encryptedData = ProtectedData.Protect(
    plainTextBytes, 
    entropy: null, 
    scope: DataProtectionScope.CurrentUser
);

// Decryption process  
byte[] decryptedData = ProtectedData.Unprotect(
    encryptedData,
    entropy: null,
    scope: DataProtectionScope.CurrentUser
);
```

### Security Benefits

1. **No Plain Text Storage**: Passwords are never stored in readable format
2. **User Isolation**: Each Windows user's credentials are completely isolated
3. **Machine Binding**: Stolen configuration files cannot be decrypted on other machines
4. **OS-Level Protection**: Leverages Windows security infrastructure
5. **Automatic Key Rotation**: Keys are rotated when user passwords change

## Credential Isolation and User Context Security

### Windows User Context Isolation

#### Process-Level Security
- Each user's encrypted credentials are bound to their Windows login session
- Credentials encrypted by User A cannot be accessed by User B, even with administrative privileges
- The application must run under the same user context that encrypted the credentials

#### File System Permissions
- Configuration files are stored in the user's AppData folder with restricted permissions
- Only the owning user account has read/write access to credential files
- Windows ACLs prevent other users from accessing the configuration directory

### Active Directory Integration

#### Credential Validation
- AD credentials are validated during the launch process, not at storage time
- Invalid credentials result in launch failure with appropriate error messaging
- No credential caching beyond the encrypted storage mechanism

#### Domain Security
- Supports both domain and local account credentials
- Domain policies (password complexity, expiration) are respected
- No bypass of organizational security policies

## Data Storage Locations and Permissions

### Storage Locations

#### Primary Configuration
```
%APPDATA%\V-Launcher\config.json
```
- Contains encrypted AD account credentials
- Executable configuration mappings
- Application settings and preferences (startup behavior, minimize options)

#### Windows Registry
```
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
```
- Contains startup entry when "Start on Windows Start" is enabled
- Stores executable path for automatic startup
- Only accessible by current user (no elevated privileges required)

#### Backup Configuration
```
%APPDATA%\V-Launcher\config.backup.json
```
- Automatic backup created before configuration changes
- Same security properties as primary configuration

### File Permissions

#### Windows ACL Configuration
- **Owner**: Current user (Full Control)
- **System**: Full Control (for backup/restore operations)
- **Administrators**: No access (explicitly denied)
- **Other Users**: No access (explicitly denied)

#### Permission Verification
The application verifies file permissions on startup and will recreate the configuration directory with proper ACLs if permissions are compromised.

## Security Best Practices

### For Users

#### Account Management
1. **Use Dedicated Service Accounts**: Create specific AD accounts for automated processes rather than using personal credentials
2. **Regular Password Rotation**: Update stored credentials when AD passwords change
3. **Principle of Least Privilege**: Only grant necessary permissions to service accounts
4. **Monitor Account Usage**: Review AD logs for unexpected account activity

#### System Security
1. **Keep Windows Updated**: Ensure latest security patches are installed
2. **Use Full Disk Encryption**: Protect against physical access to stored credentials
3. **Regular Backups**: Backup configuration files to secure locations
4. **Antivirus Protection**: Maintain up-to-date antivirus software

#### Startup and Tray Security
1. **Lock Workstation**: Always lock your computer when stepping away
2. **Exit When Not Needed**: Fully exit the application instead of leaving it running in system tray
3. **Review Startup Items**: Regularly check Task Manager startup tab for unexpected entries
4. **Disable Auto-Start**: Disable automatic startup if not required for your workflow
5. **Monitor Background Processes**: Be aware when V-Launcher is running in the system tray

### For Administrators

#### Deployment Security
1. **Code Signing**: Verify application signatures before deployment
2. **Network Isolation**: Deploy on networks with appropriate segmentation
3. **Audit Logging**: Enable Windows audit logging for process creation
4. **Group Policy**: Use GPO to control application deployment and usage

#### Monitoring
1. **Event Log Monitoring**: Monitor Windows Security logs for unusual logon events
2. **File Access Auditing**: Enable auditing for configuration file access
3. **Process Monitoring**: Monitor for unexpected process launches with alternate credentials

## Application Startup and System Tray Security

### Windows Startup Integration

#### Registry-Based Startup
- **Location**: `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`
- **Scope**: User-level startup (no system-wide or elevated privileges)
- **Security**: Only the current user can modify their own startup entries
- **Persistence**: Startup entry persists until explicitly disabled or application is uninstalled

#### Security Considerations
1. **No Elevated Privileges**: Startup does not require or grant administrative rights
2. **User Context**: Application always runs in the user's security context
3. **Malware Protection**: Antivirus software monitors registry startup locations
4. **Audit Trail**: Windows logs registry modifications for security auditing

#### Best Practices
- **Review Startup Items**: Regularly review startup applications in Task Manager
- **Disable When Not Needed**: Disable automatic startup if not required
- **Monitor Registry**: Use security tools to monitor registry startup key changes
- **Verify Executable**: Ensure the startup entry points to the legitimate V-Launcher executable

### System Tray Integration

#### Minimize to Tray Behavior
- **Background Operation**: Application continues running when minimized to system tray
- **Credential Access**: Encrypted credentials remain accessible while in tray
- **Process Visibility**: Application process remains visible in Task Manager
- **Resource Usage**: Minimal CPU and memory usage while in system tray

#### Security Implications
1. **Persistent Access**: Credentials remain decryptable while application is running
2. **Session Duration**: Application may run for extended periods when using tray functionality
3. **Lock Screen**: Application continues running when workstation is locked
4. **User Awareness**: Users may forget the application is running in the background

#### Security Recommendations
1. **Lock Workstation**: Always lock your workstation when leaving it unattended
2. **Exit Completely**: Use "Exit" from tray menu to fully close the application when not needed
3. **Session Timeout**: Consider closing the application at end of work day
4. **Monitor Processes**: Regularly review running processes for unexpected instances

### Startup Security Risks and Mitigations

#### Potential Risks

**Unauthorized Startup Modification**
- **Risk**: Malicious software could modify startup registry entry
- **Mitigation**: Windows Defender and antivirus monitor registry changes
- **Detection**: Regular review of startup items in Task Manager

**Executable Replacement**
- **Risk**: Malicious executable could replace legitimate V-Launcher.exe
- **Mitigation**: Use code signing and verify digital signatures
- **Detection**: Antivirus file integrity monitoring

**Credential Exposure on Startup**
- **Risk**: Application starts automatically with access to encrypted credentials
- **Mitigation**: DPAPI encryption requires user to be logged in
- **Best Practice**: Use "Start Minimized" to reduce visibility

#### Mitigation Strategies

1. **File System Monitoring**: Enable Windows file system auditing
2. **Registry Auditing**: Monitor registry startup key for unauthorized changes
3. **Code Signing**: Verify application digital signature before startup
4. **Startup Delay**: Consider adding delay to startup to allow security software to initialize
5. **User Education**: Train users on startup security best practices

## Security Limitations and Considerations

### Known Limitations

#### Memory Security
- **Temporary Exposure**: Decrypted passwords exist briefly in memory during launch operations
- **Memory Dumps**: Passwords could potentially be recovered from memory dumps
- **Mitigation**: Application uses SecureString and clears sensitive data promptly
- **System Tray Impact**: Credentials remain accessible in memory while application runs in tray

#### Physical Security
- **Local Access**: Users with physical access to the machine could potentially extract credentials
- **Hibernation Files**: Encrypted passwords might be present in hibernation files
- **Mitigation**: Use full disk encryption and disable hibernation on sensitive systems

#### Network Security
- **Credential Transmission**: AD authentication occurs over the network using standard protocols
- **Man-in-the-Middle**: Network-based attacks could potentially intercept authentication
- **Mitigation**: Ensure secure network infrastructure and use VPNs when appropriate

### Attack Vectors and Mitigations

#### Local Privilege Escalation
- **Risk**: Malicious software running as the same user could access encrypted credentials
- **Mitigation**: Maintain updated antivirus, avoid running untrusted software

#### Configuration File Tampering
- **Risk**: Malicious modification of configuration files
- **Mitigation**: Application validates configuration integrity on load

#### Process Injection
- **Risk**: Malicious code injected into the application process
- **Mitigation**: Use application whitelisting and endpoint protection

## Compliance Considerations

### Regulatory Compliance

#### Data Protection Regulations
- **GDPR**: Encrypted storage meets data protection requirements
- **SOX**: Audit trails available through Windows Event Logs
- **HIPAA**: Technical safeguards implemented for credential protection

#### Industry Standards
- **NIST Cybersecurity Framework**: Aligns with Protect and Detect functions
- **ISO 27001**: Supports information security management requirements
- **CIS Controls**: Implements secure configuration and access control measures

### Audit Requirements

#### Logging Capabilities
- Windows Security Event Log captures process launches with alternate credentials
- Application logs credential management operations (without sensitive data)
- File system auditing can track configuration file access

#### Compliance Reporting
- Credential usage can be tracked through Windows Event Logs
- Configuration changes are logged with timestamps
- Failed authentication attempts are recorded
- Application startup events are logged in Windows Event Logs
- Registry modifications for startup entries are auditable
- System tray operations and background execution are traceable through process monitoring

## Incident Response

### Security Incident Procedures

#### Suspected Credential Compromise
1. **Immediate Actions**:
   - Change affected AD account passwords
   - Remove compromised accounts from the application
   - Review Windows Security logs for unauthorized usage

2. **Investigation Steps**:
   - Analyze application logs for unusual activity
   - Check file system audit logs for unauthorized access
   - Review network logs for suspicious authentication attempts

3. **Recovery Actions**:
   - Recreate configuration with new credentials
   - Verify file system permissions
   - Update security monitoring rules

#### Configuration File Corruption
1. **Detection**: Application will report configuration errors on startup
2. **Recovery**: Restore from automatic backup files
3. **Prevention**: Regular configuration backups to secure locations

#### Unauthorized Startup Entry
1. **Detection**: Review Task Manager startup tab or registry key for unexpected modifications
2. **Investigation**: Check Windows Event Logs for registry modification events
3. **Recovery**: Disable startup entry and verify application executable integrity
4. **Prevention**: Use endpoint protection with registry monitoring

#### System Tray Persistence Issues
1. **Detection**: Application remains running in background unexpectedly
2. **Investigation**: Check Task Manager for V-Launcher process
3. **Recovery**: Use tray icon context menu to exit, or terminate process if unresponsive
4. **Prevention**: Configure "Minimize on Close" setting according to security policy

## Security Updates and Maintenance

### Regular Security Tasks

#### Monthly
- Review stored credentials for accuracy
- Check Windows Event Logs for unusual activity
- Verify file system permissions on configuration directory
- Review Windows startup registry entries for unauthorized modifications
- Audit running instances of V-Launcher in Task Manager

#### Quarterly  
- Update AD account passwords and refresh stored credentials
- Review and update security monitoring rules
- Validate backup and recovery procedures
- Review system tray usage patterns and security implications
- Verify application startup behavior aligns with security policies

#### Annually
- Conduct security assessment of deployment
- Review and update security documentation
- Validate compliance with organizational policies
- Assess startup and background operation security posture
- Review and update user training on secure application usage

### Security Contact Information

For security-related questions or to report vulnerabilities:
- Review application logs and Windows Event Logs
- Consult your organization's security team
- Follow your organization's incident response procedures

---

**Last Updated**: November 2024  
**Document Version**: 1.0  
**Application Version**: Compatible with V-Launcher 1.0+