# Claude Code Integration Quick Start
## Visual Studio MCP Server - 10-Minute Success Path

**Last Updated:** 12 August 2025  
**Target Audience:** Claude Code users wanting immediate Visual Studio automation  
**Success Time:** 10 minutes to first successful automation  

---

## 🚀 Quick Success Path (10 Minutes)

### Step 1: Install MCP Server (2 minutes)

**Option A: Pre-built Release (Recommended)**
```bash
# Install as global .NET tool
dotnet tool install -g VisualStudioMcp.Server

# Verify installation
vsmcp --version
```

**Option B: Build from Source**
```bash
# Clone and build
git clone https://github.com/your-org/MCP-VS-AUTOMATION.git
cd MCP-VS-AUTOMATION
dotnet build --configuration Release
```

### Step 2: Configure Claude Code (3 minutes)

**Add MCP Server to Claude Code Configuration:**

Create or edit your Claude Code MCP configuration file:

**Windows:** `%APPDATA%\claude-code\mcp-servers.json`  
**Linux:** `~/.config/claude-code/mcp-servers.json`

```json
{
  "servers": {
    "visual-studio": {
      "command": "vsmcp",
      "args": [],
      "env": {},
      "description": "Visual Studio automation via COM interop"
    }
  }
}
```

### Step 3: Start Visual Studio (1 minute)

```powershell
# Start Visual Studio with any solution
Start-Process "devenv.exe" "C:\path\to\your\solution.sln"
```

### Step 4: Test Integration (4 minutes)

**In Claude Code, test the connection:**

```
Can you list all running Visual Studio instances?
```

**Expected Response:**
```
I can see the following Visual Studio instances:
- Process ID: 12345, Version: 17.8.0
- Solution: C:\YourProject\YourSolution.sln
- Status: Healthy ✅
```

**Try building your solution:**
```
Build the current solution in Debug configuration
```

**Expected Response:**
```
✅ Build completed successfully!
- Configuration: Debug
- Build time: 8.2 seconds
- Warnings: 0
- Errors: 0
- Projects built: 5
```

---

## 🎯 Core MCP Tools Available

### 1. `vs_list_instances` - Discover Visual Studio

**What it does:** Lists all running Visual Studio instances with metadata

**Claude Code usage:**
```
"Show me all Visual Studio instances"
"Which Visual Studio processes are running?"
"List available VS instances with their solutions"
```

**Sample Response:**
```json
{
  "instances": [
    {
      "processId": 15420,
      "version": "17.8.0",
      "solutionPath": "C:\\Dev\\MyProject\\MyProject.sln",
      "isHealthy": true,
      "mainWindowTitle": "MyProject - Microsoft Visual Studio"
    }
  ],
  "count": 1,
  "timestamp": "2025-08-12T16:45:00Z"
}
```

### 2. `vs_connect_instance` - Connect to Specific Visual Studio

**What it does:** Establishes connection to a specific VS instance by process ID

**Claude Code usage:**
```
"Connect to Visual Studio process 15420"
"Connect to the Visual Studio instance running MyProject"
"Use the VS instance with PID 15420"
```

**Sample Response:**
```json
{
  "instance": {
    "processId": 15420,
    "version": "17.8.0",
    "connected": true
  },
  "connected": true,
  "timestamp": "2025-08-12T16:45:00Z"
}
```

### 3. `vs_open_solution` - Open Solutions

**What it does:** Opens a solution file in the connected Visual Studio instance

**Claude Code usage:**
```
"Open the solution at C:\\Dev\\MyProject\\MyProject.sln"
"Load the solution file MyProject.sln"
"Open C:\\Projects\\WebApp\\WebApp.sln in Visual Studio"
```

**Security Features:**
- Path traversal protection (blocks `../` attempts)
- Extension validation (only `.sln` files accepted)
- File existence verification
- Comprehensive error reporting

### 4. `vs_build_solution` - Build Solutions

**What it does:** Builds the currently open solution with specified configuration

**Claude Code usage:**
```
"Build the current solution"
"Build in Release configuration"
"Compile the solution and show any errors"
```

