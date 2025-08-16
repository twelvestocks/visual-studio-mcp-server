# Visual Studio MCP Server Troubleshooting Guide

Comprehensive troubleshooting guide for the Visual Studio MCP Server, covering Phase 5 Advanced Visual Capture features, common issues, diagnostic procedures, and resolution strategies.

## 📋 Quick Diagnostic Checklist

Before diving into specific issues, run through this quick diagnostic checklist:

### System Requirements Verification
```bash
# Check .NET runtime version
dotnet --version

# Verify Visual Studio installation
reg query "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\Setup\Instances"

# Test MCP server connectivity
vsmcp --test-connection

# Check available memory
systeminfo | findstr "Available Physical Memory"
```

### Basic Health Check
```bash
# Verify MCP server status
vsmcp --status

# Test basic capture functionality
vsmcp --test-capture

# Check for running Visual Studio instances
vsmcp --list-vs-instances

# Validate configuration
vsmcp --validate-config
```

---

## 🚨 Critical Issues (Service Breaking)

### 1. MCP Server Won't Start

#### Symptoms
- Service fails to start with error codes
- Port binding errors (e.g., "Address already in use")
- Permission denied errors
- Configuration validation failures

#### Diagnostic Steps
```bash
# Check port availability
netstat -an | findstr :8080

# Verify service account permissions
sc qc "Visual Studio MCP Server"

# Check Windows Event Log
eventvwr.msc
# Navigate to: Windows Logs > Application > Source: VisualStudioMcp
```

#### Common Causes and Solutions

**Port Conflict**
```bash
# Find process using the port
netstat -ano | findstr :8080

# Kill conflicting process (if safe)
taskkill /PID <process_id> /F

# Or configure alternative port
vsmcp --config-port 8081
```

**Permission Issues**
```cmd
# Run as Administrator
runas /user:Administrator "vsmcp --start"

# Grant service account permissions
icacls "C:\Program Files\VisualStudioMcp" /grant "NT AUTHORITY\NETWORK SERVICE:(OI)(CI)F"
```

**Configuration Corruption**
```bash
# Reset to default configuration
vsmcp --reset-config

# Validate configuration file
vsmcp --validate-config --verbose

# Regenerate configuration
vsmcp --generate-config --force
```

#### Advanced Troubleshooting
```powershell
# Enable debug logging
$env:VSMCP_LOG_LEVEL = "Debug"
vsmcp --start --verbose

# Check dependency loading
vsmcp --check-dependencies

# Test COM component registration
regsvr32 /s "C:\Program Files\VisualStudioMcp\VisualStudioMcp.ComInterop.dll"
```

### 2. Visual Studio COM Connection Failures

#### Symptoms
- "Visual Studio instance not found" errors
- "Access denied" when attempting to connect
- Intermittent connection drops
- COM exception: 0x80040154 (Class not registered)

#### Diagnostic Steps
```bash
# List available Visual Studio instances
vsmcp --list-vs-instances --detailed

# Test COM connection directly
vsmcp --test-com --instance-id <id>

# Check Visual Studio process state
tasklist | findstr devenv
```

#### Common Causes and Solutions

**Visual Studio Not Running**
```bash
# Start Visual Studio programmatically
start "" "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe"

# Wait for Visual Studio to fully initialize
timeout /t 30

# Retry connection
vsmcp --connect --instance-id <id>
```

**COM Registration Issues**
```cmd
# Re-register Visual Studio COM components (as Administrator)
cd "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\"
regsvr32 /s Microsoft.VisualStudio.OLE.Interop.dll
regsvr32 /s envdte.dll
regsvr32 /s envdte80.dll

# Reset Visual Studio settings
devenv.exe /ResetSettings
```

