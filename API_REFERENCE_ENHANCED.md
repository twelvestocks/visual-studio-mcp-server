# Enhanced API Reference - Visual Studio MCP Server
## Working Examples for All Implemented Tools

**Last Updated:** 12 August 2025  
**API Version:** 1.0 (Phase 2 Complete)  
**Target Audience:** Claude Code users and MCP developers  

---

## 🚀 Quick Start with Real Examples

### Test Your Setup (30 seconds)
```bash
# Test MCP server is working
echo '{"method":"tools/list"}' | vsmcp

# Expected response shows all 5 tools
```

### Basic Workflow (5 minutes)
1. **Discover instances** → `vs_list_instances`
2. **Connect to one** → `vs_connect_instance` 
3. **Open solution** → `vs_open_solution`
4. **Build solution** → `vs_build_solution`
5. **Check projects** → `vs_get_projects`

---

## 📋 Implemented MCP Tools (Phase 2)

### 1. `vs_list_instances` - Discover Visual Studio Instances

**Purpose:** Find all running Visual Studio processes with metadata for connection

**Parameters:** None

**MCP Request Format:**
```json
{
  "method": "tools/call",
  "params": {
    "name": "vs_list_instances",
    "arguments": {}
  },
  "id": 1
}
```

**Response Format:**
```json
{
  "result": {
    "success": true,
    "data": {
      "instances": [
        {
          "processId": 15420,
          "version": "17.8.0", 
          "solutionPath": "C:\\Dev\\MyProject\\MyProject.sln",
          "isHealthy": true,
          "mainWindowTitle": "MyProject - Microsoft Visual Studio",
          "edition": "Professional",
          "startTime": "2025-08-12T14:30:00Z"
        }
      ],
      "count": 1,
      "timestamp": "2025-08-12T16:45:00Z"
    }
  }
}
```

**Claude Code Usage:**
```
"Show me all running Visual Studio instances"
"List Visual Studio processes with their solutions"  
"Which VS instances are currently running?"
"Find all Visual Studio windows and their projects"
```

**Real-World Example:**
```
User: "List my Visual Studio instances"
Claude: I can see 2 Visual Studio instances running:

1. **Process 15420** (Professional 17.8.0)
   - Solution: MyWebApp.sln
   - Status: Healthy ✅
   - Window: "MyWebApp - Microsoft Visual Studio"

2. **Process 18750** (Professional 17.8.0)  
   - Solution: MobileApp.sln
   - Status: Healthy ✅
   - Window: "MobileApp - Microsoft Visual Studio"

Which instance would you like to work with?
```

**Security Features:**
- Only actual Visual Studio processes are returned
- Process validation prevents targeting other applications
- Health status indicates connection reliability

**Error Handling:**
```json
{
  "result": {
    "success": false,
    "errorMessage": "No Visual Studio instances found",
    "errorCode": "VS_NOT_FOUND",
    "errorDetails": "Ensure Visual Studio 2022 is running"
  }
}
```

---

### 2. `vs_connect_instance` - Connect to Specific Instance

**Purpose:** Establish connection to a Visual Studio instance for automation

**Parameters:**
- `processId` (required, integer): Process ID from `vs_list_instances`

**MCP Request Format:**
```json
{
  "method": "tools/call",
  "params": {
    "name": "vs_connect_instance",
    "arguments": {
      "processId": 15420
    }
  },
  "id": 2
}
```

**Response Format:**
```json
{
  "result": {
    "success": true,
    "data": {
      "instance": {
        "processId": 15420,
        "version": "17.8.0",
        "solutionPath": "C:\\Dev\\MyProject\\MyProject.sln",
        "connected": true,
        "connectionTime": "2025-08-12T16:45:15Z"
      },
      "connected": true,
      "timestamp": "2025-08-12T16:45:15Z"
    }
  }
}
```

**Claude Code Usage:**
```
"Connect to Visual Studio process 15420"
"Connect to the VS instance running MyProject"
"Use the Visual Studio with process ID 15420"
"Switch to the Visual Studio instance with the web app"
```

**Real-World Example:**
```
User: "Connect to the Visual Studio instance running MyWebApp"  
Claude: ✅ Successfully connected to Visual Studio instance!

**Connected Instance:**
- Process ID: 15420
- Version: 17.8.0 Professional
- Solution: MyWebApp.sln
- Connection established at 16:45:15

I'm now ready to automate build, debugging, and other tasks for this instance. What would you like me to do?
```

