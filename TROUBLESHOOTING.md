# Comprehensive Troubleshooting Guide
## Visual Studio MCP Server - Problem Resolution

**Last Updated:** 12 August 2025  
**Target Audience:** All users experiencing issues with VS MCP Server  
**Coverage:** Installation, configuration, runtime, and integration issues  

---

## 🚨 Emergency Quick Fixes

### MCP Server Won't Start
```bash
# Kill existing processes
pkill -f VisualStudioMcp
dotnet tool uninstall -g VisualStudioMcp.Server
dotnet tool install -g VisualStudioMcp.Server
vsmcp --version
```

### Visual Studio Not Detected
```powershell
# Re-register COM components
& "${env:ProgramFiles}\\Microsoft Visual Studio\\2022\\Professional\\Common7\\IDE\\devenv.exe" /regserver

# Restart Visual Studio
Get-Process devenv | Stop-Process -Force
Start-Process devenv
```

### Claude Code Integration Broken
```bash
# Reset MCP configuration
mv ~/.config/claude-code/mcp-servers.json ~/.config/claude-code/mcp-servers.json.backup
# Recreate configuration with known-good settings
```

---

## 🔧 Installation Issues

### Issue: .NET Tool Installation Failed

**Error Messages:**
```
Error: Could not find a version of VisualStudioMcp.Server that satisfies the version range
Tool 'visualstudiomcp.server' failed to install
```

**Diagnostic Steps:**
```bash
# Check .NET SDK version
dotnet --version
# Should be 8.0.100 or later

# Check available packages
dotnet tool search VisualStudioMcp

# Check global tools location
dotnet tool list -g
```

**Solutions:**

**Solution 1: Update .NET SDK**
```bash
# Download and install latest .NET 8 SDK
# https://dotnet.microsoft.com/download/dotnet/8.0

# Verify installation
dotnet --info
```

**Solution 2: Clear NuGet Caches**
```bash
dotnet nuget locals all --clear
dotnet tool uninstall -g VisualStudioMcp.Server
dotnet tool install -g VisualStudioMcp.Server --version latest
```

**Solution 3: Manual Installation**
```bash
# Build from source
git clone https://github.com/your-org/MCP-VS-AUTOMATION.git
cd MCP-VS-AUTOMATION
dotnet pack src/VisualStudioMcp.Server/ --configuration Release
dotnet tool install -g --add-source ./src/VisualStudioMcp.Server/bin/Release VisualStudioMcp.Server
```

### Issue: Windows Permission Denied

**Error Messages:**
```
Access to the path 'C:\Users\...\dotnet\tools' is denied
Insufficient privileges to perform this operation
```

**Solutions:**

**Solution 1: Run as Administrator**
```powershell
# Open PowerShell as Administrator
Start-Process PowerShell -Verb RunAs
dotnet tool install -g VisualStudioMcp.Server
```

**Solution 2: User-level Installation**
```bash
# Install to user profile instead
dotnet tool install --tool-path ~/.dotnet/tools VisualStudioMcp.Server
export PATH="$PATH:~/.dotnet/tools"
```

**Solution 3: Corporate Environment Workaround**
```bash
# Use local tool manifest
dotnet new tool-manifest
dotnet tool install VisualStudioMcp.Server
dotnet tool run vsmcp
```

### Issue: Missing Dependencies

**Error Messages:**
```
Could not load file or assembly 'Microsoft.Extensions.Hosting'
System.IO.FileNotFoundException: Could not load EnvDTE
```

**Diagnostic Steps:**
```bash
# Check installed workloads
& "${env:ProgramFiles(x86)}\\Microsoft Visual Studio\\Installer\\vs_installer.exe" export --installPath "${env:ProgramFiles}\\Microsoft Visual Studio\\2022\\Professional" --config vs_config.vsconfig
```

**Solutions:**
```bash
# Install required Visual Studio workloads
& "${env:ProgramFiles(x86)}\\Microsoft Visual Studio\\Installer\\vs_installer.exe" modify --installPath "${env:ProgramFiles}\\Microsoft Visual Studio\\2022\\Professional" --add Microsoft.VisualStudio.Workload.ManagedDesktop --add Microsoft.VisualStudio.Workload.NetCoreTools
```

