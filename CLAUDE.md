# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview
**Visual Studio MCP Server** - A C# .NET 8 console application that provides Claude Code with comprehensive Visual Studio automation capabilities including debugging control, XAML designer interaction, and visual context capture through COM interop.

## Project Management Protocol
**ALWAYS follow this workflow:**
- Always read vs-mcp-planning.md at the start of every new conversation
- Check vs_mcp_tasks_md.md before starting your work
- Mark completed tasks immediately
- Add newly discovered tasks

## Essential Commands

### Development Commands (via MCP Tools)
**IMPORTANT:** You are running in WSL and cannot execute .NET commands directly. Use MCP tools instead:

```bash
# Check project compilation
mcp__code-tools__dotnet_check_compilation --project-path "D:\source\repos\MCP-VS-AUTOMATION\src\VisualStudioMcp.Server\VisualStudioMcp.Server.csproj"

# Analyze project structure
mcp__code-tools__dotnet_analyze_project --project-path "D:\source\repos\MCP-VS-AUTOMATION\src\VisualStudioMcp.Server\VisualStudioMcp.Server.csproj"

# Find all projects in solution
mcp__code-tools__dotnet_find_solution_projects --solution-path "D:\source\repos\MCP-VS-AUTOMATION\VisualStudioMcp.sln"

# Check entire solution
mcp__code-tools__dotnet_check_solution --solution-path "D:\source\repos\MCP-VS-AUTOMATION\VisualStudioMcp.sln"

# Run tests (when test projects exist)
mcp__code-tools__run_all_tests_in_project --project-path "D:\source\repos\MCP-VS-AUTOMATION\tests\VisualStudioMcp.Core.Tests\VisualStudioMcp.Core.Tests.csproj"
```

**Note:** Always use Windows paths (D:\...) when calling MCP .NET tools from WSL.

### Git Workflow Commands
```bash
# Always create feature branches
git checkout -b feature/vs-debugging-control
git add . && git commit -m "Add VS debugging automation"
git push -u origin feature/vs-debugging-control
gh pr create --title "Add Visual Studio debugging automation" --body "Implements comprehensive debugging control capabilities"
```

## Technology Stack & Architecture

### Core Technologies
- **Runtime:** .NET 8.0 (LTS) with `net8.0-windows` target framework
- **Framework:** Console Application with Microsoft.Extensions.Hosting
- **Language:** C# 12.0 with nullable reference types enabled
- **MCP Protocol:** ModelContextProtocol 0.3.0-preview.3
- **Visual Studio APIs:** EnvDTE 17.0.32112.339, EnvDTE80 8.0.1
- **COM Interop:** Windows-only, requires Visual Studio 2022

### Project Structure
```
src/VisualStudioMcp.Server/     # Console application entry point
src/VisualStudioMcp.Core/       # Core VS automation services
src/VisualStudioMcp.Xaml/       # XAML designer automation
src/VisualStudioMcp.Debug/      # Debugging automation services
src/VisualStudioMcp.Imaging/    # Screenshot and visual capture
src/VisualStudioMcp.Shared/     # Common models and interfaces
tests/                          # Unit and integration test projects
```

### Service-Oriented Architecture
- Dependency injection with Microsoft.Extensions.DependencyInjection
- Service interfaces: `IVisualStudioService`, `IXamlDesignerService`, `IDebugService`, `IImagingService`
- MCP tool routing through `VisualStudioMcpServer` class
- COM object lifecycle management with proper disposal patterns

## Critical Implementation Patterns

### COM Object Lifecycle Management
**ALWAYS use this pattern for COM objects:**
```csharp
DTE dte = null;
try
{
    dte = GetActiveVisualStudioInstance();
    // Use dte here
    return result;
}
finally
{
    if (dte != null)
        Marshal.ReleaseComObject(dte);
}
```

### Async COM Operations Pattern
```csharp
public async Task<BuildResult> BuildSolutionAsync()
{
    return await Task.Run(() =>
    {
        // COM operations here - EnvDTE is not thread-safe
        // Always execute on background thread
    });
}
```

### MCP Tool Implementation Pattern
- All tools return `McpToolResult` objects
- Use structured error responses with detailed context
- Include comprehensive parameter validation
- Log all operations with structured logging

## Code Quality Standards

### COM Interop Requirements
- Always use `Marshal.ReleaseComObject()` for COM cleanup
- Wrap COM calls in try-catch with specific exception handling
- Never cache COM objects across method calls
- Handle Visual Studio crashes gracefully with retry logic
- Use CLSCTX_LOCAL_SERVER for secure COM activation

### Performance Requirements
- MCP tool responses must complete within 5 seconds maximum
- Screenshot capture operations within 2 seconds
- Use async/await patterns throughout
- Implement proper cancellation token support
- Monitor memory usage and dispose resources promptly

### Error Handling Standards
- Use structured exception handling with specific exception types
- Always log exceptions with full context using ILogger
- Provide meaningful error messages in MCP responses
- Implement connection retry logic for transient failures
- Never suppress exceptions - always log and handle appropriately

## Testing Strategy

### Unit Testing Requirements
- MSTest with Moq for COM object mocking
- Minimum 80% code coverage for core services
- Test all MCP tool implementations comprehensively
- Mock COM objects to avoid VS dependencies in unit tests

### Integration Testing
- Test with actual Visual Studio instances
- Validate COM exception scenarios and recovery
- Test screenshot capture quality across resolutions
- Validate debugging workflow end-to-end

## Development Environment Requirements

### Required Software
- Visual Studio 2022 (17.8 or later) with .NET desktop development workload
- .NET 8 SDK (8.0.100 or later) on Windows
- Windows 10/11 (Visual Studio COM dependency)
- Git with proper Windows line endings configured

### WSL Environment Notes
- You are running Claude Code in WSL2 Linux environment
- Cannot execute .NET commands directly (`dotnet build`, `dotnet run`, etc.)
- Must use `mcp__code-tools__dotnet_*` tools for all .NET operations
- Always pass Windows paths (D:\...) to MCP .NET tools
- Use Bash commands only for Git operations and file system access

### Project Configuration
- All projects target `net8.0-windows`
- `UseWindowsForms=true` for screenshot capture
- `PackAsTool=true` for global tool packaging
- `#nullable enable` throughout all source files

## Deployment as Global Tool
- Builds as .NET Global Tool with command name `vsmcp`
- Single executable with all dependencies included
- Semantic versioning (major.minor.patch)
- Automated CI/CD via GitHub Actions

## Key MCP Tools (Planned)
- `vs_list_instances` - List running Visual Studio instances
- `vs_connect_instance` - Connect to specific VS instance
- `vs_build_solution` - Build solutions with error capture
- `vs_start_debugging` - Control debugging sessions
- `vs_capture_xaml_designer` - Screenshot XAML designer surfaces
- `vs_capture_window` - Capture any VS window
- `vs_get_debug_state` - Inspect runtime debugging state

## Security & Safety
- Local development tool only (no network exposure)
- Validates all input parameters before COM calls
- Temporary file cleanup for screenshots
- No persistent sensitive data storage
- COM object sandboxing with minimal permissions

## Performance Monitoring
- Structured logging with correlation IDs
- Performance metrics for all operations
- Memory usage tracking for COM objects
- Screenshot capture timing validation