**Permission/Security Issues**
```cmd
# Run Visual Studio as Administrator
runas /user:Administrator devenv.exe

# Configure DCOM permissions (run dcomcnfg.exe as Administrator)
# Navigate to Component Services > Computers > My Computer > DCOM Config
# Find "Microsoft Visual Studio" application
# Right-click > Properties > Security
# Grant "Local Launch" and "Local Activation" permissions
```

**Multiple Visual Studio Instances**
```bash
# Kill all Visual Studio processes
taskkill /im devenv.exe /f

# Start single instance
start "" "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe"

# Connect to specific instance
vsmcp --connect --process-id <pid>
```

---

## ⚠️ Major Issues (Functionality Impaired)

### 3. Window Enumeration Failures

#### Symptoms
- "No windows found" despite Visual Studio being open
- Incomplete window lists
- Window enumeration timeouts (>30 seconds)
- AccessViolationException during enumeration

#### Diagnostic Steps
```bash
# Test window enumeration with verbose logging
vsmcp --enumerate-windows --verbose --timeout 60

# Check window visibility states
vsmcp --analyze-window-states

# Test with different enumeration strategies
vsmcp --enumerate-windows --strategy alternative
```

#### Common Causes and Solutions

**Security Policy Blocking Window Access**
```powershell
# Check User Account Control settings
Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System" -Name "EnableLUA"

# Temporarily disable UAC (requires restart)
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System" -Name "EnableLUA" -Value 0

# Alternative: Run MCP server elevated
Start-Process -FilePath "vsmcp.exe" -Verb RunAs
```

**Windows API Rate Limiting**
```csharp
// Adjust enumeration timing in configuration
{
  "windowEnumeration": {
    "delayBetweenCalls": 50,
    "maxConcurrentEnumerations": 2,
    "retryAttempts": 3,
    "timeout": 45000
  }
}
```

**Memory Pressure During Enumeration**
```bash
# Check available memory before enumeration
vsmcp --check-memory

# Force garbage collection before enumeration
vsmcp --enumerate-windows --force-gc

# Reduce enumeration scope
vsmcp --enumerate-windows --visible-only --top-level-only
```

**Visual Studio Window State Issues**
```bash
# Restore Visual Studio windows
vsmcp --restore-vs-windows

# Reset Visual Studio layout
devenv.exe /ResetSettings

# Test enumeration with specific window types
vsmcp --enumerate-windows --window-types "CodeEditor,SolutionExplorer"
```

#### Advanced Diagnostics
```bash
# Enable detailed P/Invoke logging
set VSMCP_PINVOKE_DEBUG=true
vsmcp --enumerate-windows --debug-pinvoke

# Test enumeration with different security contexts
runas /user:SYSTEM "vsmcp --enumerate-windows"

# Check for antivirus interference
vsmcp --check-security-software-interference
```

### 4. Capture Timeout Issues

#### Symptoms
- Capture operations timing out after 30 seconds
- "Operation cancelled" errors
- Partial captures with missing content
- UI freezing during capture operations

#### Diagnostic Steps
```bash
# Test capture with extended timeout
vsmcp --capture-window --handle <hwnd> --timeout 120

# Monitor system performance during capture
vsmcp --capture-window --handle <hwnd> --monitor-performance

# Test with reduced capture quality
vsmcp --capture-window --handle <hwnd> --quality fast
```

#### Common Causes and Solutions

**Insufficient System Resources**
```bash
# Close unnecessary applications
taskkill /im chrome.exe /f
taskkill /im outlook.exe /f

# Increase virtual memory
# Control Panel > System > Advanced > Performance Settings > Advanced > Virtual Memory

# Check disk space
dir C:\ | findstr "bytes free"
```

**Large Window Capture Size**
```bash
# Limit capture dimensions
vsmcp --capture-window --handle <hwnd> --max-width 1920 --max-height 1080

# Use progressive capture strategy
vsmcp --capture-window --handle <hwnd> --strategy progressive

# Enable capture compression
vsmcp --capture-window --handle <hwnd> --compress true
```

