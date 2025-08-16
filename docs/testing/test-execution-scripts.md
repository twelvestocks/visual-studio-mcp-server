# Visual Studio MCP Server - Test Execution Scripts

This document provides ready-to-execute scripts and commands for systematic testing of the Visual Studio MCP Server.

## Phase 1: Pre-Deployment Testing Scripts

### Script 1.1: Solution Compilation Test
```bash
# Execute from WSL in project root
echo "=== Phase 1.1: Solution Compilation Test ==="
echo "Testing solution compilation..."
mcp__code-tools__dotnet_check_solution --solution-path "D:\source\repos\MCP-VS-AUTOMATION\VisualStudioMcp.sln"

echo "Testing individual projects..."
mcp__code-tools__dotnet_check_compilation --project-path "D:\source\repos\MCP-VS-AUTOMATION\src\VisualStudioMcp.Server\VisualStudioMcp.Server.csproj"
mcp__code-tools__dotnet_check_compilation --project-path "D:\source\repos\MCP-VS-AUTOMATION\src\VisualStudioMcp.Core\VisualStudioMcp.Core.csproj"
mcp__code-tools__dotnet_check_compilation --project-path "D:\source\repos\MCP-VS-AUTOMATION\src\VisualStudioMcp.Debug\VisualStudioMcp.Debug.csproj"
mcp__code-tools__dotnet_check_compilation --project-path "D:\source\repos\MCP-VS-AUTOMATION\src\VisualStudioMcp.Xaml\VisualStudioMcp.Xaml.csproj"
mcp__code-tools__dotnet_check_compilation --project-path "D:\source\repos\MCP-VS-AUTOMATION\src\VisualStudioMcp.Imaging\VisualStudioMcp.Imaging.csproj"
mcp__code-tools__dotnet_check_compilation --project-path "D:\source\repos\MCP-VS-AUTOMATION\src\VisualStudioMcp.Shared\VisualStudioMcp.Shared.csproj"

echo "Testing dependency analysis..."
mcp__code-tools__dotnet_analyze_solution_structure --solution-path "D:\source\repos\MCP-VS-AUTOMATION\VisualStudioMcp.sln"
```

### Script 1.2: Build Script Test
```powershell
# Execute from PowerShell in project root
Write-Host "=== Phase 1.2: Build Script Test ===" -ForegroundColor Cyan

Write-Host "Testing build script in dry-run mode..." -ForegroundColor Yellow
./scripts/build-global-tool.ps1 -SkipTests -DryRun

Write-Host "Testing actual build process..." -ForegroundColor Yellow  
./scripts/build-global-tool.ps1 -SkipTests -Pack

Write-Host "Checking artifacts..." -ForegroundColor Yellow
Get-ChildItem ./artifacts -Filter "*.nupkg" | Format-Table Name, Length, LastWriteTime
```

## Phase 2: Packaging & Installation Scripts

### Script 2.1: Global Tool Packaging Test
```powershell
# Execute from PowerShell in project root
Write-Host "=== Phase 2.1: Global Tool Packaging Test ===" -ForegroundColor Cyan

# Clean previous artifacts
if (Test-Path ./artifacts) {
    Remove-Item ./artifacts -Recurse -Force
    Write-Host "Cleaned previous artifacts" -ForegroundColor Green
}

# Execute full build and package
Write-Host "Building and packaging..." -ForegroundColor Yellow
./scripts/build-global-tool.ps1 -Configuration Release -Pack

# Verify package contents
Write-Host "Package verification:" -ForegroundColor Yellow
$packages = Get-ChildItem ./artifacts -Filter "VisualStudioMcp.*.nupkg"
foreach ($package in $packages) {
    Write-Host "  Package: $($package.Name)" -ForegroundColor White
    Write-Host "  Size: $([math]::Round($package.Length / 1KB, 2)) KB" -ForegroundColor White
    Write-Host "  Created: $($package.LastWriteTime)" -ForegroundColor White
}
```

### Script 2.2: Installation & Execution Test
```bash
# Execute from bash/WSL
echo "=== Phase 2.2: Installation & Execution Test ==="

# Check if tool is already installed and uninstall
echo "Checking for existing installation..."
if dotnet tool list --global | grep -q "VisualStudioMcp"; then
    echo "Uninstalling existing version..."
    dotnet tool uninstall --global VisualStudioMcp
fi

# Install from local package
echo "Installing from local package..."
dotnet tool install --global --add-source ./artifacts VisualStudioMcp

# Test tool execution
echo "Testing tool execution..."
echo "Version check:"
vsmcp --version

echo "Help command:"
vsmcp --help

# Test MCP server startup (brief)
echo "Testing MCP server startup (10 second test)..."
timeout 10 vsmcp || echo "MCP server startup test completed"
```