---

## 🔗 COM Interop Issues

### Issue: "Unable to cast COM object of type..."

**Error Messages:**
```
System.InvalidCastException: Unable to cast COM object of type 'System.__ComObject' to interface type 'EnvDTE.DTE'
System.Runtime.InteropServices.COMException: Class not registered (Exception from HRESULT: 0x80040154)
```

**Diagnostic Steps:**
```powershell
# Check if Visual Studio COM objects are registered
Get-ChildItem "HKLM:\SOFTWARE\Classes" | Where-Object { $_.Name -like "*VisualStudio*" }

# Check running Visual Studio processes
Get-Process devenv | Select-Object Id, ProcessName, StartTime, MainWindowTitle

# Test COM access directly
try {
    $dte = [System.Runtime.InteropServices.Marshal]::GetActiveObject("VisualStudio.DTE.17.0")
    Write-Host "✅ COM access working: $($dte.Version)"
} catch {
    Write-Host "❌ COM access failed: $_"
}
```

**Solutions:**

**Solution 1: Re-register COM Components**
```powershell
# Re-register Visual Studio COM components
& "${env:ProgramFiles}\\Microsoft Visual Studio\\2022\\Professional\\Common7\\IDE\\devenv.exe" /regserver

# Wait for registration to complete
Start-Sleep -Seconds 10

# Restart any running VS instances
Get-Process devenv | Stop-Process -Force
```

**Solution 2: Repair Visual Studio Installation**
```powershell
# Run Visual Studio Installer repair
& "${env:ProgramFiles(x86)}\\Microsoft Visual Studio\\Installer\\vs_installer.exe" repair --installPath "${env:ProgramFiles}\\Microsoft Visual Studio\\2022\\Professional"
```

**Solution 3: Check DCOM Configuration**
```powershell
# Open DCOM configuration
dcomcnfg.exe

# Navigate to Component Services > Computers > My Computer > DCOM Config
# Find "Microsoft Development Environment" or "Visual Studio"
# Right-click > Properties > Security tab
# Ensure current user has "Launch and Activation Permissions"
```

### Issue: "Process cannot be found" Errors

**Error Messages:**
```
ArgumentException: No process found with the specified process ID
Process with ID 12345 is not running
Visual Studio instance appears to be shutting down
```

**Diagnostic Steps:**
```csharp
// Add diagnostic code to test process access
var processes = Process.GetProcesses()
    .Where(p => p.ProcessName.Contains("devenv") || p.ProcessName.Contains("VisualStudio"))
    .ToArray();

foreach (var proc in processes)
{
    Console.WriteLine($"Found: {proc.ProcessName} (PID: {proc.Id})");
    try
    {
        Console.WriteLine($"  MainWindowTitle: {proc.MainWindowTitle}");
        Console.WriteLine($"  StartTime: {proc.StartTime}");
        Console.WriteLine($"  Responding: {proc.Responding}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Error accessing process: {ex.Message}");
    }
}
```

**Solutions:**

**Solution 1: Verify Process State**
```powershell
# Get detailed process information
Get-WmiObject Win32_Process | Where-Object { $_.Name -eq "devenv.exe" } | Select-Object ProcessId, CommandLine, CreationDate

# Check if process is responding
$proc = Get-Process devenv -ErrorAction SilentlyContinue
if ($proc) {
    Write-Host "Process responding: $($proc.Responding)"
} else {
    Write-Host "No devenv processes found"
}
```

**Solution 2: Handle Process State Changes**
```csharp
// Implement process monitoring in your code
private bool IsProcessStillAlive(int processId)
{
    try
    {
        var process = Process.GetProcessById(processId);
        return !process.HasExited && process.Responding;
    }
    catch (ArgumentException)
    {
        return false; // Process no longer exists
    }
    catch (InvalidOperationException)
    {
        return false; // Process has exited
    }
}
```

### Issue: Memory Leaks and COM Object Accumulation