**Security Validation:**
- Process ID range validation (1-65535)
- Visual Studio process type verification
- Active process confirmation

**Error Scenarios:**
```json
// Invalid process ID
{
  "result": {
    "success": false,
    "errorMessage": "Process ID validation failed: INVALID_PROCESS_TYPE",
    "errorCode": "INVALID_PROCESS_TYPE", 
    "errorDetails": "Process 'notepad' (PID: 12345) is not a Visual Studio instance"
  }
}

// Process not found
{
  "result": {
    "success": false,
    "errorMessage": "No process found with the specified process ID",
    "errorCode": "PROCESS_NOT_FOUND",
    "errorDetails": "Process with ID 15420 is not running"
  }
}
```

---

### 3. `vs_open_solution` - Open Solution Files

**Purpose:** Open a solution file in the connected Visual Studio instance

**Parameters:**
- `solutionPath` (required, string): Full absolute path to .sln file

**MCP Request Format:**
```json
{
  "method": "tools/call",
  "params": {
    "name": "vs_open_solution",
    "arguments": {
      "solutionPath": "C:\\Projects\\MyWebApp\\MyWebApp.sln"
    }
  },
  "id": 3
}
```

**Response Format:**
```json
{
  "result": {
    "success": true,
    "data": {
      "solution": {
        "name": "MyWebApp",
        "path": "C:\\Projects\\MyWebApp\\MyWebApp.sln",
        "projectCount": 5,
        "loadTime": "00:00:03.245",
        "projects": [
          "MyWebApp.Core",
          "MyWebApp.Web", 
          "MyWebApp.Tests",
          "MyWebApp.Integration.Tests",
          "MyWebApp.Shared"
        ]
      },
      "opened": true,
      "timestamp": "2025-08-12T16:46:00Z"
    }
  }
}
```

**Claude Code Usage:**
```
"Open the solution at C:\\Projects\\MyWebApp\\MyWebApp.sln"
"Load the MyWebApp solution file"
"Switch to the e-commerce solution in the projects folder"
"Open C:\\Dev\\MobileApp\\MobileApp.sln"
```

**Real-World Example:**
```
User: "Open the e-commerce solution in my projects folder"
Claude: ✅ Successfully opened the solution!

**MyWebApp Solution Loaded:**
- Path: C:\Projects\MyWebApp\MyWebApp.sln
- Projects: 5 total
  - MyWebApp.Core (Class Library)
  - MyWebApp.Web (ASP.NET Core App)
  - MyWebApp.Tests (Unit Tests)  
  - MyWebApp.Integration.Tests (Integration Tests)
  - MyWebApp.Shared (Shared Models)
- Load time: 3.2 seconds

Ready for build operations, debugging, and project analysis.
```

**Comprehensive Security Features:**
- **Path Traversal Protection:** Blocks `../` and `~` references
- **Extension Validation:** Only `.sln` files accepted
- **File Existence Check:** Prevents information disclosure attacks
- **Absolute Path Requirement:** Relative paths rejected for security

**Security Error Examples:**
```json
// Path traversal attempt
{
  "result": {
    "success": false,
    "errorMessage": "Path contains potential traversal attempts",
    "errorCode": "PATH_TRAVERSAL_DETECTED",
    "errorDetails": "Paths cannot contain '..' or '~' references for security"
  }
}

// Wrong file extension  
{
  "result": {
    "success": false,
    "errorMessage": "File must have .sln extension",
    "errorCode": "INVALID_FILE_EXTENSION",
    "errorDetails": "Expected .sln but got .csproj"
  }
}

// File not found
{
  "result": {
    "success": false,
    "errorMessage": "Specified file does not exist", 
    "errorCode": "FILE_NOT_FOUND",
    "errorDetails": "File not found: C:\\Projects\\NonExistent\\App.sln"
  }
}
```

---

### 4. `vs_build_solution` - Build Solutions

**Purpose:** Build the currently loaded solution with comprehensive result capture

**Parameters:**
- `configuration` (optional, string): "Debug" or "Release" (default: "Debug")

**MCP Request Format:**
```json
{
  "method": "tools/call",
  "params": {
    "name": "vs_build_solution", 
    "arguments": {
      "configuration": "Release"
    }
  },
  "id": 4
}
```

