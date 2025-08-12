# Development Environment Setup & Validation
## Visual Studio MCP Server - Developer Guide

**Last Updated:** 12 August 2025  
**Target Audience:** New contributors, maintainers, and developers  
**Estimated Setup Time:** 20-30 minutes  

---

## Quick Start Checklist

Use this checklist to validate your setup step-by-step:

- [ ] **Prerequisites installed and verified**
- [ ] **Repository cloned and configured**
- [ ] **Solution builds successfully**
- [ ] **COM interop tested with sample VS instance**
- [ ] **MCP server runs and responds**
- [ ] **Health monitoring functions correctly**

---

## Prerequisites Verification

### 1. Visual Studio 2022 (17.8 or later)

**Required Workloads:**
- ✅ **.NET desktop development** - Essential for .NET 8 projects
- ✅ **Visual Studio extension development** - Required for COM interop testing
- ✅ **Windows SDK** - Latest version for COM components

**Verification Commands:**
```powershell
# Check Visual Studio version
& "${env:ProgramFiles}\\Microsoft Visual Studio\\2022\\Professional\\Common7\\IDE\\devenv.exe" /?

# Expected output should show version 17.8.x or later
```

**Installation Issues?**
- Download from: https://visualstudio.microsoft.com/vs/
- Choose "Professional" or "Enterprise" edition
- If using "Community", ensure extension development workload is available

### 2. .NET 8 SDK Validation

**Installation Check:**
```bash
dotnet --version
```
**Expected Output:** `8.0.100` or later

**Verification Test:**
```bash
dotnet --list-sdks
dotnet --list-runtimes
```

**Installation Issues?**
- Download from: https://dotnet.microsoft.com/download/dotnet/8.0
- Install the SDK (not just runtime)
- Restart your terminal after installation

### 3. Windows Version Compatibility

**Supported Versions:**
- ✅ **Windows 10** (version 1903 or later)
- ✅ **Windows 11** (all versions)
- ❌ **Windows Server** (limited COM interop support)

**Verification:**
```powershell
Get-ComputerInfo | Select-Object WindowsProductName, WindowsVersion
```

### 4. COM Component Registration

**Test COM Access:**
```powershell
# Test if we can access Running Object Table
$rot = [System.Runtime.InteropServices.Marshal]::GetActiveObject("VisualStudio.DTE")
if ($rot) { 
    Write-Host "✅ COM interop accessible" -ForegroundColor Green 
} else { 
    Write-Host "❌ COM interop issue - Visual Studio must be running" -ForegroundColor Yellow 
}
```

---

## Step-by-Step Setup Process

### Step 1: Repository Setup

**1.1 Clone the Repository**
```bash
git clone https://github.com/your-org/MCP-VS-AUTOMATION.git
cd MCP-VS-AUTOMATION
```

**1.2 Create Feature Branch**
```bash
# Always work in feature branches
git checkout -b feature/your-feature-name
```

**1.3 Verify Repository Structure**
```bash
# Check that all directories exist
ls -la
# Should see: src/, tests/, docs/, *.sln, *.md files
```

### Step 2: NuGet Package Restoration

**2.1 Restore Packages**
```bash
dotnet restore VisualStudioMcp.sln
```

**Expected Output:**
```
Restored packages for 9 projects in 45.2 sec
```

**2.2 Verify Package Dependencies**
```bash
dotnet list package --include-transitive
```

**Common Issues:**
- **Package conflicts:** Clear NuGet cache: `dotnet nuget locals all --clear`
- **Network issues:** Configure corporate proxy in NuGet.config
- **Version mismatches:** Update packages: `dotnet add package <PackageName>`

### Step 3: Build Validation

**3.1 Clean Build**
```bash
dotnet clean VisualStudioMcp.sln
dotnet build VisualStudioMcp.sln --configuration Debug
```