**Sample Response:**
```json
{
  "buildResult": {
    "success": true,
    "configuration": "Debug",
    "duration": "00:00:08.234",
    "projects": 5,
    "errors": [],
    "warnings": [],
    "outputPath": "C:\\Dev\\MyProject\\bin\\Debug"
  },
  "timestamp": "2025-08-12T16:45:30Z"
}
```

### 5. `vs_get_projects` - Enumerate Projects

**What it does:** Lists all projects in the currently open solution

**Claude Code usage:**
```
"Show me all projects in this solution"
"List the projects and their types"
"What projects are in the current solution?"
```

**Sample Response:**
```json
{
  "projects": [
    {
      "name": "MyProject.Core",
      "path": "C:\\Dev\\MyProject\\src\\MyProject.Core\\MyProject.Core.csproj",
      "type": "C# Class Library",
      "targetFramework": "net8.0"
    },
    {
      "name": "MyProject.Web",
      "path": "C:\\Dev\\MyProject\\src\\MyProject.Web\\MyProject.Web.csproj",
      "type": "ASP.NET Core Web Application",
      "targetFramework": "net8.0"
    }
  ],
  "count": 2,
  "timestamp": "2025-08-12T16:45:45Z"
}
```

---

## 🔧 Common Integration Scenarios

### Scenario 1: Daily Development Workflow

**Morning Setup:**
```
Claude: "List all Visual Studio instances and connect to the one running MyProject"
```

**Development Tasks:**
```
Claude: "Build the solution and show me any warnings or errors"
Claude: "Show me all projects in the solution with their target frameworks"
Claude: "Open the WebAPI solution in the second Visual Studio instance"
```

**Before Committing:**
```
Claude: "Build the solution in Release configuration to check for any issues"
```

### Scenario 2: Multi-Solution Management

**When working with multiple solutions:**
```
Claude: "List all Visual Studio instances"
Claude: "Connect to process 15420 (the one with the WebAPI solution)"
Claude: "Build this solution while I work on the other one"
Claude: "Switch to process 18750 (the mobile app solution)"
```

### Scenario 3: Solution Analysis

**Understanding a new codebase:**
```
Claude: "Open the solution at C:\\Projects\\NewProject\\NewProject.sln"
Claude: "Show me all projects in this solution with their types"
Claude: "Build the solution and let me know if there are any errors"
```

### Scenario 4: Automated Build Verification

**Pre-deployment checks:**
```
Claude: "Build the solution in Release configuration"
Claude: "If the build succeeds, show me the output path and built assemblies"
Claude: "Check if there are any warnings I should address"
```

---

## 🛡️ Security and Safety Features

### Input Validation
- **Process ID Security:** Only valid Visual Studio processes can be targeted
- **Path Security:** Protection against directory traversal attacks
- **File Type Validation:** Only `.sln` files can be opened
- **Process Verification:** Confirms target process is actually Visual Studio

### Error Handling
- **Graceful Degradation:** MCP server continues operating if VS crashes
- **Health Monitoring:** Automatic detection and cleanup of disconnected instances
- **Detailed Error Messages:** Clear, actionable error information without security disclosure

### Resource Management
- **Memory Safety:** Weak references prevent COM object memory leaks
- **Connection Cleanup:** Automatic disposal of dead connections
- **Process Monitoring:** Health checks ensure reliable operation

---

## 🔍 Troubleshooting Common Issues

### Issue: "No Visual Studio instances found"

**Symptoms:**
```
Claude responds: "I don't see any running Visual Studio instances."
```

**Solutions:**
1. **Verify Visual Studio is running:**
   ```powershell
   Get-Process devenv
   ```

2. **Check COM registration:**
   ```powershell
   # Re-register Visual Studio COM components
   & "${env:ProgramFiles}\\Microsoft Visual Studio\\2022\\Professional\\Common7\\IDE\\devenv.exe" /regserver
   ```

3. **Restart MCP server:**
   ```bash
   # If running as service, restart it
   vsmcp --restart
   ```

### Issue: "Connection failed to Visual Studio instance"

**Symptoms:**
```
Error: "Failed to connect to Visual Studio instance - Access denied"
```

**Solutions:**
1. **Run with administrator privileges** (if corporate environment)
2. **Check antivirus/firewall** blocking COM access
3. **Verify Visual Studio version compatibility** (requires VS 2022 17.8+)

### Issue: "Build failed with COM errors"