**Response Format:**
```json
{
  "result": {
    "success": true,
    "data": {
      "buildResult": {
        "success": true,
        "configuration": "Release", 
        "duration": "00:01:23.567",
        "projectsBuilt": 5,
        "projectsFailed": 0,
        "totalWarnings": 2,
        "totalErrors": 0,
        "outputPath": "bin\\Release\\net8.0-windows",
        "timestamp": "2025-08-12T16:47:30Z",
        "warnings": [
          {
            "project": "MyWebApp.Core",
            "file": "Services\\UserService.cs",
            "line": 42,
            "column": 15,
            "code": "CS8618",
            "message": "Non-nullable field must contain a non-null value when exiting constructor"
          }
        ],
        "errors": []
      },
      "timestamp": "2025-08-12T16:47:30Z"
    }
  }
}
```

**Claude Code Usage:**
```
"Build the current solution"
"Build in Release configuration"  
"Compile the solution and show me any errors"
"Build the solution in Debug mode and report status"
"Perform a clean build of the current solution"
```

**Real-World Example:**
```
User: "Build the solution in Release mode and tell me if there are any issues"
Claude: ✅ Build completed successfully in Release mode!

**Build Summary:**
- Configuration: Release
- Duration: 1 minute 23 seconds
- Projects built: 5/5 ✅
- Errors: 0 ✅
- Warnings: 2 ⚠️

**Warnings to Review:**
1. **MyWebApp.Core/Services/UserService.cs:42** 
   - CS8618: Non-nullable field must contain a non-null value when exiting constructor
   
2. **MyWebApp.Web/Controllers/HomeController.cs:156**
   - CS0168: Variable 'result' is declared but never used

The build succeeded, but you may want to address these warnings before deployment. The warnings are related to nullable reference types and unused variables.
```

**Configuration Validation:**
- Whitelist-based validation: "Debug", "Release", "DebugAnyCPU", "ReleaseAnyCPU"
- Case-insensitive matching with proper casing returned
- Default to "Debug" if not specified

**Build Failure Example:**
```json
{
  "result": {
    "success": true,
    "data": {
      "buildResult": {
        "success": false,
        "configuration": "Debug",
        "projectsBuilt": 2,
        "projectsFailed": 3,
        "totalErrors": 5,
        "errors": [
          {
            "project": "MyWebApp.Web",
            "file": "Controllers\\ApiController.cs", 
            "line": 28,
            "column": 12,
            "code": "CS0103",
            "message": "The name 'InvalidMethod' does not exist in the current context"
          }
        ]
      }
    }
  }
}
```

---

### 5. `vs_get_projects` - Enumerate Solution Projects

**Purpose:** List all projects in the currently loaded solution with metadata

**Parameters:** None

**MCP Request Format:**
```json
{
  "method": "tools/call",
  "params": {
    "name": "vs_get_projects",
    "arguments": {}
  },
  "id": 5
}
```

**Response Format:**
```json
{
  "result": {
    "success": true,
    "data": {
      "projects": [
        {
          "name": "MyWebApp.Core",
          "path": "C:\\Projects\\MyWebApp\\src\\MyWebApp.Core\\MyWebApp.Core.csproj",
          "type": "Class Library",
          "targetFramework": "net8.0",
          "outputType": "Library",
          "isStartupProject": false
        },
        {
          "name": "MyWebApp.Web", 
          "path": "C:\\Projects\\MyWebApp\\src\\MyWebApp.Web\\MyWebApp.Web.csproj",
          "type": "ASP.NET Core Web Application",
          "targetFramework": "net8.0",
          "outputType": "Exe", 
          "isStartupProject": true
        },
        {
          "name": "MyWebApp.Tests",
          "path": "C:\\Projects\\MyWebApp\\tests\\MyWebApp.Tests\\MyWebApp.Tests.csproj",
          "type": "Unit Test Project",
          "targetFramework": "net8.0",
          "outputType": "Library",
          "isStartupProject": false
        }
      ],
      "count": 3,
      "solutionName": "MyWebApp",
      "timestamp": "2025-08-12T16:48:00Z"
    }
  }
}
```

**Claude Code Usage:**
```
"Show me all projects in this solution"
"List the projects with their target frameworks"
"What projects are in the current solution?"
"Show me the project structure and types"
"Which project is the startup project?"
```