**Expected Output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:08.45
```

**3.2 Verify All Projects Build**
```bash
# Check each project individually
dotnet build src/VisualStudioMcp.Server/VisualStudioMcp.Server.csproj
dotnet build src/VisualStudioMcp.Core/VisualStudioMcp.Core.csproj
# ... repeat for all projects
```

**Build Troubleshooting:**
- **COM reference errors:** Ensure Visual Studio 2022 is installed
- **Target framework errors:** Verify .NET 8 SDK installation
- **Missing dependencies:** Run `dotnet restore` again

### Step 4: COM Interop Testing

**4.1 Start Visual Studio Instance**
```powershell
# Start Visual Studio with a solution loaded
Start-Process "${env:ProgramFiles}\\Microsoft Visual Studio\\2022\\Professional\\Common7\\IDE\\devenv.exe"
```

**4.2 Test DTE Object Access**
```powershell
# PowerShell script to test COM access
try {
    $dte = [System.Runtime.InteropServices.Marshal]::GetActiveObject("VisualStudio.DTE.17.0")
    Write-Host "✅ Successfully connected to Visual Studio DTE" -ForegroundColor Green
    Write-Host "Version: $($dte.Version)" -ForegroundColor Cyan
    Write-Host "Process ID: $($dte.MainWindow.HWnd)" -ForegroundColor Cyan
} catch {
    Write-Host "❌ Failed to connect to Visual Studio DTE: $_" -ForegroundColor Red
}
```

### Step 5: MCP Server Testing

**5.1 Build and Run MCP Server**
```bash
dotnet run --project src/VisualStudioMcp.Server/VisualStudioMcp.Server.csproj
```

**Expected Output:**
```
[16:45:23] INFO: Visual Studio MCP Server starting...
[16:45:23] INFO: Registered 5 MCP tools
[16:45:23] INFO: MCP Server ready - waiting for requests...
```

**5.2 Test MCP Protocol Handshake**
In another terminal:
```bash
# Test tools list request
echo '{"method":"tools/list","id":1}' | dotnet run --project src/VisualStudioMcp.Server/
```

**Expected Response:**
```json
{
  "result": {
    "tools": [
      {"name": "vs_list_instances", "description": "List all running Visual Studio instances"},
      {"name": "vs_connect_instance", "description": "Connect to a specific Visual Studio instance"},
      {"name": "vs_open_solution", "description": "Open a solution in the connected Visual Studio instance"},
      {"name": "vs_build_solution", "description": "Build the currently open solution"},
      {"name": "vs_get_projects", "description": "Get all projects in the currently open solution"}
    ]
  }
}
```

### Step 6: Health Monitoring Validation

**6.1 Test Health Check System**
With MCP server running, monitor logs for health checks:
```
[16:45:53] DEBUG: Starting health check cycle...
[16:45:53] DEBUG: Health check completed successfully
```

**6.2 Test Connection Recovery**
1. Start MCP server
2. Connect to a Visual Studio instance
3. Close Visual Studio
4. Observe automatic cleanup in logs:
```
[16:46:23] DEBUG: Removed dead DTE reference for process 12345
[16:46:23] INFO: Cleaned up disconnected Visual Studio instance
```

---

## Troubleshooting Common Issues

### COM Interop Issues

#### Issue: "Unable to cast COM object of type..."
**Symptoms:**
- COM objects can't be accessed
- DTE interface not available

**Solutions:**
```powershell
# Re-register Visual Studio COM components
& "${env:ProgramFiles}\\Microsoft Visual Studio\\2022\\Professional\\Common7\\IDE\\devenv.exe" /regserver

# Clear COM cache
Remove-Item -Path "HKCU:\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\CurrentVersion\\AppContainer\\Storage\\microsoft.microsoftedge_*" -Recurse -Force -ErrorAction SilentlyContinue
```

#### Issue: "Process cannot be found" errors
**Symptoms:**
- Visual Studio instances not detected
- Connection failures

**Solutions:**
```csharp
// Verify VS is actually running
var processes = Process.GetProcessesByName("devenv");
Console.WriteLine($"Found {processes.Length} Visual Studio processes");
foreach (var proc in processes)
{
    Console.WriteLine($"PID: {proc.Id}, StartTime: {proc.StartTime}");
}
```

### Build Issues

#### Issue: "Target framework not found"
**Symptoms:**
```
error NU1202: Package Microsoft.Extensions.Hosting 8.0.0 is not compatible with net8.0-windows7.0
```

**Solutions:**
```xml
<!-- Verify TargetFramework in project files -->
<PropertyGroup>
  <TargetFramework>net8.0-windows</TargetFramework>
  <UseWindowsForms>true</UseWindowsForms>
</PropertyGroup>
```

#### Issue: "Package restore failed"
**Solutions:**
```bash
# Clear all NuGet caches
dotnet nuget locals all --clear

# Delete bin/obj folders
find . -name "bin" -type d -exec rm -rf {} +
find . -name "obj" -type d -exec rm -rf {} +

# Restore with verbose logging
dotnet restore --verbosity detailed
```

### MCP Server Issues

#### Issue: "MCP Server not responding"
**Diagnostic Steps:**
```bash
# Check if server process is running
ps aux | grep VisualStudioMcp

# Test with simple request
echo '{"method":"tools/list"}' | dotnet run --project src/VisualStudioMcp.Server/

# Check logs for errors
tail -f logs/mcp-server.log
```

#### Issue: "Tools not registered"
**Solutions:**
```csharp
// Verify service registration in Program.cs
services.AddSingleton<VisualStudioMcpServer>();
services.AddSingleton<IVisualStudioService, VisualStudioService>();
// ... other services
```

### Security Validation Issues

#### Issue: "Process ID validation failed"
**Understanding the Error:**
```
Process ID validation failed: INVALID_PROCESS_TYPE - Specified process is not Visual Studio
Process 'notepad' (PID: 12345) is not a Visual Studio instance
```

**Solution:**
Ensure you're targeting actual Visual Studio processes. Use `vs_list_instances` to get valid process IDs:
```json
{"method":"tools/call","params":{"name":"vs_list_instances","arguments":{}}}
```

---

## Development Workflow Best Practices

### Git Workflow
```bash
# Start new feature
git checkout main
git pull origin main
git checkout -b feature/your-feature