**Symptoms:**
```
System.OutOfMemoryException after extended use
Visual Studio becomes unresponsive
MCP server memory usage grows continuously
Process Explorer shows high handle counts
```

**Diagnostic Steps:**
```powershell
# Monitor memory usage
Get-Process VisualStudioMcp* | Select-Object ProcessName, WorkingSet, PagedMemorySize, HandleCount

# Check for COM object leaks
[System.GC]::Collect()
[System.GC]::WaitForPendingFinalizers()
[System.GC]::Collect()
Write-Host "Memory after GC: $([System.GC]::GetTotalMemory($true) / 1MB) MB"
```

**Solutions:**

**Solution 1: Implement Proper COM Object Disposal**
```csharp
// Ensure all COM objects are properly released
public void Dispose()
{
    foreach (var kvp in _connectedInstances.ToArray())
    {
        if (kvp.Value.TryGetTarget(out var dte))
        {
            try
            {
                Marshal.ReleaseComObject(dte);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error releasing COM object");
            }
        }
    }
    _connectedInstances.Clear();
}
```

**Solution 2: Implement Memory Monitoring**
```csharp
// Add memory pressure monitoring
private void MonitorMemoryUsage()
{
    var currentMemory = GC.GetTotalMemory(false);
    if (currentMemory > _maxAllowedMemory)
    {
        _logger.LogWarning("High memory usage detected: {MemoryMB} MB", currentMemory / 1024 / 1024);
        
        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // Clean up dead references
        CleanupDeadReferences();
    }
}
```

---

## 🌐 MCP Protocol Issues

### Issue: "MCP Server not responding"

**Error Messages:**
```
Connection timeout after 30 seconds
Failed to establish MCP connection
No response from MCP server
```

**Diagnostic Steps:**
```bash
# Check if MCP server process is running
ps aux | grep -i visualstudiomcp
# or on Windows:
Get-Process | Where-Object { $_.ProcessName -like "*VisualStudioMcp*" }

# Test direct MCP server communication
echo '{"method":"tools/list","id":1}' | vsmcp

# Check port binding (if using TCP instead of stdio)
netstat -an | grep :8080
```

**Solutions:**

**Solution 1: Restart MCP Server**
```bash
# Kill existing processes
pkill -f VisualStudioMcp
# or on Windows:
Get-Process VisualStudioMcp* | Stop-Process -Force

# Restart server
vsmcp --debug
```

**Solution 2: Check Stdio Communication**
```bash
# Test stdio communication manually
echo '{"method":"tools/list"}' | dotnet run --project src/VisualStudioMcp.Server/

# Should return JSON response with tools list
```

**Solution 3: Verify Claude Code Configuration**
```json
// Check ~/.config/claude-code/mcp-servers.json
{
  "servers": {
    "visual-studio": {
      "command": "vsmcp",
      "args": ["--stdio"],
      "env": {},
      "description": "Visual Studio automation"
    }
  }
}
```

### Issue: "Tool registration failed"

**Error Messages:**
```
Tool 'vs_list_instances' not found
Unknown tool: vs_connect_instance
Method not found: tools/call
```

**Diagnostic Steps:**
```bash
# Verify tool registration
echo '{"method":"tools/list"}' | vsmcp | jq '.result.tools[].name'

# Should output:
# vs_list_instances
# vs_connect_instance
# vs_open_solution
# vs_build_solution
# vs_get_projects
```

**Solutions:**

**Solution 1: Verify Service Registration**
```csharp
// Check Program.cs service registration
services.AddSingleton<VisualStudioMcpServer>();
services.AddSingleton<IVisualStudioService, VisualStudioService>();
// ... other services

// Verify tool registration in VisualStudioMcpServer constructor
_tools["vs_list_instances"] = HandleListInstancesAsync;
_tools["vs_connect_instance"] = HandleConnectInstanceAsync;
// ... other tools
```