**Graphics Driver Issues**
```bash
# Update graphics drivers
# Device Manager > Display adapters > Update driver

# Test with software rendering
vsmcp --capture-window --handle <hwnd> --force-software-rendering

# Check graphics capabilities
dxdiag /t dxdiag.txt
```

**Threading Contention**
```csharp
// Adjust concurrency settings
{
  "captureSettings": {
    "maxConcurrentCaptures": 1,
    "threadPoolMinThreads": 2,
    "threadPoolMaxThreads": 8,
    "enableAsyncCapture": false
  }
}
```

---

## ⚡ Performance Issues

### 5. Memory Pressure Warnings

#### Symptoms
- "Memory pressure detected" warnings in logs
- Capture operations rejected due to memory limits
- System slowdown during capture operations
- OutOfMemoryException errors

#### Diagnostic Steps
```bash
# Check current memory usage
vsmcp --check-memory-pressure

# Monitor memory during capture operations
vsmcp --monitor-memory --duration 300

# Analyze memory usage patterns
vsmcp --analyze-memory-patterns
```

#### Memory Monitoring Commands
```bash
# Real-time memory monitoring
typeperf "\Process(vsmcp)\Working Set" "\Memory\Available MBytes" -si 5

# Check for memory leaks
vsmcp --check-memory-leaks --duration 600

# Force garbage collection
vsmcp --force-gc --detailed
```

#### Solutions

**Adjust Memory Thresholds**
```json
{
  "memoryManagement": {
    "warningThreshold": "100MB",
    "rejectionThreshold": "200MB",
    "emergencyThreshold": "400MB",
    "adaptiveThresholds": true,
    "systemMemoryFactor": 0.1
  }
}
```

**Enable Aggressive Cleanup**
```json
{
  "memoryManagement": {
    "enableAggressiveCleanup": true,
    "cleanupInterval": 30000,
    "forceGCOnPressure": true,
    "clearCacheOnPressure": true
  }
}
```

**Optimize Capture Strategy**
```bash
# Use sequential captures instead of parallel
vsmcp --capture-full-ide --strategy sequential

# Reduce capture quality temporarily
vsmcp --capture-window --quality balanced

# Enable streaming capture for large operations
vsmcp --capture-window --stream true --chunk-size 1MB
```

### 6. Slow Capture Performance

#### Symptoms
- Capture operations taking longer than 5 seconds
- Visual Studio becomes unresponsive during captures
- High CPU usage during capture operations
- Network timeouts with Claude Code integration

#### Performance Diagnostics
```bash
# Benchmark capture performance
vsmcp --benchmark-capture --iterations 10

# Profile capture operations
vsmcp --profile-capture --detailed

# Test different quality settings
vsmcp --performance-test --quality-range fast,balanced,high
```

#### Solutions

**Optimize Capture Settings**
```json
{
  "captureOptimization": {
    "defaultQuality": "balanced",
    "enableCaching": true,
    "cacheExpiration": 300,
    "parallelProcessing": true,
    "maxConcurrency": 4
  }
}
```

**System Performance Tuning**
```bash
# Set high performance power plan
powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c

# Increase process priority
vsmcp --set-priority high

# Disable Windows visual effects
SystemPropertiesPerformance.exe
```

**Graphics Optimization**
```bash
# Enable hardware acceleration
vsmcp --enable-hardware-acceleration

# Optimize DPI settings
vsmcp --set-dpi-awareness permonitorv2

# Use dedicated graphics for capture
vsmcp --prefer-discrete-graphics
```

---

## 🔒 Security and Access Issues

### 7. Process Access Denied Errors

#### Symptoms
- ArgumentException: "Process with ID X is not running"
- InvalidOperationException: "Process has exited"
- "Access denied" when enumerating processes
- Security policy blocking process access

#### Security Diagnostics
```bash
# Check current user permissions
whoami /all

# Test process access capabilities
vsmcp --test-process-access

# Check security policy restrictions
gpresult /r | findstr "Process"
```

