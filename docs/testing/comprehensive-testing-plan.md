# Visual Studio MCP Server - Comprehensive Testing & Deployment Plan

## Overview

This document outlines the systematic testing approach for the Visual Studio MCP Server, from initial compilation through user acceptance testing with Claude Code. The plan accounts for the restart limitations of LLM testing and implements a batch-fix methodology.

## Testing Philosophy

**Key Principles:**
- **Systematic Progression**: Test in phases from basic to complex functionality
- **Batch Documentation**: Record all findings before attempting fixes
- **No Fix-As-We-Go**: Complete full test cycles before implementing fixes
- **Comprehensive Coverage**: Test all 17 MCP tools and integration scenarios
- **Real-World Conditions**: Test with actual Visual Studio instances and projects

## Phase 1: Pre-Deployment Compilation & Build Testing

**Objective**: Verify the codebase compiles and packages correctly before any deployment attempts.

### 1.1 Solution Compilation Testing
```bash
# Test basic compilation
mcp__code-tools__dotnet_check_solution --solution-path "D:\source\repos\MCP-VS-AUTOMATION\VisualStudioMcp.sln"

# Test each project individually
mcp__code-tools__dotnet_check_compilation --project-path "D:\source\repos\MCP-VS-AUTOMATION\src\VisualStudioMcp.Server\VisualStudioMcp.Server.csproj"
mcp__code-tools__dotnet_check_compilation --project-path "D:\source\repos\MCP-VS-AUTOMATION\src\VisualStudioMcp.Core\VisualStudioMcp.Core.csproj"
mcp__code-tools__dotnet_check_compilation --project-path "D:\source\repos\MCP-VS-AUTOMATION\src\VisualStudioMcp.Debug\VisualStudioMcp.Debug.csproj"
mcp__code-tools__dotnet_check_compilation --project-path "D:\source\repos\MCP-VS-AUTOMATION\src\VisualStudioMcp.Xaml\VisualStudioMcp.Xaml.csproj"
mcp__code-tools__dotnet_check_compilation --project-path "D:\source\repos\MCP-VS-AUTOMATION\src\VisualStudioMcp.Imaging\VisualStudioMcp.Imaging.csproj"
```

### 1.2 Dependency Analysis
```bash
# Verify all package references resolve correctly
mcp__code-tools__dotnet_analyze_solution_structure --solution-path "D:\source\repos\MCP-VS-AUTOMATION\VisualStudioMcp.sln"
```

### 1.3 Build Script Testing
```powershell
# Test build script in dry-run mode first
./scripts/build-global-tool.ps1 -SkipTests -DryRun

# Test actual build process
./scripts/build-global-tool.ps1 -SkipTests -Pack
```

**Success Criteria:**
- [ ] Solution compiles with zero errors
- [ ] All individual projects compile successfully
- [ ] No circular dependencies detected
- [ ] Build script executes without errors
- [ ] NuGet package is generated successfully

**Findings Documentation**: Record in `test-findings-phase1.md`

---

## Phase 2: Global Tool Packaging & Installation Testing

**Objective**: Verify the global tool can be packaged, installed, and executed correctly.

### 2.1 Local Packaging Test
```powershell
# Execute full build and package
./scripts/build-global-tool.ps1 -Configuration Release -Pack

# Verify package contents
# Manual inspection of artifacts/VisualStudioMcp.*.nupkg
```

### 2.2 Local Installation Test
```bash
# Test installation from local package
dotnet tool install --global --add-source ./artifacts VisualStudioMcp

# Verify tool is available
vsmcp --version
vsmcp --help
```

### 2.3 Tool Execution Test
```bash
# Test basic MCP server startup
vsmcp
# Should start MCP server and show initialization messages
# Test with Ctrl+C to ensure clean shutdown
```

