# Development Setup Guide

This guide walks you through setting up your development environment for the Visual Studio MCP Server project.

## System Requirements

### Required Software
- **Windows 10/11** - Visual Studio COM interop requires Windows
- **Visual Studio 2022** (17.8 or later) with the following workloads:
  - .NET desktop development
  - Visual Studio extension development (optional, for advanced scenarios)
- **.NET 8 SDK** (8.0.100 or later)
- **Git** with proper Windows line endings configured

### Recommended Tools
- **Windows Terminal** or **PowerShell** for command line operations
- **VS Code** for markdown editing and cross-project work
- **GitKraken** or similar Git GUI (optional)

## Environment Setup

### 1. Install Visual Studio 2022

Download and install [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) with these workloads:

**Required Workloads:**
- ✅ .NET desktop development
- ✅ .NET Multi-platform App UI development (for XAML support)

**Optional but Recommended:**
- 📋 Visual Studio extension development
- 🔧 Git for Windows

### 2. Install .NET 8 SDK

```powershell
# Check if .NET 8 is already installed
dotnet --list-sdks

# If .NET 8.x is not listed, download from:
# https://dotnet.microsoft.com/download/dotnet/8.0
```

### 3. Configure Git for Windows

```bash
# Configure line endings for cross-platform compatibility
git config --global core.autocrlf true

# Set up your Git identity
git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"
```

### 4. Clone the Repository

```bash
# Clone the repository
git clone https://github.com/your-org/MCP-VS-AUTOMATION
cd MCP-VS-AUTOMATION

# Switch to develop branch if it exists
git checkout develop
```

## Build Verification

### 1. Restore Dependencies

```powershell
# From the repository root
dotnet restore
```

### 2. Build the Solution

```powershell
# Build all projects
dotnet build

# Build in Release mode
dotnet build --configuration Release
```

### 3. Run Tests

```powershell
# Run all unit tests
dotnet test

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### 4. Run the Application

```powershell
# Run the MCP server from source
cd src/VisualStudioMcp.Server
dotnet run

# Or build and run
dotnet build
dotnet run --project src/VisualStudioMcp.Server
```

## Development Environment Configuration

### Visual Studio Settings

**Recommended Visual Studio Extensions:**
- CodeMaid - Code cleanup and organisation
- SonarLint - Code quality analysis
- GitLens - Enhanced Git integration (if using VS Code)

**Visual Studio Configuration:**
```json
// .vscode/settings.json (for VS Code usage)
{
    "dotnet.defaultSolution": "VisualStudioMcp.sln",
    "omnisharp.enabled": true,
    "dotnet.completion.showCompletionItemsFromUnimportedNamespaces": true
}
```

### Code Quality Tools

**EditorConfig (.editorconfig)** - Already configured in the repository:
```ini
root = true

[*.cs]
charset = utf-8
indent_style = space
indent_size = 4
insert_final_newline = true
trim_trailing_whitespace = true
```

## Project Structure Understanding

### Solution Layout
```
VisualStudioMcp.sln                 # Main solution file
├── src/                            # Source code projects
│   ├── VisualStudioMcp.Server/     # Console application entry point
│   ├── VisualStudioMcp.Core/       # Core VS automation services  
│   ├── VisualStudioMcp.Debug/      # Debugging automation services
│   ├── VisualStudioMcp.Xaml/       # XAML designer automation
│   ├── VisualStudioMcp.Imaging/    # Screenshot and visual capture
│   └── VisualStudioMcp.Shared/     # Common models and interfaces
├── tests/                          # Test projects
│   ├── VisualStudioMcp.Core.Tests/
│   ├── VisualStudioMcp.Debug.Tests/
│   └── VisualStudioMcp.Integration.Tests/
├── docs/                           # Documentation
└── tools/                          # Build and development tools
```

### Key Configuration Files
- **`Directory.Build.props`** - Shared MSBuild properties
- **`Directory.Packages.props`** - Central package management  
- **`global.json`** - .NET SDK version specification
- **`.gitignore`** - Git ignore patterns
- **`.editorconfig`** - Code style configuration

## WSL Development Notes

**Important:** If you're using WSL (Windows Subsystem for Linux), note these limitations:

- **Cannot run .NET commands directly** - WSL cannot execute Windows .NET applications
- **Use Claude Code MCP tools instead** - Available `mcp__code-tools__dotnet_*` tools
- **File path conversions** - Use Windows paths (`D:\...`) for MCP tool calls
- **Git operations work normally** - Can use standard Git commands in WSL

**Example WSL workflow:**
```bash
# In WSL - Git operations work fine
git checkout -b feature/new-feature
git add .
git commit -m "Add new feature"