**Solution 2: Debug Tool Handler Registration**
```csharp
// Add logging to RegisterMcpTools method
private void RegisterMcpTools()
{
    _logger.LogDebug("Registering MCP tools...");
    
    _tools["vs_list_instances"] = HandleListInstancesAsync;
    _logger.LogDebug("Registered vs_list_instances");
    
    // ... register other tools
    
    _logger.LogInformation("Registered {Count} MCP tools: {Tools}", 
        _tools.Count, string.Join(", ", _tools.Keys));
}
```

### Issue: Protocol Format Errors

**Error Messages:**
```
Invalid JSON in request
Missing required parameter 'processId'
Request format validation failed
```

**Solutions:**

**Solution 1: Validate Request Format**
```json
// Correct format for tool calls
{
  "method": "tools/call",
  "params": {
    "name": "vs_connect_instance",
    "arguments": {
      "processId": 12345
    }
  },
  "id": 1
}
```

**Solution 2: Debug Request Processing**
```csharp
// Add request logging in ProcessRequest method
private async Task<object> ProcessRequest(JsonElement request)
{
    _logger.LogDebug("Received request: {Request}", request.GetRawText());
    
    if (!request.TryGetProperty("method", out var methodElement))
    {
        _logger.LogWarning("Request missing method property");
        return new { error = new { code = "INVALID_REQUEST", message = "Missing method" } };
    }
    
    // ... rest of processing
}
```

---

## 🏗️ Build and Compilation Issues

### Issue: "Target framework not found"

**Error Messages:**
```
error NU1202: Package is not compatible with net8.0-windows7.0
NETSDK1045: The current .NET SDK does not support targeting .NET 8.0
```

**Solutions:**

**Solution 1: Verify .NET SDK Version**
```bash
# Check installed SDKs
dotnet --list-sdks

# Should include 8.0.xxx version
# If missing, download from https://dotnet.microsoft.com/download/dotnet/8.0
```

**Solution 2: Update Global.json (if present)**
```json
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "latestPatch"
  }
}
```

**Solution 3: Verify Project Files**
```xml
<!-- Ensure correct target framework in all .csproj files -->
<PropertyGroup>
  <TargetFramework>net8.0-windows</TargetFramework>
  <UseWindowsForms>true</UseWindowsForms>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

### Issue: "Package restore failed"

**Error Messages:**
```
error NU1101: Unable to find package Microsoft.Extensions.Hosting
Restore failed for project with exit code 1
```

**Diagnostic Steps:**
```bash
# Check package sources
dotnet nuget list source

# Check connectivity to NuGet.org
curl -I https://api.nuget.org/v3-flatcontainer/

# Check corporate proxy settings
dotnet nuget list source --format detailed
```

**Solutions:**

**Solution 1: Clear NuGet Caches**
```bash
dotnet nuget locals all --clear
dotnet restore --no-cache --force
```

**Solution 2: Configure Corporate Proxy**
```xml
<!-- Add to NuGet.Config -->
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <config>
    <add key="http_proxy" value="http://proxy.company.com:8080" />
    <add key="https_proxy" value="https://proxy.company.com:8080" />
  </config>
</configuration>
```

**Solution 3: Use Alternative Package Sources**
```bash
# Add alternative sources
dotnet nuget add source https://pkgs.dev.azure.com/your-org/_packaging/your-feed/nuget/v3/index.json -n "AzureArtifacts"

# Restore with specific source
dotnet restore --source https://api.nuget.org/v3/index.json
```

---

## 🔐 Security Validation Issues

### Issue: "Process ID validation failed"

**Error Messages:**
```
Process ID validation failed: INVALID_PROCESS_TYPE
Specified process is not Visual Studio
Process 'notepad' (PID: 12345) is not a Visual Studio instance
```

**Understanding the Security:**
The MCP server implements strict security validation to prevent targeting non-Visual Studio processes.

**Solutions:**

**Solution 1: Use vs_list_instances First**
```bash
# Get valid Visual Studio process IDs
echo '{"method":"tools/call","params":{"name":"vs_list_instances","arguments":{}}}' | vsmcp