### 2.4 Uninstall/Reinstall Test
```bash
# Test clean uninstall
dotnet tool uninstall --global VisualStudioMcp

# Reinstall and verify
dotnet tool install --global --add-source ./artifacts VisualStudioMcp
vsmcp --version
```

**Success Criteria:**
- [ ] Package builds without errors
- [ ] Global tool installs successfully
- [ ] Tool executes and shows version information
- [ ] MCP server starts and shuts down cleanly
- [ ] Uninstall/reinstall cycle works correctly

**Findings Documentation**: Record in `test-findings-phase2.md`

---

## Phase 3: MCP Protocol & Basic Connectivity Testing

**Objective**: Verify MCP protocol implementation and basic Visual Studio connectivity.

### 3.1 MCP Server Integration Test
```json
// Add to Claude Code mcp_servers.json for testing
{
  "mcpServers": {
    "visual-studio-test": {
      "command": "vsmcp",
      "args": [],
      "env": {}
    }
  }
}
```

### 3.2 Basic MCP Tool Discovery
**Test Commands for Claude Code:**
```
List all available MCP tools from the visual-studio server
Show me the vs_list_instances tool specification
Show me all tool specifications for debugging tools
```

### 3.3 Visual Studio Instance Detection
**Test Commands:**
```
Use vs_list_instances to show all running Visual Studio instances
If no instances running, start Visual Studio 2022 and re-test vs_list_instances
Test vs_connect_instance with a valid instance ID
```

### 3.4 Basic Solution Operations
**Test Commands:**
```
Use vs_open_solution to open a test solution file
Use vs_get_projects to list all projects in the solution
Use vs_build_solution to build the current solution
```

**Success Criteria:**
- [ ] MCP server registers correctly with Claude Code
- [ ] All 17 tools are discoverable
- [ ] Tool specifications are valid and complete
- [ ] Visual Studio instances can be detected and connected
- [ ] Basic solution operations work without errors

**Findings Documentation**: Record in `test-findings-phase3.md`

---

## Phase 4: Comprehensive User Acceptance Testing

**Objective**: Systematically test all 17 MCP tools through realistic usage scenarios.

### 4.1 Core VS Management Tools (5 tools)

**Test Script for Claude Code:**
```markdown
## Core VS Management Testing

Test each tool systematically and document results:

### vs_list_instances
- Execute: List all Visual Studio instances
- Expected: Should return list with at least one VS 2022 instance
- Document: Instance details, PIDs, solution paths

### vs_connect_instance  
- Execute: Connect to the first available instance
- Expected: Successful connection confirmation
- Document: Connection status, instance details

### vs_open_solution
- Execute: Open a known test solution file
- Expected: Solution opens in Visual Studio
- Document: Success/failure, error messages

### vs_build_solution
- Execute: Build the currently open solution
- Expected: Build completes, errors/warnings reported
- Document: Build status, error details, build time

### vs_get_projects
- Execute: Get list of projects in current solution
- Expected: Returns project list with types and paths
- Document: Project count, types, any missing information
```

### 4.2 Build and Project Tools (4 tools)

**Test Script:**
```markdown
## Build and Project Testing

### vs_build_project
- Execute: Build a specific project from the solution
- Expected: Project builds successfully
- Document: Build output, timing, errors

### vs_clean_solution
- Execute: Clean the current solution
- Expected: Clean completes successfully
- Document: Clean status, output folder state

### vs_get_build_errors
- Execute: Get current build errors/warnings
- Expected: Returns structured error information
- Document: Error format, completeness, severity levels

### vs_get_solution_info
- Execute: Get detailed solution information
- Expected: Returns solution properties and statistics
- Document: Information completeness, accuracy
```

### 4.3 Debugging Tools (4 tools)

