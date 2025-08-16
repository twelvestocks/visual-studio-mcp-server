# Getting Started with Visual Studio MCP Server

**5-Minute Setup Guide** - Get your Visual Studio automation running with Claude Code in under 10 minutes.

## 🎯 What You'll Achieve

By the end of this guide, you'll be able to:
- Install the Visual Studio MCP Server as a global tool
- Connect Claude Code to Visual Studio 
- Execute your first automation (list VS instances and build a solution)
- Troubleshoot common setup issues

## ✅ Prerequisites Checklist

Before starting, ensure you have:

- [ ] **Windows 10/11** (Visual Studio COM dependency)
- [ ] **Visual Studio 2022** (17.8 or later) with .NET desktop development workload
- [ ] **.NET 8 SDK** (8.0.100 or later) - [Download here](https://dotnet.microsoft.com/download/dotnet/8.0)
- [ ] **Claude Code** - [Install from claude.ai/code](https://claude.ai/code)
- [ ] **Administrator privileges** (for global tool installation)

### Quick Verification Commands
```bash
# Verify .NET 8 SDK
dotnet --version
# Should show 8.0.x

# Verify Visual Studio is accessible
# Open Visual Studio 2022 and create/open any solution
```

## 🚀 5-Minute Installation

### Step 1: Install the Global Tool (2 minutes)

Open **Command Prompt** or **PowerShell** as Administrator:

```bash
# Install the Visual Studio MCP Server
dotnet tool install --global VisualStudioMcp

# Verify installation
vsmcp --version
```

**Expected Output:**
```
VisualStudioMcp 1.0.0
```

### Step 2: Configure Claude Code (2 minutes)

1. **Locate your Claude Code configuration:**
   - Windows: `%APPDATA%\Claude\mcp_servers.json`
   - Or use Claude Code command: `/settings` → MCP Settings

2. **Add the Visual Studio MCP Server:**

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

3. **Restart Claude Code** to load the new MCP server

### Step 3: Test Your Setup (1 minute)

1. **Open Visual Studio 2022** with any solution loaded
2. **Start a new Claude Code session**
3. **Test the connection:**

```
List my Visual Studio instances
```

**Expected Response:**
```
I can see 1 Visual Studio instance running:
- Visual Studio 2022 (PID: 12345)
  Solution: YourSolution.sln
  Status: Ready
```

## 🎉 Your First Automation

Now try building your solution through Claude Code:

```
Build my current solution and show me any errors
```

**Success!** You're now ready to use all 17 MCP tools for Visual Studio automation.

## 🔧 Common Setup Issues & Quick Fixes

### Issue: "vsmcp: command not found"

**Cause:** Global tool not in PATH or installation failed

**Quick Fix:**
```bash
# Check if tool is installed
dotnet tool list --global

# If missing, reinstall
dotnet tool install --global VisualStudioMcp

# If still not working, add to PATH manually
# Add to PATH: %USERPROFILE%\.dotnet\tools
```

### Issue: Claude Code shows "Connection failed to visual-studio"

**Cause:** MCP server configuration issue or process failure

**Quick Fix:**
1. Check `mcp_servers.json` syntax (valid JSON)
2. Restart Claude Code completely
3. Verify Visual Studio is running
4. Test manually: `vsmcp --version` in terminal

### Issue: "No Visual Studio instances found"

**Cause:** Visual Studio not running or COM registration issue

**Quick Fix:**
1. Ensure Visual Studio 2022 is running with a solution loaded
2. Run Visual Studio as Administrator
3. Restart Visual Studio if it was opened before installing the tool

### Issue: Permission errors during installation

**Cause:** Insufficient privileges for global tool installation

**Quick Fix:**
1. Run Command Prompt/PowerShell as Administrator
2. Alternative: Install without `--global` flag for user-only installation

## 🎓 Next Steps

### Essential Documentation
- **[Complete API Reference](api/mcp-tools-reference.md)** - All 17 MCP tools explained
- **[Troubleshooting Guide](operations/troubleshooting-matrix.md)** - Comprehensive error resolution
- **[Claude Code Integration](user-guides/claude-code-integration.md)** - Advanced workflows

### Try These Common Workflows

1. **Debugging Session Management:**
   ```
   Start a debugging session for my current project
   Set a breakpoint in Program.cs at line 15
   Show me the current call stack
   ```

2. **Visual Capture:**
   ```
   Take a screenshot of my XAML designer
   Capture the full Visual Studio IDE
   ```

3. **Build Automation:**
   ```
   Build my solution and show me any warnings
   Get information about all projects in my solution
   ```

## 🆘 Need Help?

- **Quick Issues:** Check [Troubleshooting Matrix](operations/troubleshooting-matrix.md)
- **Performance Issues:** See [Performance Benchmarks](operations/performance-benchmarks.md)
- **Feature Requests:** [Create GitHub Issue](https://github.com/twelvestocks/visual-studio-mcp-server/issues)
- **Community Support:** [GitHub Discussions](https://github.com/twelvestocks/visual-studio-mcp-server/discussions)

## 🔍 Verification Checklist

Before moving to advanced usage, verify:

- [ ] `vsmcp --version` works from command line
- [ ] Claude Code can list Visual Studio instances
- [ ] You can build a solution through Claude Code
- [ ] Screenshots/captures work properly
- [ ] No error messages in Claude Code logs

**Estimated Setup Time:** 5-10 minutes for most users

---

**🎯 Success!** You're now ready to leverage the full power of Visual Studio automation through Claude Code. The Visual Studio MCP Server provides 17 specialized tools for debugging, building, visual capture, and XAML design automation.

**Pro Tip:** Start with simple commands like listing instances and building solutions, then gradually explore advanced features like debugging session control and visual state analysis.