**Real-World Example:**
```
User: "Show me the project structure of this solution"
Claude: Here's the complete project structure for **MyWebApp** solution:

**Core Projects:**
1. **MyWebApp.Core** (Class Library)
   - Target: .NET 8.0
   - Type: Shared business logic
   - Path: src\MyWebApp.Core\

2. **MyWebApp.Web** (ASP.NET Core) ⭐ *Startup Project*
   - Target: .NET 8.0  
   - Type: Web application
   - Path: src\MyWebApp.Web\

**Test Projects:**
3. **MyWebApp.Tests** (Unit Tests)
   - Target: .NET 8.0
   - Type: Unit test library
   - Path: tests\MyWebApp.Tests\

4. **MyWebApp.Integration.Tests** (Integration Tests)
   - Target: .NET 8.0
   - Type: Integration test library
   - Path: tests\MyWebApp.Integration.Tests\

**Shared Libraries:**
5. **MyWebApp.Shared** (Class Library)
   - Target: .NET 8.0
   - Type: Shared models and utilities
   - Path: src\MyWebApp.Shared\

**Total:** 5 projects (1 startup, 2 libraries, 2 test projects)
```

---

## 🔧 Integration Patterns with Claude Code

### Pattern 1: Daily Development Startup
```
User: "Set up my development environment"
Claude: I'll get your Visual Studio environment ready:

1. 🔍 Discovering Visual Studio instances...
   Found 2 instances running
   
2. 🔌 Connecting to MyWebApp instance (PID: 15420)...
   Connected successfully
   
3. 🏗️ Building current solution in Debug mode...
   Build completed: 5 projects, 0 errors, 2 warnings
   
4. 📋 Current project structure:
   - MyWebApp.Web (Startup) ✅
   - MyWebApp.Core (Library) ✅  
   - MyWebApp.Tests (Tests) ✅

Your development environment is ready! Build successful with minor warnings to review.
```

### Pattern 2: Pre-Deployment Checklist
```
User: "Prepare for deployment - check everything is ready"
Claude: Running pre-deployment checklist:

✅ **Release Build**: Building solution in Release mode...
   - Duration: 1m 23s
   - Projects: 5/5 successful
   - Errors: 0
   - Warnings: 1 (non-critical)

✅ **Project Structure**: All projects present and configured
   - Web app: Ready for deployment
   - Core libraries: Built successfully
   - Tests: Available for CI/CD

✅ **Solution Health**: No critical issues detected

🚀 **Ready for deployment!** Only 1 minor warning about unused variable.
```

### Pattern 3: Multi-Solution Management
```
User: "I'm working on both the web app and mobile app - help me manage both"
Claude: Managing multiple solutions:

**Current Sessions:**
1. **MyWebApp** (PID: 15420)
   - Status: Connected ✅
   - Last build: Success (Release)
   - Projects: 5
   
2. **MobileApp** (PID: 18750) 
   - Status: Available
   - Solution: MobileApp.sln
   - Projects: 8

Would you like me to:
- Switch between solutions
- Build both solutions
- Compare project structures
- Run specific operations on either solution
```

---

## 🛡️ Security and Error Handling

### Security Features Summary

**Input Validation:**
- Process ID range validation (1-65535)
- Visual Studio process type verification  
- Path traversal attack prevention
- File extension validation
- Configuration parameter whitelisting

**Process Security:**
- Only Visual Studio processes can be targeted
- Process existence and health verification
- Connection state monitoring and cleanup
- Resource limit enforcement

**File System Security:**
- Absolute path requirement
- Extension-based access control (.sln only)
- File existence verification without content disclosure
- Path sanitization and normalization

### Comprehensive Error Catalog

| Error Code | Scenario | User-Friendly Message |
|------------|----------|----------------------|
| `VS_NOT_FOUND` | No VS instances running | "No Visual Studio instances found. Please start Visual Studio 2022." |
| `INVALID_PROCESS_TYPE` | Targeting non-VS process | "Can only connect to Visual Studio processes for security." |
| `PROCESS_NOT_FOUND` | Process ID doesn't exist | "Visual Studio process not found. It may have closed." |
| `PATH_TRAVERSAL_DETECTED` | Security violation in path | "Invalid path detected for security. Use absolute paths only." |
| `INVALID_FILE_EXTENSION` | Wrong file type | "Only solution files (.sln) can be opened." |
| `FILE_NOT_FOUND` | Solution file missing | "Solution file not found. Check the path and try again." |
| `INVALID_CONFIGURATION` | Bad build config | "Invalid build configuration. Use 'Debug' or 'Release'." |
| `NO_SOLUTION_LOADED` | No solution open | "No solution loaded. Open a solution first." |
| `CONNECTION_FAILED` | VS connection lost | "Lost connection to Visual Studio. It may have closed." |