**Test Script:**
```markdown
## Debugging Tools Testing

### vs_start_debugging
- Execute: Start debugging the startup project
- Expected: Debugging session starts successfully
- Document: Startup time, debug state, any errors

### vs_stop_debugging
- Execute: Stop the current debugging session
- Expected: Clean debugging session termination
- Document: Stop time, cleanup status

### vs_get_debug_state
- Execute: Get current debugging state information
- Expected: Returns detailed debug state
- Document: State accuracy, available information

### vs_set_breakpoint
- Execute: Set breakpoint at specific file/line
- Expected: Breakpoint set successfully
- Document: Breakpoint confirmation, Visual Studio UI update
```

### 4.4 Visual Capture Tools (4 tools)

**Test Script:**
```markdown
## Visual Capture Testing

### vs_capture_window
- Execute: Capture the main Visual Studio window
- Expected: High-quality screenshot saved
- Document: Image quality, file size, save location

### vs_capture_xaml_designer
- Execute: Capture XAML designer for a WPF project
- Expected: Designer surface captured clearly
- Document: Designer visibility, capture quality

### vs_capture_code_editor
- Execute: Capture the code editor with syntax highlighting
- Expected: Clear code capture with proper formatting
- Document: Text clarity, syntax highlighting preservation

### vs_capture_with_annotations
- Execute: Capture with element highlighting/annotations
- Expected: Screenshot with clear annotations
- Document: Annotation accuracy, visual clarity
```

### 4.5 Advanced Workflow Testing

**Test Complex Scenarios:**
```markdown
## Advanced Workflow Testing

### Debugging Workflow
1. Open solution with vs_open_solution
2. Build with vs_build_solution
3. Set breakpoints with vs_set_breakpoint
4. Start debugging with vs_start_debugging
5. Capture debug state with vs_get_debug_state
6. Stop debugging with vs_stop_debugging
Document: Complete workflow success, any break points

### UI Development Workflow
1. Open WPF project solution
2. Build the UI project
3. Capture XAML designer
4. Start debugging
5. Capture running application
6. Stop debugging
Document: UI workflow completeness, visual quality

### Error Investigation Workflow
1. Open solution with known build errors
2. Build solution
3. Get build errors with vs_get_build_errors
4. Capture error list window
5. Fix error and rebuild
Document: Error handling accuracy, fix verification
```

### 4.6 Performance and Reliability Testing

**Test Commands:**
```markdown
## Performance Testing

### Response Time Testing
- Execute each tool 3 times and measure response times
- Document: Average response time per tool
- Flag: Any tools taking >5 seconds

### Reliability Testing
- Execute each tool 5 times consecutively
- Document: Success rate, any intermittent failures
- Flag: Tools with <100% success rate

### Resource Usage Testing
- Monitor Visual Studio memory/CPU during automation
- Document: Resource usage patterns
- Flag: Any excessive resource consumption

### Error Recovery Testing
- Test tools when Visual Studio is not responsive
- Test tools when no Visual Studio instance running
- Document: Error messages, recovery behavior
```

**Success Criteria for Phase 4:**
- [ ] All 17 MCP tools execute without errors
- [ ] All tools return expected data formats
- [ ] Complex workflows complete successfully
- [ ] Response times are acceptable (<5 seconds)
- [ ] No Visual Studio crashes or freezes
- [ ] Error handling is graceful and informative

**Findings Documentation**: Record in `test-findings-phase4.md`

---

## Phase 5: Fix Cycle & Regression Testing

**Objective**: Address findings from previous phases and verify fixes don't introduce regressions.

### 5.1 Issue Prioritization
```markdown
## Issue Classification

### Critical Issues (Must Fix)
- Tool completely non-functional
- Visual Studio crashes
- MCP protocol violations
- Data corruption or loss

### High Priority Issues (Should Fix)
- Incorrect data returned
- Poor error messages
- Performance issues (>5 second response)
- UI capture quality problems

### Medium Priority Issues (Nice to Fix)
- Missing optional features
- Minor UI inconsistencies
- Documentation gaps
- Performance optimizations

### Low Priority Issues (Future)
- Enhancement requests
- Style improvements
- Additional features
```