# Use the returned process ID
echo '{"method":"tools/call","params":{"name":"vs_connect_instance","arguments":{"processId":15420}}}' | vsmcp
```

**Solution 2: Verify Process is Visual Studio**
```powershell
# Check process details
Get-Process | Where-Object { $_.Id -eq 12345 } | Select-Object Id, ProcessName, MainWindowTitle

# Only processes named 'devenv' or containing 'VisualStudio' are accepted
```

### Issue: "Path validation failed"

**Error Messages:**
```
Path contains potential traversal attempts
Path validation failed: PATH_TRAVERSAL_DETECTED
Invalid path characters detected
```

**Understanding the Security:**
Path sanitization prevents directory traversal attacks and ensures only valid solution files can be opened.

**Solutions:**

**Solution 1: Use Absolute Paths**
```bash
# ✅ Correct - absolute path
{"solutionPath": "C:\\Projects\\MyProject\\MyProject.sln"}

# ❌ Incorrect - relative path
{"solutionPath": "..\\..\\MyProject\\MyProject.sln"}
```

**Solution 2: Verify File Extension**
```bash
# ✅ Correct - .sln extension
{"solutionPath": "C:\\Dev\\WebApp\\WebApp.sln"}

# ❌ Incorrect - wrong extension
{"solutionPath": "C:\\Dev\\WebApp\\WebApp.csproj"}
```

**Solution 3: Check File Exists**
```powershell
# Verify file exists before trying to open
Test-Path "C:\\Projects\\MyProject\\MyProject.sln"
```

---

## 📊 Performance Issues

### Issue: Slow Response Times

**Symptoms:**
```
MCP tool calls taking >5 seconds
Visual Studio becomes unresponsive during operations
Build operations timeout
```

**Diagnostic Steps:**
```bash
# Monitor CPU and memory usage
top -p $(pgrep -f VisualStudioMcp)

# Check Visual Studio resource usage
Get-Process devenv | Select-Object CPU, WorkingSet, PagedMemorySize
```

**Solutions:**

**Solution 1: Optimize Visual Studio**
```bash
# Close unnecessary VS instances
Get-Process devenv | Where-Object { $_.MainWindowTitle -eq "" } | Stop-Process

# Clean up temporary files
Remove-Item "$env:TEMP\*" -Recurse -Force -ErrorAction SilentlyContinue
```

**Solution 2: Tune MCP Server**
```csharp
// Add timeout configuration
public class MCP ServerConfiguration
{
    public int CommandTimeoutMs { get; set; } = 30000;
    public int HealthCheckIntervalMs { get; set; } = 30000;
    public int MaxConcurrentOperations { get; set; } = 5;
}
```

**Solution 3: Monitor Performance**
```csharp
// Add performance monitoring
private readonly Dictionary<string, TimeSpan> _operationTimes = new();

private async Task<T> MonitorOperation<T>(string operationName, Func<Task<T>> operation)
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        var result = await operation();
        _operationTimes[operationName] = stopwatch.Elapsed;
        
        if (stopwatch.ElapsedMilliseconds > 1000)
        {
            _logger.LogWarning("Slow operation: {Operation} took {ElapsedMs}ms", 
                operationName, stopwatch.ElapsedMilliseconds);
        }
        
        return result;
    }
    finally
    {
        stopwatch.Stop();
    }
}
```

---

## 🔍 Diagnostic Tools and Commands

### System Information Gathering

```bash
# Complete system diagnostic
echo "=== System Information ===" > diagnostic.txt
uname -a >> diagnostic.txt
dotnet --info >> diagnostic.txt

echo "=== Visual Studio Processes ===" >> diagnostic.txt
Get-Process devenv | Select-Object Id, ProcessName, StartTime, WorkingSet >> diagnostic.txt

echo "=== MCP Server Status ===" >> diagnostic.txt
ps aux | grep VisualStudioMcp >> diagnostic.txt

echo "=== COM Registration ===" >> diagnostic.txt
Get-ChildItem "HKLM:\SOFTWARE\Classes" | Where-Object { $_.Name -like "*VisualStudio*" } >> diagnostic.txt

