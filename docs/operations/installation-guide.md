# Visual Studio MCP Server - Installation Guide

This guide covers installing and configuring the Visual Studio MCP Server as a global .NET tool for use with Claude Code.

## System Requirements

### Prerequisites
- **Windows 10/11** - Required for Visual Studio COM interop
- **Visual Studio 2022** (17.8 or later) - Any edition (Community, Professional, Enterprise)
- **.NET 8 Runtime** (8.0.100 or later) - Will be installed automatically if missing
- **Claude Code** - Available from [claude.ai/code](https://claude.ai/code)

### Supported Visual Studio Editions
✅ **Visual Studio 2022 Community** (Free)  
✅ **Visual Studio 2022 Professional**  
✅ **Visual Studio 2022 Enterprise**  
❌ **Visual Studio Code** (Not supported - requires full Visual Studio)  
❌ **Visual Studio 2019** (Not supported - requires VS 2022)  

## Installation Methods

### Method 1: Install from NuGet (Recommended)

```powershell
# Install the global tool
dotnet tool install --global VisualStudioMcp

# Verify installation
vsmcp --version
```

### Method 2: Install from Local Build

```powershell
# Clone and build from source
git clone https://github.com/automint/visual-studio-mcp-server
cd visual-studio-mcp-server

# Build and pack
dotnet pack src/VisualStudioMcp.Server/VisualStudioMcp.Server.csproj -c Release

# Install locally
dotnet tool install --global --add-source ./src/VisualStudioMcp.Server/nupkg VisualStudioMcp
```

### Method 3: Install Specific Version

```powershell
# Install specific version
dotnet tool install --global VisualStudioMcp --version 1.2.0

# Update to latest version
dotnet tool update --global VisualStudioMcp
```

## Claude Code Integration

### 1. Configure MCP Server

Create or update your Claude Code MCP configuration file:

**Location:** `%APPDATA%\Claude\mcp_servers.json` (Windows)

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

### 2. Alternative Configuration (Advanced)

For custom configurations or multiple instances:

```json
{
  "mcpServers": {
    "visual-studio-dev": {
      "command": "vsmcp",
      "args": ["--debug", "--timeout", "10000"],
      "env": {
        "VSMCP_LOG_LEVEL": "Debug",
        "VSMCP_COM_TIMEOUT": "15000"
      }
    },
    "visual-studio-prod": {
      "command": "vsmcp", 
      "args": ["--timeout", "5000"],
      "env": {
        "VSMCP_LOG_LEVEL": "Information"
      }
    }
  }
}
```

### 3. Verify Claude Code Integration

1. **Start Claude Code**
2. **Open a terminal** in Claude Code
3. **Test the connection:**
   ```
   List all running Visual Studio instances
   ```
4. **Expected response:** Claude Code should show available VS instances or indicate none are running

## Configuration Options

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `VSMCP_LOG_LEVEL` | `Information` | Logging verbosity: `Trace`, `Debug`, `Information`, `Warning`, `Error` |
| `VSMCP_COM_TIMEOUT` | `5000` | COM operation timeout in milliseconds |
| `VSMCP_CAPTURE_PATH` | `%TEMP%` | Directory for screenshot captures |
| `VSMCP_MAX_INSTANCES` | `10` | Maximum VS instances to track |

### Command Line Arguments

```powershell
# Run with debug logging
vsmcp --debug

# Set custom timeout
vsmcp --timeout 10000

# Show help
vsmcp --help

# Show version information
vsmcp --version
```

## Verification & Testing

### 1. Basic Installation Test

```powershell
# Check tool is installed
dotnet tool list --global

# Should show:
# Package Id         Version      Commands
# VisualStudioMcp    1.0.0        vsmcp

# Test tool execution
vsmcp --version
```

### 2. Visual Studio Integration Test

1. **Open Visual Studio 2022**
2. **Open any solution** (or create a new one)
3. **Run verification command:**
   ```powershell
   vsmcp --test-connection
   ```
4. **Expected output:**
   ```
   ✅ Visual Studio MCP Server v1.0.0
   ✅ Found 1 Visual Studio instance(s)
   ✅ COM interop working correctly
   ✅ Ready for Claude Code integration
   ```

### 3. Claude Code Integration Test

1. **Open Claude Code**
2. **Create a new conversation**
3. **Test with this prompt:**
   ```
   List all running Visual Studio instances and show me their current solution files
   ```
4. **Expected behavior:** Claude Code should communicate with the MCP server and return VS instance information

## Troubleshooting

### Common Installation Issues

#### Issue: `dotnet tool install` fails
```
error NU1103: Unable to find a stable package VisualStudioMcp
```

**Solutions:**
1. **Check .NET version:**
   ```powershell
   dotnet --version
   # Should show 8.0.x or later
   ```

2. **Update .NET:**
   ```powershell
   # Download from https://dotnet.microsoft.com/download/dotnet/8.0
   ```

3. **Clear NuGet cache:**
   ```powershell
   dotnet nuget locals all --clear
   dotnet tool install --global VisualStudioMcp
   ```

#### Issue: `vsmcp` command not found
```
'vsmcp' is not recognized as an internal or external command
```

**Solutions:**
1. **Check PATH environment:**
   ```powershell
   echo $env:PATH
   # Should include: C:\Users\[username]\.dotnet\tools
   ```

2. **Restart terminal/PowerShell**

3. **Manually add to PATH:**
   ```powershell
   $env:PATH += ";$env:USERPROFILE\.dotnet\tools"
   ```

#### Issue: COM interop failures
```
Error: Unable to connect to Visual Studio instances
System.Runtime.InteropServices.COMException
```

**Solutions:**
1. **Run as Administrator:**
   ```powershell
   # Run PowerShell as Administrator
   vsmcp --test-connection
   ```

2. **Check Visual Studio is running:**
   - Open Visual Studio 2022
   - Open a solution or project
   - Try connection test again

3. **Verify EnvDTE registration:**
   ```powershell
   # Re-register Visual Studio COM components
   # Run as Administrator:
   cd "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE"
   devenv.exe /regserver
   ```

### Claude Code Integration Issues

#### Issue: MCP server not starting
**Check Claude Code configuration:**
1. Verify `mcp_servers.json` syntax is correct
2. Restart Claude Code completely
3. Check Claude Code logs for error messages

#### Issue: Commands not working in Claude Code
**Debugging steps:**
1. **Test tool directly:**
   ```powershell
   vsmcp --test-connection
   ```

2. **Check Claude Code MCP status:**
   - Look for MCP server status in Claude Code interface
   - Verify server is listed as "connected"

3. **Try simpler commands first:**
   ```
   List Visual Studio instances
   ```

### Performance Issues

#### Issue: Slow response times
**Optimization strategies:**
1. **Increase timeout:**
   ```json
   {
     "command": "vsmcp",
     "args": ["--timeout", "15000"]
   }
   ```

2. **Reduce VS instance count:**
   - Close unnecessary Visual Studio instances
   - Use `vs_connect_instance` to target specific instances

3. **Check system resources:**
   - Monitor CPU and memory usage
   - Ensure adequate RAM (16GB+ recommended)

## Updating & Maintenance

### Update to Latest Version

```powershell
# Check current version
vsmcp --version

# Update to latest
dotnet tool update --global VisualStudioMcp

# Verify update
vsmcp --version
```

### Uninstallation

```powershell
# Remove the global tool
dotnet tool uninstall --global VisualStudioMcp

# Clean up configuration (optional)
Remove-Item "$env:APPDATA\Claude\mcp_servers.json" -Force
```

### Maintenance Tasks

**Monthly:**
- Update to latest version
- Clear temporary capture files: `%TEMP%\vsmcp_*`
- Review Claude Code integration logs

**As Needed:**
- Clear .NET tool cache if experiencing issues
- Update Visual Studio to latest version
- Review and update MCP server configuration

## Security Considerations

### Permissions Required
- **COM Interop:** Access to Visual Studio COM objects
- **File System:** Read project/solution files, write temporary captures
- **Process Access:** Monitor Visual Studio processes

### Security Best Practices
1. **Run with standard user permissions** when possible
2. **Only use Administrator mode** if COM registration issues occur
3. **Keep software updated:** VS 2022, .NET 8, and MCP server
4. **Review captured screenshots** before sharing (may contain sensitive code)

### Firewall/Antivirus
- **MCP server runs locally** - no network access required
- **Some antivirus software** may flag COM interop - add exception if needed
- **Corporate environments** may require IT approval for global tool installation

## Getting Help

### Support Resources
- **Documentation:** [Complete API Reference](../api/mcp-tools-reference.md)
- **Troubleshooting:** [Troubleshooting Guide](troubleshooting-guide.md)
- **Issues:** [GitHub Issues](https://github.com/automint/visual-studio-mcp-server/issues)

### Diagnostic Information
When reporting issues, include this diagnostic information:

```powershell
# System information
vsmcp --version
dotnet --info
Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\VisualStudio\*" | Select-Object PSChildName

# Test output
vsmcp --test-connection --verbose

# Claude Code MCP configuration
Get-Content "$env:APPDATA\Claude\mcp_servers.json"
```

### Community & Support
- **GitHub Discussions** for questions and feature requests
- **Documentation Updates** welcome via pull requests
- **Feature Requests** through GitHub Issues with enhancement label

---

**Next:** Review [Usage Examples](../user-guides/claude-code-integration.md) to learn how to effectively use the MCP server with Claude Code.