### 5.2 Fix Implementation Process
```markdown
## Fix Cycle Process

For each identified issue:

1. **Analysis Phase**
   - Reproduce the issue
   - Identify root cause
   - Design fix approach
   - Estimate impact

2. **Implementation Phase**
   - Implement fix
   - Update unit tests if applicable
   - Update documentation if needed

3. **Verification Phase**
   - Test fix resolves issue
   - Run regression tests
   - Verify no new issues introduced

4. **Documentation Phase**
   - Update findings document
   - Mark issue as resolved
   - Note any related changes needed
```

### 5.3 Regression Testing Protocol
```markdown
## Regression Test Suite

After each fix implementation:

### Core Functionality Test
- vs_list_instances
- vs_connect_instance  
- vs_build_solution
- vs_capture_window

### Integration Test
- Open solution workflow
- Debugging workflow
- Build and fix errors workflow

### Performance Test
- Response time verification
- Resource usage check
- Stability over 10 consecutive operations
```

**Success Criteria for Phase 5:**
- [ ] All critical issues resolved
- [ ] 90%+ of high priority issues resolved
- [ ] No regression in previously working functionality
- [ ] Performance maintained or improved
- [ ] Clean test run through all phases

**Findings Documentation**: Record in `test-findings-phase5.md`

---

## Testing Infrastructure

### 5.1 Test Environment Setup
```markdown
## Required Environment

### Software Requirements
- Windows 10/11
- Visual Studio 2022 (latest version)
- .NET 8 SDK
- Claude Code with MCP support
- Git command line tools

### Test Projects
- Simple console application (.NET 8)
- WPF application with XAML designers
- Solution with multiple projects
- Solution with deliberate build errors
- Solution with unit tests

### Preparation Steps
1. Create test solutions in dedicated folder
2. Ensure Visual Studio is running with test solution
3. Configure Claude Code with MCP server
4. Prepare findings documentation templates
```

### 5.2 Documentation Templates

**Finding Entry Template:**
```markdown
## Issue ID: [PHASE]-[NUMBER]
**Tool:** [Tool Name]
**Severity:** [Critical/High/Medium/Low]  
**Description:** [Clear description of issue]
**Steps to Reproduce:** 
1. [Step 1]
2. [Step 2]
**Expected Result:** [What should happen]
**Actual Result:** [What actually happened]
**Error Messages:** [Any error messages]
**Screenshots:** [If applicable]
**Impact:** [User impact description]
**Proposed Fix:** [If obvious]
```

### 5.3 Success Metrics

**Overall Success Criteria:**
- [ ] 100% of MCP tools functional
- [ ] 95%+ reliability across all test scenarios
- [ ] <3 second average response time for simple operations
- [ ] <10 second response time for complex operations (debugging, builds)
- [ ] Zero Visual Studio crashes during testing
- [ ] High-quality visual captures (readable text, proper formatting)
- [ ] Graceful error handling for all edge cases

**Deployment Readiness Checklist:**
- [ ] All Phase 1-5 testing completed
- [ ] Critical and high priority issues resolved
- [ ] Documentation updated with known limitations
- [ ] Performance benchmarks documented
- [ ] User guide validated through testing
- [ ] Installation process verified end-to-end

---

## Execution Timeline

**Phase 1**: 1-2 hours (Compilation & Build)
**Phase 2**: 1-2 hours (Packaging & Installation)  
**Phase 3**: 2-3 hours (MCP & Basic Connectivity)
**Phase 4**: 4-6 hours (Comprehensive UAT)
**Phase 5**: Variable (Fix Implementation)

**Total Estimated Time**: 8-13 hours + fix time

**Critical Path**: Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 (repeat as needed)

---

This comprehensive testing plan ensures systematic verification of all functionality while accounting for the batch-fix methodology required for LLM-based testing. Each phase builds on the previous one, ensuring we don't waste time on advanced testing if basic functionality is broken.