**Symptoms:**
```
Error: "Build operation failed - COM object has been separated from its underlying RCW"
```

**Solutions:**
1. **Restart Visual Studio** to refresh COM objects
2. **Close unnecessary VS instances** to reduce COM pressure
3. **Check available memory** - COM objects need sufficient resources

### Issue: "Path validation failed"

**Symptoms:**
```
Error: "Path contains potential traversal attempts"
```

**Solutions:**
1. **Use absolute paths only:** `C:\\Projects\\MySolution.sln`
2. **Avoid relative references:** Don't use `..\\` or `~\\`
3. **Check file exists:** Ensure the `.sln` file actually exists

### Issue: "MCP server not responding"

**Diagnostic Steps:**
```bash
# Check if MCP server is running
ps aux | grep vsmcp

# Test direct connection
echo '{"method":"tools/list"}' | vsmcp

# Check Claude Code MCP configuration
cat ~/.config/claude-code/mcp-servers.json
```

---

## ⚡ Performance Tips

### Optimise for Speed
- **Keep Visual Studio instances minimal** - Close unnecessary solutions
- **Use specific process IDs** - More efficient than auto-discovery
- **Build incrementally** - Use Debug configuration during development
- **Monitor memory usage** - Restart VS if memory usage exceeds 2GB

### Best Practices
- **One solution per VS instance** for better isolation
- **Regular VS restarts** during long development sessions
- **Use specific commands** rather than asking Claude to figure out what you want
- **Batch operations** when possible (e.g., "build all projects")

---

## 📊 Integration Success Metrics

### ✅ Integration Working When:
- Claude Code responds to VS queries within 3 seconds
- All 5 MCP tools are discoverable and functional
- Build operations complete and report accurate status
- Solution opening works with proper path validation
- Instance discovery shows running VS processes correctly

### 🚀 Optimal Performance Indicators:
- Tool response time: < 1 second average
- Build status reporting: Real-time progress updates
- Error handling: Clear, actionable error messages
- Memory usage: Stable over extended sessions
- Connection reliability: 99%+ uptime during development

---

## 🎓 Advanced Usage Patterns

### Custom Workflows
```
Claude: "Set up my daily workflow: connect to the main VS instance, build the solution, and show me any errors"
```

### Multi-Configuration Builds
```
Claude: "Build the solution in both Debug and Release configurations and compare the results"
```

### Solution Health Checks
```
Claude: "Check the health of all my Visual Studio instances and build status"
```

### Project Analysis
```
Claude: "Analyse the projects in this solution and tell me about their dependencies and target frameworks"
```

---

## 📞 Getting Help

### Quick Diagnostics
```
Claude: "Run a diagnostic check on the Visual Studio MCP integration"
```

This will test:
- MCP server connectivity
- Visual Studio instance discovery
- COM object access
- Tool registration status
- Basic functionality verification

### Documentation Resources
- **Full API Reference:** `docs/api/mcp-tools-reference.md`
- **Development Setup:** `DEVELOPMENT_SETUP.md`
- **Troubleshooting Guide:** `TROUBLESHOOTING.md`
- **Architecture Overview:** `docs/architecture/system-architecture.md`

### Support Channels
- **GitHub Issues:** For bugs and feature requests
- **GitHub Discussions:** For usage questions and tips
- **Documentation Updates:** Submit PRs for documentation improvements

---

## 🎯 Success Checklist

**✅ Ready for Production Use When:**
- [ ] All 5 MCP tools respond correctly to Claude Code queries
- [ ] Visual Studio instances are discovered automatically
- [ ] Solution building works reliably with error reporting
- [ ] Path validation prevents security issues
- [ ] Performance meets targets (< 3 second response times)
- [ ] Error messages are clear and actionable
- [ ] Health monitoring shows stable connections

**🚀 Advanced Integration Achieved When:**
- [ ] Custom workflows integrated into daily development routine
- [ ] Multi-solution management working smoothly
- [ ] Automated build verification integrated with deployment process
- [ ] Performance optimised for your specific development environment
- [ ] Troubleshooting procedures are familiar and effective

---

*This integration guide provides the fastest path to productive Visual Studio automation with Claude Code. Follow the 10-minute success path for immediate results, then explore advanced scenarios to maximise your development productivity.*