## Phase 3: MCP Protocol Testing Scripts

### Script 3.1: Claude Code MCP Configuration
```json
// Add this to Claude Code mcp_servers.json
{
  "mcpServers": {
    "visual-studio-test": {
      "command": "vsmcp",
      "args": [],
      "env": {},
      "timeout": 30000
    }
  }
}
```

### Script 3.2: MCP Tool Discovery Commands
```
Execute these commands in Claude Code after MCP server is configured:

1. List all available MCP tools from the visual-studio-test server

2. Show me the tool specification for vs_list_instances

3. Show me all debugging-related tool specifications (vs_start_debugging, vs_stop_debugging, vs_get_debug_state, vs_set_breakpoint)

4. Show me all visual capture tool specifications (vs_capture_window, vs_capture_xaml_designer, vs_capture_code_editor, vs_capture_with_annotations)

5. Verify all 17 tools are available and properly specified
```

### Script 3.3: Basic Connectivity Test Commands
```
Execute these commands in Claude Code to test basic connectivity:

1. Use vs_list_instances to show all running Visual Studio instances
   Expected: List of instances or message indicating no instances running

2. Start Visual Studio 2022 manually, then re-run vs_list_instances
   Expected: At least one VS 2022 instance shown

3. Use vs_connect_instance with the first available instance ID
   Expected: Successful connection confirmation

4. Use vs_open_solution to open a test solution file (provide actual path)
   Expected: Solution opens in Visual Studio

5. Use vs_get_projects to list all projects in the solution
   Expected: List of projects with names, types, and paths
```

## Phase 4: Comprehensive UAT Scripts

### Script 4.1: Core VS Management Tools Test
```
Execute these commands systematically in Claude Code:

=== VS Management Tools Testing ===

Test 1: vs_list_instances
Command: Use vs_list_instances to show all Visual Studio instances
Document: Number of instances, PIDs, solution paths, versions

Test 2: vs_connect_instance
Command: Use vs_connect_instance to connect to instance [use actual ID from step 1]
Document: Connection success/failure, response time, instance details

Test 3: vs_open_solution
Command: Use vs_open_solution to open [provide actual solution path]
Document: Open success/failure, Visual Studio response, error messages

Test 4: vs_build_solution
Command: Use vs_build_solution to build the currently open solution
Document: Build time, success/failure, error count, warning count

Test 5: vs_get_projects
Command: Use vs_get_projects to list all projects in the current solution
Document: Project count, project types, completeness of information
```

### Script 4.2: Build and Project Tools Test
```
Execute these commands systematically in Claude Code:

=== Build and Project Tools Testing ===

Test 6: vs_build_project
Command: Use vs_build_project to build [specific project name from previous test]
Document: Build success/failure, build time, output details

Test 7: vs_clean_solution
Command: Use vs_clean_solution to clean the current solution
Document: Clean success/failure, time taken, confirmation of clean

Test 8: vs_get_build_errors
Command: Use vs_get_build_errors to get current build errors and warnings
Document: Error format, completeness, severity levels, file references

Test 9: vs_get_solution_info
Command: Use vs_get_solution_info to get detailed solution information
Document: Information accuracy, completeness, useful metrics
```

### Script 4.3: Debugging Tools Test
```
Execute these commands systematically in Claude Code:

=== Debugging Tools Testing ===

Test 10: vs_start_debugging
Command: Use vs_start_debugging to start debugging the startup project
Document: Debug start time, success/failure, debug state confirmation

Test 11: vs_get_debug_state
Command: Use vs_get_debug_state to get current debugging information
Document: Debug state accuracy, available information, process details

Test 12: vs_set_breakpoint
Command: Use vs_set_breakpoint to set a breakpoint at [specific file:line]
Document: Breakpoint set confirmation, Visual Studio UI update

Test 13: vs_stop_debugging
Command: Use vs_stop_debugging to stop the current debugging session
Document: Stop time, cleanup confirmation, final state
```

### Script 4.4: Visual Capture Tools Test
```
Execute these commands systematically in Claude Code:

=== Visual Capture Tools Testing ===

Test 14: vs_capture_window
Command: Use vs_capture_window to capture the main Visual Studio window
Document: Image quality, file size, save location, clarity

Test 15: vs_capture_code_editor
Command: Use vs_capture_code_editor to capture the code editor with syntax highlighting
Document: Text clarity, syntax highlighting preservation, readability

Test 16: vs_capture_xaml_designer
Command: Use vs_capture_xaml_designer to capture XAML designer (if WPF project open)
Document: Designer visibility, capture quality, design elements clarity

Test 17: vs_capture_with_annotations
Command: Use vs_capture_with_annotations to capture with element highlighting
Document: Annotation accuracy, visual clarity, element identification
```