#### Solutions

**Run with Elevated Permissions**
```cmd
# Run as Administrator
runas /user:Administrator vsmcp.exe

# Run as SYSTEM (for testing only)
psexec -s vsmcp.exe
```

**Configure Security Policies**
```bash
# Enable "Debug programs" privilege
secpol.msc
# Navigate to: Local Policies > User Rights Assignment
# Add user to "Debug programs" policy

# Disable "Prevent access to drives from My Computer"
gpedit.msc
# Navigate to: User Configuration > Administrative Templates > Windows Components > File Explorer
```

**Handle Security Exceptions Gracefully**
```csharp
// Enhanced exception handling in code
try
{
    var process = Process.GetProcessById(processId);
    return ValidateProcess(process);
}
catch (ArgumentException ex)
{
    _logger.LogWarning("Process access denied or not found: {ProcessId}", processId);
    return CreateSecurityDeniedResult(processId);
}
catch (InvalidOperationException ex)
{
    _logger.LogWarning("Process has terminated: {ProcessId}", processId);
    return CreateProcessTerminatedResult(processId);
}
```

### 8. COM Security Configuration Issues

#### Symptoms
- DCOM permission errors
- COM class registration failures
- "Class not registered" errors
- Authentication failures with Visual Studio

#### DCOM Configuration
```cmd
# Open DCOM configuration (as Administrator)
dcomcnfg.exe

# Navigate to Component Services > Computers > My Computer > DCOM Config
# Find "Microsoft Visual Studio" or related applications
# Configure security settings:
```

**Authentication Tab:**
- Authentication Level: None or Connect
- Enable Distributed COM on this computer: Checked

**Security Tab:**
- Access Permissions: Add "Everyone" with "Local Access" and "Remote Access"
- Launch and Activation Permissions: Add service account with all permissions
- Configuration Permissions: Add administrators group

#### Manual COM Registration
```cmd
# Register Visual Studio COM components
cd "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\"
regsvr32 envdte.dll
regsvr32 envdte80.dll
regsvr32 Microsoft.VisualStudio.OLE.Interop.dll

# Register MCP server COM components
regsvr32 "C:\Program Files\VisualStudioMcp\VisualStudioMcp.ComInterop.dll"
```

---

## 📱 Multi-Monitor and Display Issues

### 9. Multi-Monitor Capture Problems

#### Symptoms
- Captures only showing primary monitor content
- Incorrect window positioning in multi-monitor setups
- DPI scaling issues across different monitors
- Color profile inconsistencies

#### Multi-Monitor Diagnostics
```bash
# Check monitor configuration
vsmcp --list-monitors --detailed

# Test capture on each monitor
vsmcp --test-multi-monitor-capture

# Check DPI settings per monitor
vsmcp --check-dpi-scaling --all-monitors
```

#### Solutions

**Configure Monitor Awareness**
```json
{
  "displaySettings": {
    "multiMonitorSupport": true,
    "dpiAwareness": "perMonitorV2",
    "colorProfileHandling": "auto",
    "coordinateNormalization": true
  }
}
```

**DPI Scaling Configuration**
```bash
# Set application DPI awareness
vsmcp --set-dpi-awareness permonitorv2

# Override high DPI scaling
vsmcp --override-high-dpi-scaling true

# Test DPI scaling compensation
vsmcp --test-dpi-compensation
```

**Monitor-Specific Capture**
```bash
# Capture specific monitor
vsmcp --capture-monitor --monitor-id 2

# Capture with coordinate mapping
vsmcp --capture-window --handle <hwnd> --map-coordinates true

# Normalize multi-monitor coordinates
vsmcp --capture-full-ide --normalize-coordinates true
```

### 10. Color Profile and Quality Issues

#### Symptoms
- Color accuracy problems in captures
- Washed out or oversaturated images
- Different colors between monitors
- Poor text clarity in captures