echo "=== Network Configuration ===" >> diagnostic.txt
netstat -an | grep -E "(8080|3000)" >> diagnostic.txt
```

### Log Analysis Scripts

```powershell
# Analyze MCP server logs
function Analyze-McpLogs {
    param([string]$LogPath)
    
    $logs = Get-Content $LogPath | ConvertFrom-Json
    
    $errors = $logs | Where-Object { $_.Level -eq "Error" }
    $warnings = $logs | Where-Object { $_.Level -eq "Warning" }
    $toolCalls = $logs | Where-Object { $_.Message -like "*Executing*tool*" }
    
    Write-Host "Errors: $($errors.Count)"
    Write-Host "Warnings: $($warnings.Count)" 
    Write-Host "Tool calls: $($toolCalls.Count)"
    
    if ($errors.Count -gt 0) {
        Write-Host "`nRecent errors:"
        $errors | Select-Object -Last 5 | Format-Table Timestamp, Message
    }
}
```

### Health Check Script

```bash
#!/bin/bash
# health-check.sh - Complete MCP server health verification

echo "🔍 MCP Server Health Check"
echo "========================="

# Check .NET installation
echo "📦 .NET SDK Version:"
dotnet --version
if [ $? -ne 0 ]; then
    echo "❌ .NET SDK not installed or not in PATH"
    exit 1
fi

# Check MCP server installation
echo "🔧 MCP Server Installation:"
which vsmcp
if [ $? -ne 0 ]; then
    echo "❌ MCP server not installed as global tool"
    exit 1
fi

# Check Visual Studio processes
echo "🏗️ Visual Studio Processes:"
ps aux | grep devenv | grep -v grep
if [ $? -ne 0 ]; then
    echo "⚠️ No Visual Studio processes found"
fi

# Test MCP server response
echo "🌐 MCP Server Response:"
timeout 5s bash -c 'echo "{\"method\":\"tools/list\"}" | vsmcp'
if [ $? -eq 0 ]; then
    echo "✅ MCP server responding correctly"
else
    echo "❌ MCP server not responding"
    exit 1
fi

echo "✅ Health check completed successfully"
```

---

## 📞 When to Seek Help

### Self-Service Resolution (Try First)
- **Installation issues:** Follow the installation troubleshooting above
- **COM errors:** Re-register Visual Studio COM components
- **Performance problems:** Restart Visual Studio and MCP server
- **Protocol errors:** Verify request format and tool registration

### Community Support (GitHub)
- **Usage questions:** GitHub Discussions
- **Feature requests:** GitHub Issues with enhancement label
- **Documentation improvements:** Submit pull requests
- **General troubleshooting:** Community support in discussions

### Bug Reports (GitHub Issues)
**Include in bug reports:**
- Complete error messages and stack traces
- System information (OS, .NET version, VS version)
- Steps to reproduce the issue
- Output from diagnostic scripts above
- MCP server logs (redacted for sensitive information)

### Emergency Support Escalation
**Critical issues requiring immediate attention:**
- Security vulnerabilities
- Data corruption or loss
- Complete system failure preventing development work
- Production deployment blocking issues

---

## 📋 Troubleshooting Checklist

### ✅ Before Reporting Issues
- [ ] **Tried restarting** Visual Studio and MCP server
- [ ] **Checked logs** for specific error messages
- [ ] **Verified prerequisites** (.NET 8, VS 2022 17.8+)
- [ ] **Tested with minimal setup** (single VS instance, simple solution)
- [ ] **Reviewed recent changes** that might have caused the issue
- [ ] **Searched existing issues** on GitHub for similar problems

### ✅ Information to Gather
- [ ] **Exact error messages** (copy/paste, not screenshots)
- [ ] **System configuration** (OS, .NET version, VS edition)
- [ ] **Reproduction steps** that consistently cause the issue
- [ ] **Expected vs actual behavior** clearly described
- [ ] **Workarounds attempted** and their results
- [ ] **Log files** with timestamps around the issue occurrence

---

*This comprehensive troubleshooting guide covers the most common issues encountered with the Visual Studio MCP Server. Follow the diagnostic steps and solutions systematically to resolve problems efficiently.*