### Error Recovery Patterns

**Automatic Recovery:**
```json
// Health monitoring detects disconnection
{
  "result": {
    "success": false,
    "errorMessage": "Connection lost to Visual Studio instance",
    "errorCode": "CONNECTION_LOST",
    "errorDetails": "Process 15420 is no longer responding",
    "recovery": {
      "automatic": true,
      "action": "Cleaned up dead connection",
      "suggestion": "Use vs_list_instances to find available instances"
    }
  }
}
```

**Graceful Degradation:**
```json
// Service continues despite individual failures
{
  "result": {
    "success": true,
    "data": {
      "instances": [], // Empty but successful response
      "count": 0,
      "message": "No Visual Studio instances currently running"
    },
    "timestamp": "2025-08-12T16:45:00Z"
  }
}
```

---

## ⚡ Performance Specifications

### Response Time Targets
- **vs_list_instances**: < 2 seconds (process enumeration)
- **vs_connect_instance**: < 1 second (COM connection)  
- **vs_open_solution**: < 10 seconds (depends on solution size)
- **vs_build_solution**: Variable (depends on project size)
- **vs_get_projects**: < 2 seconds (solution parsing)

### Memory Usage
- **Baseline**: ~50MB (COM interop overhead)
- **Per connection**: +5MB (DTE object and metadata)
- **Weak references**: Automatic cleanup prevents accumulation
- **Health monitoring**: Periodic cleanup of dead references

### Scalability Limits  
- **Concurrent connections**: Up to 5 Visual Studio instances
- **Request queue**: 10 concurrent MCP operations
- **Memory ceiling**: 200MB total (with monitoring and cleanup)
- **Connection lifetime**: Automatic health checking every 30 seconds

---

## 🧪 Testing and Validation

### Manual Testing Checklist

**Basic Functionality:**
- [ ] `vs_list_instances` returns running VS processes
- [ ] `vs_connect_instance` establishes connection
- [ ] `vs_open_solution` loads solution files  
- [ ] `vs_build_solution` compiles successfully
- [ ] `vs_get_projects` enumerates project structure

**Security Validation:**
- [ ] Invalid process IDs are rejected
- [ ] Path traversal attempts are blocked
- [ ] Only .sln files can be opened
- [ ] Non-VS processes cannot be targeted
- [ ] Malformed requests return proper errors

**Error Handling:**
- [ ] Missing Visual Studio handled gracefully
- [ ] Invalid parameters return clear errors
- [ ] Connection failures cleanup properly
- [ ] Build failures captured completely
- [ ] Resource cleanup prevents memory leaks

### Automated Test Examples

```bash
# Test suite for MCP tools
./run-tests.sh

# Individual tool tests
echo '{"method":"tools/list"}' | vsmcp | jq '.result.tools | length' # Should return 5

# Security test - should fail
echo '{"method":"tools/call","params":{"name":"vs_connect_instance","arguments":{"processId":-1}}}' | vsmcp

# Performance test - should complete within 2 seconds
time echo '{"method":"tools/call","params":{"name":"vs_list_instances","arguments":{}}}' | vsmcp
```

---

## 🔗 Integration Resources

### Claude Code Configuration
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

### Development Resources
- **Setup Guide**: `DEVELOPMENT_SETUP.md`
- **Troubleshooting**: `TROUBLESHOOTING.md` 
- **Integration Guide**: `CLAUDE_CODE_INTEGRATION.md`
- **Architecture Docs**: `docs/architecture/system-architecture.md`

### Support and Community
- **GitHub Issues**: Bug reports and feature requests
- **GitHub Discussions**: Usage questions and community support
- **Documentation PRs**: Improvements and corrections welcome
- **Code Contributions**: Follow `CONTRIBUTING.md` guidelines

---

**This enhanced API reference provides working examples, real-world integration patterns, and comprehensive error handling for all 5 implemented MCP tools in the Visual Studio MCP Server Phase 2 release.**