#### Color Diagnostics
```bash
# Check color profiles
vsmcp --list-color-profiles

# Test color accuracy
vsmcp --test-color-accuracy --reference-image <path>

# Check gamma settings
vsmcp --check-gamma-correction
```

#### Solutions

**Color Management Configuration**
```json
{
  "colorManagement": {
    "enableColorProfileCorrection": true,
    "preserveColorAccuracy": true,
    "gammaCorrection": 2.2,
    "colorSpace": "sRGB"
  }
}
```

**Quality Optimization**
```bash
# Capture with high color fidelity
vsmcp --capture-window --handle <hwnd> --color-fidelity high

# Apply color profile correction
vsmcp --capture-window --handle <hwnd> --correct-color-profile true

# Use uncompressed capture for accuracy
vsmcp --capture-window --handle <hwnd> --compression none
```

---

## 🔧 Configuration and Integration Issues

### 11. Claude Code Integration Problems

#### Symptoms
- MCP tools not responding in Claude Code
- Timeout errors during Claude Code operations
- Malformed responses or incomplete data
- Connection drops during large operations

#### Integration Diagnostics
```bash
# Test MCP protocol compliance
vsmcp --test-mcp-protocol

# Validate tool responses
vsmcp --validate-tool-responses

# Check Claude Code connectivity
vsmcp --test-claude-integration
```

#### Solutions

**MCP Configuration Optimization**
```json
{
  "mcpSettings": {
    "responseTimeout": 120000,
    "maxResponseSize": "50MB",
    "enableCompression": true,
    "streamingSupport": true,
    "retryAttempts": 3
  }
}
```

**Response Size Management**
```bash
# Limit response sizes for Claude Code
vsmcp --set-max-response-size 25MB

# Enable response streaming
vsmcp --enable-streaming-responses

# Compress large responses
vsmcp --enable-response-compression
```

**Connection Reliability**
```bash
# Enable connection keepalive
vsmcp --enable-keepalive --interval 30

# Configure retry logic
vsmcp --set-retry-policy exponential --max-attempts 5

# Test connection stability
vsmcp --test-connection-stability --duration 3600
```

### 12. Configuration File Issues

#### Symptoms
- Configuration validation errors
- Settings not taking effect
- Default values being used despite configuration
- JSON parsing errors

#### Configuration Diagnostics
```bash
# Validate configuration file
vsmcp --validate-config --file <path>

# Check configuration precedence
vsmcp --show-config-sources

# Test configuration changes
vsmcp --test-config --dry-run
```

#### Solutions

**Configuration File Structure**
```json
{
  "logging": {
    "level": "Information",
    "file": "logs/vsmcp.log",
    "enableConsole": true
  },
  "server": {
    "port": 8080,
    "host": "localhost",
    "enableHttps": false
  },
  "visualStudio": {
    "timeout": 30000,
    "retryAttempts": 3,
    "enableCOM": true
  },
  "capture": {
    "defaultQuality": "balanced",
    "timeout": 60000,
    "memoryThreshold": "100MB"
  }
}
```

**Configuration Validation**
```bash
# Reset to default configuration
vsmcp --reset-config

# Merge configuration files
vsmcp --merge-config base.json custom.json

# Export current configuration
vsmcp --export-config --output current-config.json
```

---

## 🔍 Advanced Diagnostics

### Comprehensive System Diagnosis

#### Full System Health Check
```bash
# Run comprehensive diagnostics
vsmcp --full-diagnostics --output diagnostics-report.json

# Check all dependencies
vsmcp --check-dependencies --detailed

# Validate system requirements
vsmcp --validate-system-requirements
```

#### Performance Profiling
```bash
# Profile capture performance
vsmcp --profile --operation capture-window --iterations 10

# Memory profiling
vsmcp --profile-memory --duration 300

# Thread profiling
vsmcp --profile-threads --capture-full-ide
```