### Script 4.5: Advanced Workflow Test
```
Execute this complete workflow in Claude Code:

=== Advanced Workflow Testing ===

Workflow 1: Complete Development Cycle
1. Use vs_list_instances and connect to available instance
2. Use vs_open_solution with a test WPF solution
3. Use vs_build_solution to ensure clean build
4. Use vs_capture_xaml_designer to document UI state
5. Use vs_set_breakpoint in MainWindow.xaml.cs constructor
6. Use vs_start_debugging to launch application
7. Use vs_get_debug_state to confirm debugging active
8. Use vs_capture_window to show debugging session
9. Use vs_stop_debugging to end session
10. Use vs_get_build_errors to confirm no issues

Document: Complete workflow success, any break points, timing

Workflow 2: Error Investigation Cycle
1. Open solution with known build errors
2. Use vs_build_solution (should fail)
3. Use vs_get_build_errors to get error details
4. Use vs_capture_window showing error list
5. Fix one error manually in Visual Studio
6. Use vs_build_solution again
7. Use vs_get_build_errors to confirm fix

Document: Error handling accuracy, fix verification process
```

## Phase 5: Performance and Reliability Scripts

### Script 5.1: Performance Measurement
```
Execute these commands in Claude Code for performance testing:

=== Performance Testing ===

For each of the 17 MCP tools, execute 3 times and record response times:

Tools to test (execute each 3 times):
- vs_list_instances
- vs_connect_instance  
- vs_open_solution
- vs_build_solution
- vs_get_projects
- vs_build_project
- vs_clean_solution
- vs_get_build_errors
- vs_get_solution_info
- vs_start_debugging
- vs_stop_debugging
- vs_get_debug_state
- vs_set_breakpoint
- vs_capture_window
- vs_capture_code_editor
- vs_capture_xaml_designer
- vs_capture_with_annotations

For each tool execution, document:
- Start time
- End time
- Response time
- Success/failure
- Any error messages

Performance targets:
- Simple operations (<3 seconds): vs_list_instances, vs_get_debug_state, vs_get_projects
- Complex operations (<10 seconds): vs_build_solution, vs_start_debugging, vs_capture_*
```

### Script 5.2: Reliability Testing
```
Execute this reliability test in Claude Code:

=== Reliability Testing ===

Execute each tool 5 times consecutively and record results:

Template for each tool:
"Execute vs_[TOOL_NAME] five times in a row and report results for each execution including success/failure and response time"

Tools to test:
1. vs_list_instances (5 consecutive executions)
2. vs_connect_instance (5 consecutive executions)
3. vs_build_solution (5 consecutive executions) 
4. vs_capture_window (5 consecutive executions)
[Continue for all 17 tools]

Success criteria:
- 100% success rate for each tool
- Consistent response times (±20% variance)
- No Visual Studio crashes or freezes
- No memory leaks or resource issues
```

## Test Data Preparation

### Required Test Solutions
```bash
# Create test project structure
mkdir test-projects
cd test-projects

# 1. Simple Console Application
dotnet new console -n SimpleConsole
cd SimpleConsole
# Add some basic code
cd ..

# 2. WPF Application  
dotnet new wpf -n WpfTestApp
cd WpfTestApp
# Add XAML controls for designer testing
cd ..

# 3. Solution with Multiple Projects
dotnet new sln -n MultiProjectSolution
dotnet new classlib -n BusinessLogic
dotnet new console -n ConsoleApp
dotnet sln MultiProjectSolution.sln add BusinessLogic/BusinessLogic.csproj
dotnet sln MultiProjectSolution.sln add ConsoleApp/ConsoleApp.csproj
cd ConsoleApp
dotnet add reference ../BusinessLogic/BusinessLogic.csproj
cd ..

# 4. Solution with Build Errors (manually introduce errors)
cp -r SimpleConsole ErrorProneConsole
# Manually edit ErrorProneConsole to introduce syntax errors
```

### Test Environment Verification Script
```bash
echo "=== Test Environment Verification ==="
echo "Windows Version:"
powershell.exe "Get-ComputerInfo | Select-Object WindowsProductName, WindowsVersion"

echo ".NET Version:"
dotnet --version

echo "Visual Studio Instances:"
powershell.exe "Get-Process devenv -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, StartTime"

echo "Claude Code Configuration:"
ls ~/.claude/
cat ~/.claude/mcp_servers.json

echo "Test Projects Available:"
ls test-projects/
```

This comprehensive set of test scripts provides a systematic approach to validating all aspects of the Visual Studio MCP Server functionality.