# Troubleshooting Matrix

**Comprehensive Error Resolution Guide** - Systematic diagnosis and resolution for Visual Studio MCP Server issues.

## 🎯 Quick Diagnosis

Use this table to quickly identify your issue category:

| Symptom | Category | Jump To |
|---------|----------|---------|
| `vsmcp: command not found` | [Installation](#installation-issues) | [INS-001](#ins-001-global-tool-not-found) |
| Claude Code connection errors | [MCP Connection](#mcp-connection-issues) | [MCP-001](#mcp-001-connection-failed) |
| "No Visual Studio instances found" | [Visual Studio Detection](#visual-studio-detection-issues) | [VS-001](#vs-001-no-instances-found) |
| COM errors or access denied | [COM Interop](#com-interop-issues) | [COM-001](#com-001-access-denied) |
| Build/debugging commands fail | [Visual Studio Operations](#visual-studio-operations) | [VSO-001](#vso-001-build-failures) |
| Screenshot/capture failures | [Visual Capture](#visual-capture-issues) | [CAP-001](#cap-001-screenshot-failures) |
| High memory usage or crashes | [Performance](#performance-issues) | [PERF-001](#perf-001-memory-issues) |

## Installation Issues

### INS-001: Global Tool Not Found

**Error Messages:**
```
vsmcp: command not found
'vsmcp' is not recognized as an internal or external command
```

**Root Causes:**
- Global tool installation failed
- PATH environment variable not updated
- .NET SDK version incompatibility

**Diagnostic Commands:**
```bash
# Check .NET version
dotnet --version

# Check global tools
dotnet tool list --global

# Check PATH
echo $PATH  # Linux/WSL
echo %PATH%  # Windows
```

**Resolution Steps:**

1. **Verify .NET 8 SDK:**
   ```bash
   dotnet --version
   # Should show 8.x.x
   ```

2. **Reinstall global tool:**
   ```bash
   dotnet tool uninstall --global VisualStudioMcp
   dotnet tool install --global VisualStudioMcp
   ```

3. **Manual PATH addition (if needed):**
   - Windows: Add `%USERPROFILE%\.dotnet\tools` to PATH
   - Linux/WSL: Add `~/.dotnet/tools` to PATH

4. **Alternative local installation:**
   ```bash
   dotnet tool install --tool-path ./tools VisualStudioMcp
   ./tools/vsmcp --version
   ```

**Verification:**
```bash
vsmcp --version
# Should show: VisualStudioMcp 1.0.0
```

### INS-002: Permission Denied During Installation

**Error Messages:**
```
Access to the path 'C:\Users\...' is denied
Insufficient privileges for installation
```

**Resolution:**
1. Run Command Prompt/PowerShell as Administrator
2. Alternative: Use `--tool-path` for user-local installation
3. Check antivirus software blocking installation

## MCP Connection Issues

### MCP-001: Connection Failed

**Error Messages:**
```
Failed to connect to MCP server 'visual-studio'
Connection timeout to visual-studio
MCP server process exited unexpectedly
```

**Diagnostic Commands:**
```bash
# Test MCP server manually
vsmcp --version

# Check Claude Code logs
# Location: %APPDATA%\Claude\logs\
```

**Resolution Steps:**

1. **Verify MCP configuration:**
   ```json
   {
     "mcpServers": {
       "visual-studio": {
         "command": "vsmcp",
         "args": [],
         "env": {}
       }
     }
   }
   ```

2. **Test manual execution:**
   ```bash
   vsmcp --version
   ```

3. **Check firewall/antivirus:**
   - Add `vsmcp.exe` to antivirus exclusions
   - Verify Windows Firewall not blocking

4. **Restart Claude Code:**
   - Completely close Claude Code
   - Restart and test connection

### MCP-002: JSON Configuration Errors

**Error Messages:**
```
Invalid JSON in mcp_servers.json
Failed to parse MCP configuration
```

**Resolution:**
1. Validate JSON syntax using [jsonlint.com](https://jsonlint.com)
2. Check for trailing commas or missing quotes
3. Backup and recreate configuration file

## Visual Studio Detection Issues

### VS-001: No Instances Found

**Error Messages:**
```
No Visual Studio instances are currently running
Could not connect to Visual Studio
Visual Studio COM interface not available
```

**Diagnostic Commands:**
```bash
# Test VS detection manually
vsmcp

# Check running processes
tasklist /fi "imagename eq devenv.exe"
```

**Resolution Steps:**

1. **Verify Visual Studio is running:**
   - Open Visual Studio 2022 (not VS Code)
   - Load a solution (.sln file)
   - Ensure not in "Safe Mode"

2. **Check Visual Studio version:**
   - Requires Visual Studio 2022 (17.8+)
   - Professional, Enterprise, or Community editions

3. **COM registration issues:**
   ```bash
   # Run as Administrator
   regsvr32 "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe" /regserver
   ```

4. **Restart Visual Studio:**
   - Close all VS instances
   - Run as Administrator
   - Open solution before testing MCP

### VS-002: Multiple Instances Confusion

**Error Messages:**
```
Multiple Visual Studio instances found
Ambiguous instance selection
```

**Resolution:**
1. Close unnecessary Visual Studio instances
2. Use specific instance targeting in future updates
3. Ensure only one solution open per instance

## COM Interop Issues

### COM-001: Access Denied

**Error Messages:**
```
Access denied (Exception from HRESULT: 0x80070005)
COM object access is denied
Insufficient permissions for COM operations
```

**Resolution Steps:**

1. **Run Visual Studio as Administrator:**
   - Right-click VS shortcut → "Run as administrator"
   - Open solution after VS fully loads

2. **Enable COM security:**
   - Run `dcomcnfg.exe` as Administrator
   - Configure DCOM permissions for Visual Studio

3. **Check user permissions:**
   - Ensure user is in local "Developers" group
   - Verify not restricted by group policy

### COM-002: COM Object Release Errors

**Error Messages:**
```
COM object that has been separated from its underlying RCW
RCW cannot be used after the object is finalized
```

**Resolution:**
1. Restart Visual Studio MCP Server process
2. Restart Visual Studio application
3. Update to latest version (includes better COM lifecycle management)

## Visual Studio Operations

### VSO-001: Build Failures

**Error Messages:**
```
Build operation failed
MSBuild execution failed
Project load errors
```

**Diagnostic Steps:**

1. **Test manual build:**
   - Build solution manually in Visual Studio
   - Check Output window for errors

2. **Verify project configuration:**
   - Ensure solution is properly loaded
   - Check for missing dependencies
   - Verify .NET target framework

**Resolution:**
1. Clean and rebuild solution manually first
2. Check for project file corruption
3. Ensure all NuGet packages restored

### VSO-002: Debugging Session Failures

**Error Messages:**
```
Could not start debugging session
Debugger attachment failed
Debug target not available
```

**Resolution:**
1. Ensure project builds successfully
2. Check debug configuration (Debug vs Release)
3. Verify startup project is set
4. Close existing debug sessions

## Visual Capture Issues

### CAP-001: Screenshot Failures

**Error Messages:**
```
Screenshot capture failed
Window not found for capture
Graphics device error
```

**Diagnostic Commands:**
```bash
# Test with simple capture
# In Claude Code: "Take a screenshot of Visual Studio"
```

**Resolution Steps:**

1. **Check Visual Studio window state:**
   - Ensure VS window is visible (not minimized)
   - Bring VS to foreground
   - Check for multiple monitors

2. **Graphics driver issues:**
   - Update graphics drivers
   - Check for hardware acceleration conflicts
   - Test with single monitor setup

3. **Permissions:**
   - Run Visual Studio as Administrator
   - Check antivirus screenshot blocking

### CAP-002: XAML Designer Capture Issues

**Error Messages:**
```
XAML designer window not found
Designer surface not available
```

**Resolution:**
1. Ensure XAML file is open in designer view
2. Switch to "Design" or "Split" view mode
3. Verify XAML workload installed in Visual Studio

## Performance Issues

### PERF-001: Memory Issues

**Symptoms:**
- High memory usage by `vsmcp.exe`
- System slowdown during operations
- Out of memory errors

**Diagnostic Commands:**
```bash
# Monitor memory usage
tasklist /fi "imagename eq vsmcp.exe" /fo table

# Check process details
wmic process where name="vsmcp.exe" get ProcessId,PageFileUsage,WorkingSetSize
```

**Resolution:**
1. Restart Visual Studio MCP Server
2. Close unnecessary Visual Studio instances
3. Check for COM object leaks (update to latest version)

### PERF-002: Slow Response Times

**Symptoms:**
- Commands take >10 seconds to complete
- Timeouts in Claude Code
- Delayed responses

**Resolution:**
1. Check system resources (CPU, Memory, Disk)
2. Close other resource-intensive applications
3. Restart Visual Studio and MCP server
4. Check for Windows Updates

## Advanced Diagnostics

### Enable Debug Logging

1. **Set environment variable:**
   ```bash
   # Windows
   set VSMCP_LOG_LEVEL=Debug

   # In mcp_servers.json
   {
     "mcpServers": {
       "visual-studio": {
         "command": "vsmcp",
         "args": [],
         "env": {
           "VSMCP_LOG_LEVEL": "Debug"
         }
       }
     }
   }
   ```

2. **Locate log files:**
   - Windows: `%TEMP%\VisualStudioMcp\logs\`
   - Check Claude Code logs: `%APPDATA%\Claude\logs\`

### System Information Collection

For support requests, collect:

```bash
# System info
systeminfo | findstr /C:"OS Name" /C:"OS Version" /C:"Total Physical Memory"

# .NET info
dotnet --info

# Visual Studio info
reg query "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\14.0\Setup\VS" /s

# Tool version
vsmcp --version

# Running processes
tasklist /fi "imagename eq devenv.exe"
tasklist /fi "imagename eq vsmcp.exe"
```

## Emergency Recovery

### Complete Reset Procedure

If all else fails, perform a complete reset:

1. **Uninstall global tool:**
   ```bash
   dotnet tool uninstall --global VisualStudioMcp
   ```

2. **Clear configuration:**
   - Remove MCP server entry from `mcp_servers.json`
   - Clear any environment variables

3. **Restart everything:**
   - Close Claude Code completely
   - Close all Visual Studio instances
   - Restart Windows (if necessary)

4. **Clean reinstall:**
   ```bash
   dotnet tool install --global VisualStudioMcp
   ```

5. **Reconfigure Claude Code:**
   - Add MCP server configuration
   - Restart Claude Code
   - Test with simple command

## Support Escalation

### When to Escalate

Escalate to GitHub Issues when:
- Following this guide doesn't resolve the issue
- Error persists after complete reset
- New error patterns not covered here
- System-specific configuration problems

### Information to Include

When creating a support issue:

1. **Error Details:**
   - Exact error messages
   - When the error occurs
   - Steps to reproduce

2. **System Information:**
   - Windows version
   - Visual Studio edition and version
   - .NET SDK version
   - Claude Code version

3. **Configuration:**
   - `mcp_servers.json` content
   - Environment variables
   - Any customizations

4. **Diagnostic Output:**
   - Debug logs
   - System information
   - Process information

### GitHub Issue Template

```markdown
**Error Description:**
[Describe the issue clearly]

**Error Messages:**
[Paste exact error messages]

**System Information:**
- OS: Windows 11 Pro
- Visual Studio: 2022 Professional 17.8.3
- .NET SDK: 8.0.100
- Claude Code: [version]

**Steps to Reproduce:**
1. [Step 1]
2. [Step 2]
3. [Error occurs]

**Diagnostic Information:**
[Paste output from diagnostic commands]

**Troubleshooting Attempted:**
[List what you've tried from this guide]
```

---

**📞 Quick Reference:**
- **Installation Issues:** [INS-001](#ins-001-global-tool-not-found), [INS-002](#ins-002-permission-denied-during-installation)
- **Connection Problems:** [MCP-001](#mcp-001-connection-failed), [MCP-002](#mcp-002-json-configuration-errors)
- **Visual Studio Issues:** [VS-001](#vs-001-no-instances-found), [COM-001](#com-001-access-denied)
- **Performance Problems:** [PERF-001](#perf-001-memory-issues), [PERF-002](#perf-002-slow-response-times)

**🆘 Emergency:** If you need immediate help, try the [Complete Reset Procedure](#complete-reset-procedure) first.