#### Log Analysis
```bash
# Analyze error patterns
vsmcp --analyze-logs --pattern "ERROR|WARN" --since "1h"

# Export diagnostic logs
vsmcp --export-logs --format json --output debug-logs.json

# Enable trace-level logging
vsmcp --set-log-level trace --duration 600
```

### Environment Information Collection

#### System Information
```powershell
# Collect system information
$info = @{
    OS = (Get-CimInstance Win32_OperatingSystem).Caption
    Memory = (Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory
    CPU = (Get-CimInstance Win32_Processor).Name
    Graphics = (Get-CimInstance Win32_VideoController).Name
    VSVersion = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\VisualStudio\*\Setup\VS" -ErrorAction SilentlyContinue).ProductDisplayVersion
    DotNetVersion = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" -ErrorAction SilentlyContinue).Version
}
$info | ConvertTo-Json | Out-File system-info.json
```

#### Visual Studio Configuration
```bash
# Export Visual Studio settings
devenv.exe /Export vs-settings.vssettings

# List installed extensions
vsmcp --list-vs-extensions

# Check Visual Studio integrity
vsmcp --verify-vs-installation
```

---

## 📞 Support and Escalation

### Before Contacting Support

**Collect the following information:**
1. System information (OS, memory, CPU, graphics)
2. Visual Studio version and installed extensions
3. MCP server version and configuration
4. Error logs and diagnostic reports
5. Steps to reproduce the issue
6. Screenshots or videos of the problem

**Run diagnostic commands:**
```bash
# Generate support bundle
vsmcp --generate-support-bundle --output support-bundle.zip

# Test minimal configuration
vsmcp --test-minimal-config

# Verify installation integrity
vsmcp --verify-installation --detailed
```

### Log Collection for Support

#### Enable Comprehensive Logging
```json
{
  "logging": {
    "level": "Trace",
    "enableFileLogging": true,
    "enableConsoleLogging": true,
    "enableETW": true,
    "loggers": {
      "Microsoft": "Warning",
      "System": "Warning",
      "VisualStudioMcp": "Trace",
      "WindowManagement": "Debug",
      "CaptureOperations": "Debug",
      "MemoryManagement": "Debug"
    }
  }
}
```

#### Collect Diagnostic Data
```bash
# Export all logs
vsmcp --export-logs --all --output logs.zip

# Generate memory dump (if process hangs)
procdump -ma vsmcp.exe memory-dump.dmp

# Collect Windows Event Logs
wevtutil epl Application application-events.evtx
wevtutil epl System system-events.evtx
```

### Support Channels

**Technical Support:**
- Email: support@vsmcp.com
- Documentation: https://docs.vsmcp.com
- Community Forum: https://community.vsmcp.com

**Bug Reports:**
- GitHub Issues: https://github.com/vsmcp/vsmcp-server/issues
- Include diagnostic bundle and reproduction steps

**Emergency Support:**
- Critical production issues: emergency@vsmcp.com
- Include "CRITICAL" in subject line
- Provide complete diagnostic information

---

## 📚 Additional Resources

### Documentation References
- [Installation Guide](../installation/installation-guide.md)
- [Configuration Reference](../configuration/configuration-reference.md)
- [API Documentation](../api/mcp-tools-reference.md)
- [Security Guide](../security/security-guide.md)
- [Performance Tuning](../performance/performance-optimization.md)

### Diagnostic Tools
- [System Requirements Checker](../tools/system-requirements-checker.md)
- [Configuration Validator](../tools/configuration-validator.md)
- [Performance Profiler](../tools/performance-profiler.md)
- [Memory Analyzer](../tools/memory-analyzer.md)

### Community Resources
- **Stack Overflow**: Use tag `visual-studio-mcp-server`
- **Reddit**: r/VisualStudioMCP
- **Discord**: MCP Server Community Channel

---

*This troubleshooting guide is regularly updated based on common support requests and community feedback. For the latest version, visit the documentation website.*