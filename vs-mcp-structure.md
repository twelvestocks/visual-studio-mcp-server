# Visual Studio MCP Server Project Structure

## Solution Structure
```
VisualStudioMcp.sln
├── src/
│   ├── VisualStudioMcp.Server/              # Console application (entry point)
│   ├── VisualStudioMcp.Core/                # Core automation logic
│   ├── VisualStudioMcp.Xaml/                # XAML-specific services
│   ├── VisualStudioMcp.Debug/               # Debugging automation
│   ├── VisualStudioMcp.Imaging/             # Visual capture services
│   └── VisualStudioMcp.Shared/              # Common models/interfaces
├── tests/
│   ├── VisualStudioMcp.Core.Tests/
│   ├── VisualStudioMcp.Xaml.Tests/
│   └── VisualStudioMcp.Integration.Tests/
└── docs/
    ├── README.md
    ├── API.md
    └── Configuration.md
```

## Main Console Application (VisualStudioMcp.Server)

### Program.cs
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using VisualStudioMcp.Core;

namespace VisualStudioMcp.Server;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(ConfigureServices)
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        var mcpServer = host.Services.GetRequiredService<VisualStudioMcpServer>();
        await mcpServer.RunAsync();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<VisualStudioMcpServer>();
        services.AddSingleton<IVisualStudioService, VisualStudioService>();
        services.AddSingleton<IXamlDesignerService, XamlDesignerService>();
        services.AddSingleton<IDebugService, DebugService>();
        services.AddSingleton<IImagingService, ImagingService>();
    }
}
```

### VisualStudioMcpServer.cs
```csharp
using ModelContextProtocol;
using Microsoft.Extensions.Logging;
using VisualStudioMcp.Shared.Models;

namespace VisualStudioMcp.Server;

public class VisualStudioMcpServer : McpServer
{
    private readonly IVisualStudioService _vsService;
    private readonly IXamlDesignerService _xamlService;
    private readonly IDebugService _debugService;
    private readonly IImagingService _imagingService;
    private readonly ILogger<VisualStudioMcpServer> _logger;

    public VisualStudioMcpServer(
        IVisualStudioService vsService,
        IXamlDesignerService xamlService,
        IDebugService debugService,
        IImagingService imagingService,
        ILogger<VisualStudioMcpServer> logger)
    {
        _vsService = vsService;
        _xamlService = xamlService;
        _debugService = debugService;
        _imagingService = imagingService;
        _logger = logger;
    }

    protected override async Task<McpToolResult> HandleToolCall(string toolName, object parameters)
    {
        _logger.LogInformation("Handling tool call: {ToolName}", toolName);

        try
        {
            return toolName switch
            {
                "vs_open_solution" => await HandleOpenSolution(parameters),
                "vs_build_solution" => await HandleBuildSolution(parameters),
                "vs_capture_xaml_designer" => await HandleCaptureXamlDesigner(parameters),
                "vs_start_debugging" => await HandleStartDebugging(parameters),
                "vs_get_debug_state" => await HandleGetDebugState(parameters),
                _ => McpToolResult.Error($"Unknown tool: {toolName}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling tool call: {ToolName}", toolName);
            return McpToolResult.Error($"Tool execution failed: {ex.Message}");
        }
    }

    private async Task<McpToolResult> HandleOpenSolution(object parameters)
    {
        var request = JsonSerializer.Deserialize<OpenSolutionRequest>(parameters.ToString());
        var result = await _vsService.OpenSolutionAsync(request.SolutionPath);
        return McpToolResult.Success(result);
    }

    // Additional tool handlers...
}
```

## Core Services Layer (VisualStudioMcp.Core)

### IVisualStudioService.cs
```csharp
using EnvDTE;
using VisualStudioMcp.Shared.Models;

namespace VisualStudioMcp.Core;

public interface IVisualStudioService
{
    Task<VisualStudioInstance[]> GetRunningInstancesAsync();
    Task<VisualStudioInstance> ConnectToInstanceAsync(int processId);
    Task<SolutionInfo> OpenSolutionAsync(string solutionPath);
    Task<BuildResult> BuildSolutionAsync(string configuration = "Debug");
    Task<ProjectInfo[]> GetProjectsAsync();
    Task ExecuteCommandAsync(string commandName, string args = "");
}
```

### VisualStudioService.cs
```csharp
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.Extensions.Logging;

namespace VisualStudioMcp.Core;

public class VisualStudioService : IVisualStudioService
{
    private readonly ILogger<VisualStudioService> _logger;
    private DTE _currentDte;

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

