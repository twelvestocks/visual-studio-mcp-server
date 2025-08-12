# CLAUDE.md - Visual Studio MCP Server

### Project Overview
**Project Name:** Visual Studio MCP Server  
**Type:** Console Application (.NET Global Tool)  
**Primary Language:** C# 12.0  

### Project Management Protocol
**ALWAYS follow this workflow:**
- Always read PLANNING.md at the start of every new conversation
- Check TASKS.md before starting your work
- Mark completed tasks immediately
- Add newly discovered tasks

### Technology Stack
- **Runtime:** .NET 8.0 (LTS)
- **Framework:** Console Application with Microsoft.Extensions.Hosting
- **Language:** C# 12.0 with nullable reference types enabled
- **MCP Protocol:** ModelContextProtocol 0.3.0-preview.3
- **Visual Studio APIs:** EnvDTE 17.0.32112.339, EnvDTE80 8.0.1
- **Testing:** MSTest with Moq for mocking COM objects

### Project Structure
```
- `src/VisualStudioMcp.Server/`: Console application entry point
- `src/VisualStudioMcp.Core/`: Core VS automation services
- `src/VisualStudioMcp.Xaml/`: XAML designer automation
- `src/VisualStudioMcp.Debug/`: Debugging automation services
- `src/VisualStudioMcp.Imaging/`: Screenshot and visual capture
- `src/VisualStudioMcp.Shared/`: Common models and interfaces
- `tests/`: Unit and integration test projects
```

### Essential Commands
```bash
# Development
dotnet run --project src/VisualStudioMcp.Server
dotnet build --configuration Debug
dotnet test --logger console
dotnet pack --configuration Release

# Global tool installation (after pack)
dotnet tool install --global --add-source ./nupkg VisualStudioMcp
dotnet tool uninstall --global VisualStudioMcp

# Git workflow
git checkout -b feature/vs-debugging-control
git add . && git commit -m "Add VS debugging automation"
git push -u origin feature/vs-debugging-control
```

### Code Style & Standards
- Use explicit nullable reference types throughout (`#nullable enable`)
- Follow Microsoft C# coding conventions with PascalCase for public members
- Use dependency injection for all services with proper interface abstractions
- Always dispose COM objects properly using `using` statements or `Marshal.ReleaseComObject`
- Prefix all interface names with `I` (e.g., `IVisualStudioService`)
- Use async/await patterns for all I/O operations, never block with `.Result`

### Architecture Patterns
- Follow dependency injection pattern with Microsoft.Extensions.DependencyInjection
- Use service-oriented architecture with clear separation between MCP protocol handling and VS automation
- Implement proper COM object lifetime management with try/finally blocks
- Use structured logging with Microsoft.Extensions.Logging throughout
- Apply command pattern for MCP tool handlers

### COM Interop Requirements
- Always use `Marshal.ReleaseComObject()` for COM cleanup
- Wrap COM calls in try-catch blocks with specific exception handling
- Use CLSCTX_LOCAL_SERVER for COM object creation security
- Never cache COM objects across method calls - always obtain fresh references
- Handle COM exceptions gracefully with meaningful error messages

### Testing Requirements
- Write unit tests for all service classes using Moq for COM object mocking
- Create integration tests that work with actual Visual Studio instances
- Test COM exception scenarios and recovery mechanisms
- Maintain minimum 80% code coverage for core automation logic
- Test files should mirror source structure: `VisualStudioService.cs` → `VisualStudioServiceTests.cs`

### Error Handling
- Use structured exception handling with specific exception types
- Always log exceptions with full context using ILogger
- Provide meaningful error messages in MCP responses
- Handle Visual Studio crashes gracefully with connection retry logic
- Never suppress exceptions - always log and re-throw or handle appropriately

### Performance Standards
- MCP tool responses must complete within 5 seconds maximum
- Screenshot capture operations should complete within 2 seconds
- Use async/await for all potentially blocking operations
- Implement proper cancellation token support for long-running operations
- Monitor memory usage and dispose resources promptly

### Security Considerations
- No network exposure - localhost MCP communication only
- Validate all input parameters before passing to COM objects
- Use minimal Windows API permissions required
- Clean up temporary files immediately after use
- Never store sensitive data persistently

### MCP Protocol Implementation
- Use ModelContextProtocol NuGet package version 0.3.0-preview.3
- Implement proper JSON serialisation for all request/response objects
- Return structured McpToolResult objects with success/error states
- Include detailed error information in failed responses
- Register all tools with clear descriptions and parameter specifications

### Visual Studio Integration Patterns
- Use GetRunningObjectTable() to discover VS instances
- Prefer EnvDTE2 interfaces over EnvDTE where available
- Always check for null references before accessing DTE properties
- Use Solution.SolutionBuild for build operations
- Access debugger through DTE.Debugger interface

### Screenshot Capture Standards
- Use Windows GDI+ BitBlt for high-quality captures
- Save screenshots as PNG format with optimal compression
- Include window boundaries and handle multi-monitor scenarios
- Implement async capture to avoid blocking MCP responses
- Clean up bitmap resources immediately after processing

### Review Process
Before submitting any code:
1. Run `dotnet build` with zero warnings
2. Execute `dotnet test` with all tests passing
3. Verify COM objects are properly disposed
4. Test with actual Visual Studio instance
5. Check memory usage and resource cleanup

### Do NOT
- Use synchronous COM calls that could block indefinitely
- Cache COM object references across method boundaries
- Ignore COM exceptions or Visual Studio connection failures
- Create UI elements in this console application
- Use outdated EnvDTE patterns - prefer modern async approaches
- Commit code that doesn't dispose COM resources properly

### Environment Setup
- Visual Studio 2022 (17.8 or later) must be installed
- .NET 8 SDK required for development
- Windows 10/11 for COM interop support
- Git configured for proper line endings (autocrlf=true)

### Deployment
- Package as .NET Global Tool using `PackAsTool=true`
- Tool command name should be `vsmcp`
- Include all dependencies in single deployment unit
- Version using semantic versioning (major.minor.patch)

### COM Object Disposal Pattern
```csharp
// ALWAYS use this pattern for COM objects
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

### Async COM Pattern
```csharp
// Use Task.Run for COM operations that might block
public async Task<BuildResult> BuildSolutionAsync()
{
    return await Task.Run(() =>
    {
        // COM operations here
    });
}
```