# For .NET operations, use MCP tools instead of direct commands
# Instead of: dotnet build
# Use Claude Code with: mcp__code-tools__dotnet_check_compilation
```

## Troubleshooting Common Setup Issues

### Issue: Visual Studio 2022 Not Found
**Problem:** Build tools cannot locate Visual Studio installation

**Solution:**
```powershell
# Check Visual Studio installation
"${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" -version
# Or for Professional/Community editions, adjust path accordingly

# Verify .NET workload is installed
dotnet --info
```

### Issue: COM Interop Exceptions  
**Problem:** Runtime errors when connecting to Visual Studio

**Solution:**
1. Ensure Visual Studio is running
2. Run development environment as Administrator (if needed)
3. Check Windows User Account Control (UAC) settings
4. Verify EnvDTE references are properly installed

### Issue: Build Errors with Package References
**Problem:** NuGet packages not restoring correctly

**Solution:**
```powershell
# Clear NuGet caches
dotnet nuget locals all --clear

# Restore packages explicitly
dotnet restore --force

# Check NuGet configuration
dotnet nuget list source
```

### Issue: Test Discovery Problems
**Problem:** Tests not appearing in Test Explorer

**Solution:**
```powershell
# Rebuild test projects
dotnet clean
dotnet build tests/

# Run test discovery
dotnet test --list-tests
```

## Development Workflow

### 1. Feature Development Process
```bash
# Create feature branch
git checkout -b feature/vs-debugging-control

# Make changes, commit frequently
git add .
git commit -m "Add debugging session management"

# Push and create PR
git push -u origin feature/vs-debugging-control
# Create PR through GitHub CLI or web interface
```

### 2. Testing Workflow
```powershell
# Before committing - run all tests
dotnet test

# Run specific test category
dotnet test --filter Category=Unit

# Run with coverage (if configured)
dotnet test --collect:"XPlat Code Coverage"
```

### 3. Documentation Updates
- Update relevant `.md` files when adding features
- Test all code examples in documentation
- Update API documentation for new MCP tools
- Review documentation during code review process

## Performance Considerations

### Development Machine Specs
- **Minimum:** 8GB RAM, Core i5 processor  
- **Recommended:** 16GB+ RAM, Core i7/Ryzen 7 processor
- **Storage:** SSD recommended for faster builds

### Build Optimization
```xml
<!-- In Directory.Build.props for faster builds -->
<PropertyGroup>
  <BuildInParallel>true</BuildInParallel>
  <UseSharedCompilation>true</UseSharedCompilation>
</PropertyGroup>
```

## Security Considerations

### Development Environment
- Run Visual Studio with appropriate permissions for COM interop
- Keep Windows and .NET SDK updated
- Use Git credential managers for secure authentication

### Code Security  
- All COM objects must be properly disposed
- Input validation required for all MCP tool parameters
- No sensitive information in logs or error messages

## Getting Help

### Internal Resources
- [System Architecture](../architecture/system-architecture.md) - Understanding the codebase
- [COM Interop Patterns](../architecture/com-interop-patterns.md) - Safe COM programming
- [Coding Standards](coding-standards.md) - Code quality guidelines

### External Resources
- [.NET 8 Documentation](https://docs.microsoft.com/dotnet/)
- [Visual Studio Extensibility](https://docs.microsoft.com/visualstudio/extensibility/)
- [EnvDTE Reference](https://docs.microsoft.com/dotnet/api/envdte)

---

**Next Steps:** Once your environment is set up, review the [System Architecture](../architecture/system-architecture.md) to understand how the components work together.