    public async Task<VisualStudioInstance[]> GetRunningInstancesAsync()
    {
        var instances = new List<VisualStudioInstance>();
        
        GetRunningObjectTable(0, out var rot);
        rot.EnumRunning(out var enumMoniker);
        
        var monikers = new IMoniker[1];
        while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
        {
            var bindCtx = CreateBindCtx(0);
            monikers[0].GetDisplayName(bindCtx, null, out var displayName);
            
            if (displayName?.StartsWith("!VisualStudio.DTE") == true)
            {
                rot.GetObject(monikers[0], out var obj);
                if (obj is DTE dte)
                {
                    instances.Add(new VisualStudioInstance
                    {
                        ProcessId = ExtractProcessId(displayName),
                        Version = dte.Version,
                        SolutionName = dte.Solution?.FullName ?? "No solution"
                    });
                }
            }
        }
        
        return instances.ToArray();
    }

    // Implementation details...
}
```

## Shared Models (VisualStudioMcp.Shared)

### Models/VisualStudioInstance.cs
```csharp
namespace VisualStudioMcp.Shared.Models;

public class VisualStudioInstance
{
    public int ProcessId { get; set; }
    public string Version { get; set; }
    public string SolutionName { get; set; }
    public DateTime StartTime { get; set; }
}

public class SolutionInfo
{
    public string FullPath { get; set; }
    public string Name { get; set; }
    public ProjectInfo[] Projects { get; set; }
    public bool IsOpen { get; set; }
}

public class BuildResult
{
    public bool Success { get; set; }
    public string Output { get; set; }
    public BuildError[] Errors { get; set; }
    public TimeSpan Duration { get; set; }
}
```

## XAML Services (VisualStudioMcp.Xaml)

### IXamlDesignerService.cs
```csharp
namespace VisualStudioMcp.Xaml;

public interface IXamlDesignerService
{
    Task<XamlDesignerCapture> CaptureDesignerAsync(string xamlFilePath);
    Task<XamlElement[]> GetVisualTreeAsync(string xamlFilePath);
    Task<XamlProperty[]> GetElementPropertiesAsync(string elementPath);
    Task ModifyElementPropertyAsync(string elementPath, string property, object value);
}
```

## Debugging Services (VisualStudioMcp.Debug)

### IDebugService.cs
```csharp
namespace VisualStudioMcp.Debug;

public interface IDebugService
{
    Task<DebugState> StartDebuggingAsync(string projectName = null);
    Task StopDebuggingAsync();
    Task<DebugState> GetCurrentStateAsync();
    Task<Breakpoint[]> GetBreakpointsAsync();
    Task<Breakpoint> AddBreakpointAsync(string file, int line, string condition = null);
    Task<Variable[]> GetLocalVariablesAsync();
    Task<CallStackFrame[]> GetCallStackAsync();
}
```

## Project Files

### VisualStudioMcp.Server.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>vsmcp</ToolCommandName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ModelContextProtocol" Version="0.3.0-preview.3" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\VisualStudioMcp.Core\VisualStudioMcp.Core.csproj" />
    <ProjectReference Include="..\VisualStudioMcp.Xaml\VisualStudioMcp.Xaml.csproj" />
    <ProjectReference Include="..\VisualStudioMcp.Debug\VisualStudioMcp.Debug.csproj" />
    <ProjectReference Include="..\VisualStudioMcp.Imaging\VisualStudioMcp.Imaging.csproj" />
  </ItemGroup>
</Project>
```

### VisualStudioMcp.Core.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="EnvDTE" Version="17.0.32112.339" />
    <PackageReference Include="EnvDTE80" Version="8.0.1" />
    <PackageReference Include="Microsoft.VisualStudio.SDK" Version="17.0.32112.339" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\VisualStudioMcp.Shared\VisualStudioMcp.Shared.csproj" />
  </ItemGroup>
</Project>
```

## Key Architectural Points

### **No UI/Views**
- Pure console application
- All communication via MCP protocol
- No WPF/WinForms dependencies in main application

### **Service-Oriented Architecture**
- Each major function area is a separate service
- Dependency injection for testability
- Clear separation of concerns

### **Async/Await Throughout**
- All VS automation operations are async
- Non-blocking MCP protocol handling
- Proper cancellation token support

### **Structured Logging**
- Microsoft.Extensions.Logging for consistent logging
- Structured logs for debugging VS automation
- Configurable log levels

### **Deployment**
- Builds as a .NET tool (`dotnet tool install -g vsmcp`)
- Single executable with all dependencies
- No installation beyond .NET runtime

This structure gives you a professional, maintainable codebase that can grow with your needs while keeping the concerns properly separated.