# Regular commits
git add .
git commit -m "Implement feature X"

# Before pushing
git rebase main  # Keep history clean
git push -u origin feature/your-feature

# Create pull request via GitHub CLI
gh pr create --title "Add feature X" --body "Description"
```

### Testing Workflow
```bash
# Run unit tests
dotnet test tests/VisualStudioMcp.Core.Tests/

# Run integration tests (requires VS instance)
dotnet test tests/VisualStudioMcp.Integration.Tests/

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

### Debugging Setup
**Visual Studio Configuration:**
1. Set `VisualStudioMcp.Server` as startup project
2. Add command line arguments for testing
3. Set breakpoints in MCP tool handlers
4. Use "Attach to Process" for live debugging

**VS Code Configuration:**
```json
// .vscode/launch.json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Debug MCP Server",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/src/VisualStudioMcp.Server/bin/Debug/net8.0-windows/VisualStudioMcp.Server.exe",
      "args": [],
      "cwd": "${workspaceFolder}",
      "console": "integratedTerminal",
      "stopAtEntry": false
    }
  ]
}
```

---

## Performance Monitoring Setup

### Memory Monitoring
```csharp
// Add to your test setup
using var memoryMonitor = new MemoryMonitor();
memoryMonitor.StartMonitoring();

// Your test code here

var usage = memoryMonitor.GetUsage();
Console.WriteLine($"Peak memory: {usage.PeakMemoryUsage:N0} bytes");
```

### Performance Benchmarks
```bash
# Run performance tests
dotnet run --project tests/VisualStudioMcp.Performance.Tests/ --configuration Release

# Expected benchmarks:
# - Tool response time: < 500ms
# - Memory usage: < 100MB baseline
# - COM object cleanup: < 5 seconds
```

---

## Validation Checklist

### ✅ Prerequisites Validated
- [ ] Visual Studio 2022 (17.8+) with required workloads
- [ ] .NET 8 SDK installed and accessible
- [ ] Windows 10/11 with COM interop support
- [ ] Git configured with proper line endings

### ✅ Build Environment Ready
- [ ] Repository cloned and branch created
- [ ] All packages restored successfully
- [ ] Solution builds with 0 errors
- [ ] All 9 projects compile individually

### ✅ COM Interop Functional
- [ ] Visual Studio instance can be started
- [ ] DTE objects accessible via COM
- [ ] Process enumeration working
- [ ] Connection health monitoring active

### ✅ MCP Server Operational
- [ ] Server starts without errors
- [ ] All 5 tools registered correctly
- [ ] Protocol handshake responds properly
- [ ] Request/response cycle functional

### ✅ Development Workflow Established
- [ ] Git workflow configured
- [ ] Testing framework operational
- [ ] Debugging environment setup
- [ ] Performance monitoring available

---

## Success Criteria

**🎯 Setup Complete When:**
- All checklist items above are validated ✅
- You can successfully run `dotnet build` with 0 errors
- MCP server responds to `tools/list` request within 2 seconds
- COM connection to Visual Studio works reliably
- Health monitoring logs show regular check cycles

**🚀 Ready for Development When:**
- You can create feature branches and commit changes
- Unit tests pass consistently
- Integration tests work with live VS instances
- You understand the troubleshooting procedures above

**⚡ Performance Targets:**
- Build time: < 30 seconds on modern hardware
- MCP server startup: < 5 seconds
- Tool response time: < 500ms average
- Memory usage: < 100MB baseline

---

## Getting Help

### Internal Resources
- **Architecture Documentation:** See `docs/architecture/system-architecture.md`
- **API Reference:** See `docs/api/mcp-tools-reference.md`
- **Planning Document:** See `vs-mcp-planning.md`

### Common Questions
**Q: Why do I need Visual Studio 2022 Professional/Enterprise?**
A: COM interop features required for DTE access are not available in Community edition for automation scenarios.

**Q: Can I develop on macOS or Linux?**
A: No, the project requires Windows-specific COM components and Visual Studio 2022.

**Q: How do I contribute changes?**
A: Follow the Git workflow above and create pull requests. See `CONTRIBUTING.md` for detailed guidelines.

### Support Channels
- **Issues:** Create GitHub issues for bugs or feature requests
- **Discussions:** Use GitHub Discussions for questions
- **Documentation:** Check existing documentation before asking questions

---

*This setup guide ensures developers can quickly establish a productive development environment for the Visual Studio MCP Server project. Follow each step carefully and use the validation checkpoints to confirm successful setup.*