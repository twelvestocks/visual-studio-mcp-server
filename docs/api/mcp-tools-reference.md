# MCP Tools Reference

This document provides comprehensive documentation for all MCP tools provided by the Visual Studio MCP Server. Each tool includes purpose, parameters, return values, and usage examples with Claude Code.

## Tool Categories

- [Visual Studio Management](#visual-studio-management) - Instance discovery and connection
- [Build Automation](#build-automation) - Solution and project building
- [Debug Control](#debug-control) - Debugging session management
- [XAML Designer](#xaml-designer) - XAML designer automation
- [Visual Capture](#visual-capture) - Screenshot and imaging tools
- [Project Analysis](#project-analysis) - Code and project inspection

---

## Visual Studio Management

### `vs_list_instances`

**Purpose:** Discover and list all running Visual Studio 2022 instances

**Parameters:** None

**Returns:**
```json
{
  "success": true,
  "data": {
    "instances": [
      {
        "id": "vs_instance_12345",
        "processId": 12345,
        "version": "17.8.3",
        "edition": "Professional",
        "solutionPath": "C:\\Projects\\MyApp\\MyApp.sln",
        "solutionName": "MyApp",
        "isDebugging": false,
        "windowTitle": "MyApp - Microsoft Visual Studio"
      }
    ],
    "totalCount": 1
  }
}
```

**Claude Code Example:**
```
List all running Visual Studio instances to see which projects are currently open
```

**Error Responses:**
- `VS_NOT_FOUND` - No Visual Studio instances are running
- `COM_ERROR` - COM interop failure during discovery

---

### `vs_connect_instance`

**Purpose:** Connect to a specific Visual Studio instance for automation

**Parameters:**
- `instance_id` (required): Instance identifier from `vs_list_instances`
- `timeout_ms` (optional): Connection timeout in milliseconds (default: 5000)

**Returns:**
```json
{
  "success": true,
  "data": {
    "instanceId": "vs_instance_12345",
    "connected": true,
    "solutionPath": "C:\\Projects\\MyApp\\MyApp.sln",
    "projectCount": 5,
    "activeDocument": "MainWindow.xaml.cs",
    "buildConfiguration": "Debug"
  }
}
```

**Claude Code Example:**
```
Connect to Visual Studio instance vs_instance_12345 so I can automate build and debugging tasks
```

**Error Responses:**
- `INSTANCE_NOT_FOUND` - Specified instance ID not found
- `CONNECTION_TIMEOUT` - Connection attempt timed out
- `ACCESS_DENIED` - Insufficient permissions for COM interop

---

## Build Automation

### `vs_build_solution`

**Purpose:** Build the entire solution and capture detailed build results

**Parameters:**
- `solution_path` (optional): Path to solution file (uses currently loaded solution if not specified)
- `configuration` (optional): Build configuration - "Debug" or "Release" (default: "Debug")
- `platform` (optional): Target platform - "Any CPU", "x64", "x86" (default: "Any CPU")
- `clean_build` (optional): Perform clean build (default: false)

**Returns:**
```json
{
  "success": true,
  "data": {
    "buildSucceeded": true,
    "projectsBuilt": 5,
    "projectsFailed": 0,
    "buildTime": "00:01:23",
    "outputPath": "bin\\Debug\\net8.0-windows",
    "warnings": [
      {
        "file": "Services\\UserService.cs",
        "line": 42,
        "column": 15,
        "code": "CS8618",
        "message": "Non-nullable field must contain a non-null value when exiting constructor"
      }
    ],
    "errors": [],
    "buildOutput": "Build started at 2025-08-12 16:30:25...\n1>------ Build started: Project: MyApp.Core ------\n..."
  }
}
```

**Claude Code Example:**
```
Build the current solution in Release mode and show me any errors or warnings that need attention
```

**Error Responses:**
- `NO_SOLUTION_LOADED` - No solution is currently loaded in Visual Studio
- `BUILD_FAILED` - Build completed with errors
- `PROJECT_NOT_FOUND` - Solution file not found at specified path

---

### `vs_build_project`

**Purpose:** Build a specific project within the solution

**Parameters:**
- `project_path` (required): Path to project file (.csproj, .vbproj, etc.)
- `configuration` (optional): Build configuration (default: "Debug")
- `platform` (optional): Target platform (default: "Any CPU")

**Returns:**
```json
{
  "success": true,
  "data": {
    "projectName": "MyApp.Core",
    "buildSucceeded": true,
    "buildTime": "00:00:15",
    "outputPath": "bin\\Debug\\net8.0",
    "assemblyPath": "bin\\Debug\\net8.0\\MyApp.Core.dll",
    "warnings": [],
    "errors": []
  }
}
```

**Claude Code Example:**
```
Build just the MyApp.Core project to test my recent changes without building the entire solution
```

---

## Debug Control

### `vs_start_debugging`

**Purpose:** Start a debugging session for the current startup project

**Parameters:**
- `project_path` (optional): Specific project to debug (uses startup project if not specified)
- `arguments` (optional): Command line arguments to pass to the debugged application
- `working_directory` (optional): Working directory for the debugged application

**Returns:**
```json
{
  "success": true,
  "data": {
    "debugSessionId": "debug_session_98765",
    "processId": 98765,
    "processName": "MyApp.exe",
    "debugMode": "Mixed",
    "startupProject": "MyApp.UI",
    "breakpointsSet": 12,
    "isRunning": true
  }
}
```

**Claude Code Example:**
```
Start debugging the application with command line arguments "--test-mode --verbose" so I can trace the startup sequence
```

**Error Responses:**
- `NO_STARTUP_PROJECT` - No startup project configured
- `BUILD_REQUIRED` - Project needs to be built before debugging
- `DEBUG_START_FAILED` - Failed to start debugging session

---

### `vs_stop_debugging`

**Purpose:** Stop the current debugging session

**Parameters:** None

**Returns:**
```json
{
  "success": true,
  "data": {
    "sessionStopped": true,
    "processId": 98765,
    "sessionDuration": "00:05:42",
    "breakpointsHit": 8,
    "exceptionsThrown": 0
  }
}
```

**Claude Code Example:**
```
Stop the current debugging session and show me a summary of what happened during the debug run
```

---

### `vs_set_breakpoint`

**Purpose:** Set a breakpoint at a specific file and line number

**Parameters:**
- `file_path` (required): Full path to source file
- `line_number` (required): Line number for breakpoint (1-based)
- `condition` (optional): Breakpoint condition expression
- `enabled` (optional): Whether breakpoint is enabled (default: true)

**Returns:**
```json
{
  "success": true,
  "data": {
    "breakpointId": "bp_12345",
    "filePath": "C:\\Projects\\MyApp\\Services\\UserService.cs",
    "lineNumber": 42,
    "condition": "userId > 100",
    "enabled": true,
    "resolved": true
  }
}
```

**Claude Code Example:**
```
Set a breakpoint at line 42 in UserService.cs with condition "userId > 100" to debug specific user scenarios
```

---

### `vs_get_debug_state`

**Purpose:** Get current debugging session state and runtime information

**Parameters:** None

**Returns:**
```json
{
  "success": true,
  "data": {
    "isDebugging": true,
    "processId": 98765,
    "processName": "MyApp.exe",
    "debugMode": "Mixed",
    "currentBreakpoint": {
      "filePath": "C:\\Projects\\MyApp\\Services\\UserService.cs",
      "lineNumber": 42,
      "functionName": "ProcessUser"
    },
    "callStack": [
      {
        "level": 0,
        "functionName": "ProcessUser",
        "fileName": "UserService.cs",
        "lineNumber": 42
      },
      {
        "level": 1,
        "functionName": "HandleRequest",
        "fileName": "RequestHandler.cs", 
        "lineNumber": 156
      }
    ],
    "localVariables": [
      {
        "name": "userId",
        "value": "12345",
        "type": "int"
      },
      {
        "name": "user",
        "value": "{ Id=12345, Name=\"John Doe\" }",
        "type": "User"
      }
    ]
  }
}
```

**Claude Code Example:**
```
Show me the current debug state including call stack and local variables so I can understand what's happening at this breakpoint
```

---

## XAML Designer

### `vs_capture_xaml_designer`

**Purpose:** Capture high-quality screenshots of XAML designer surfaces with optional annotations and metadata

**Parameters:**
- `target_window` (optional): Specific designer window to capture (auto-detects active XAML designer if not specified)
- `include_annotations` (optional): Add element highlighting and metadata overlays (default: true)
- `capture_quality` (optional): Image quality setting - "standard" or "high" (default: "high")
- `output_path` (optional): Custom output location for capture files
- `filename_template` (optional): Custom filename template with variables like ${timestamp}, ${window}

**Returns:**
```json
{
  "success": true,
  "data": {
    "image_path": "/temp/xaml_capture_20250814_143022.png",
    "metadata": {
      "window_title": "MainWindow.xaml [Design]",
      "capture_size": "1920x1080",
      "elements_annotated": 15,
      "capture_timestamp": "2025-08-14T14:30:22Z",
      "xaml_file": "MainWindow.xaml",
      "designer_mode": "Design"
    },
    "annotations": [
      {
        "element_name": "LoginButton",
        "element_type": "Button",
        "bounds": { "x": 100, "y": 200, "width": 120, "height": 35 },
        "highlight_color": "#FF0000"
      }
    ]
  }
}
```

**Claude Code Example:**
```
Capture the active XAML designer with annotations to show element boundaries and properties for UI review
```

**Error Responses:**
- `NO_XAML_DESIGNERS` - No XAML designer windows found
- `DESIGNER_NOT_ACTIVE` - Specified XAML designer is not currently active
- `CAPTURE_FAILED` - Screenshot capture operation failed
- `PATH_VALIDATION_FAILED` - Output path failed security validation

---

### `vs_get_xaml_elements`

**Purpose:** Extract and analyse XAML element hierarchy and properties from files or active designers

**Parameters:**
- `xaml_file_path` (required): Path to XAML file to analyse
- `include_properties` (optional): Include detailed element properties (default: true)
- `filter_by_type` (optional): Filter by element type (e.g., "Button", "TextBox")
- `max_depth` (optional): Limit hierarchy depth for performance (default: unlimited)

**Returns:**
```json
{
  "success": true,
  "data": {
    "elements": [
      {
        "element_type": "Grid",
        "element_name": "MainGrid",
        "properties": {
          "Background": "White",
          "Margin": "10",
          "HorizontalAlignment": "Stretch"
        },
        "children_count": 3,
        "binding_count": 0,
        "hierarchy_level": 1
      },
      {
        "element_type": "Button",
        "element_name": "LoginButton",
        "properties": {
          "Content": "Login",
          "Width": "120",
          "Height": "35",
          "Command": "{Binding LoginCommand}"
        },
        "children_count": 0,
        "binding_count": 1,
        "hierarchy_level": 2
      }
    ],
    "total_elements": 15,
    "analysis_timestamp": "2025-08-14T14:30:22Z",
    "xaml_file": "MainWindow.xaml"
  }
}
```

**Claude Code Example:**
```
Analyse MainWindow.xaml and show me all Button elements with their data binding information
```

**Error Responses:**
- `XAML_FILE_NOT_FOUND` - Specified XAML file not found
- `XAML_PARSING_FAILED` - XAML file contains syntax errors
- `PATH_VALIDATION_FAILED` - File path failed security validation

---

### `vs_modify_xaml_element`

**Purpose:** Modify XAML element properties with immediate visual feedback and backup creation

**Parameters:**
- `xaml_file_path` (required): Path to XAML file
- `element_selector` (required): Element selector (name, XPath, or CSS-like selector)
- `property_name` (required): Property to modify
- `property_value` (required): New property value
- `create_backup` (optional): Create backup before modification (default: true)

**Returns:**
```json
{
  "success": true,
  "data": {
    "modified_elements": 1,
    "backup_created": "/backups/MainWindow_20250814_143022.xaml.bak",
    "changes": [
      {
        "element": "LoginButton",
        "property": "Content",
        "old_value": "Login",
        "new_value": "Sign In"
      }
    ],
    "modification_timestamp": "2025-08-14T14:30:22Z"
  }
}
```

**Claude Code Example:**
```
Change the LoginButton content from "Login" to "Sign In" and create a backup of the original file
```

**Error Responses:**
- `ELEMENT_NOT_FOUND` - Specified element not found in XAML
- `PROPERTY_MODIFICATION_FAILED` - Property could not be modified
- `BACKUP_CREATION_FAILED` - Backup file could not be created
- `FILE_READ_ONLY` - XAML file is read-only

---

### `vs_analyse_bindings`

**Purpose:** Analyse, validate, and optimise XAML data binding expressions with performance recommendations

**Parameters:**
- `xaml_file_path` (required): Path to XAML file
- `check_performance` (optional): Include performance analysis and recommendations (default: false)
- `validate_paths` (optional): Validate binding paths and expressions (default: true)
- `include_statistics` (optional): Include comprehensive binding statistics (default: true)

**Returns:**
```json
{
  "success": true,
  "data": {
    "bindings": [
      {
        "element": "NameTextBox",
        "property": "Text",
        "binding_type": "Binding",
        "path": "Customer.Name",
        "mode": "TwoWay",
        "validation_result": "Valid",
        "performance_score": 8.5,
        "line_number": 42,
        "recommendations": []
      },
      {
        "element": "StatusLabel",
        "property": "Content",
        "binding_type": "StaticResource",
        "resource_key": "StatusTemplate",
        "validation_result": "Warning",
        "performance_score": 6.0,
        "line_number": 55,
        "recommendations": ["Consider using DynamicResource for runtime updates"]
      }
    ],
    "statistics": {
      "total_bindings": 23,
      "valid_bindings": 21,
      "warnings": 2,
      "errors": 0,
      "performance_issues": 1,
      "binding_types": {
        "Binding": 15,
        "StaticResource": 5,
        "DynamicResource": 2,
        "x:Bind": 1
      }
    },
    "recommendations": [
      "Consider using OneWay binding for read-only CustomerID property",
      "Resource key 'StatusTemplate' not found in current resource dictionaries"
    ]
  }
}
```

**Claude Code Example:**
```
Analyse all data bindings in MainWindow.xaml and provide performance recommendations for optimisation
```

**Error Responses:**
- `XAML_PARSING_FAILED` - XAML file contains syntax errors
- `BINDING_ANALYSIS_FAILED` - Error during binding expression analysis
- `PATH_VALIDATION_FAILED` - File path failed security validation

---

### `vs_find_xaml_elements_by_name`

**Purpose:** Search for specific elements by name across XAML files

**Parameters:**
- `xaml_file_path` (required): Path to XAML file
- `element_name` (required): Name to search for (supports wildcards)
- `case_sensitive` (optional): Case-sensitive search (default: false)

**Returns:**
```json
{
  "success": true,
  "data": {
    "matching_elements": [
      {
        "element_type": "Button",
        "element_name": "LoginButton",
        "line_number": 25,
        "properties": {
          "Content": "Login",
          "Command": "{Binding LoginCommand}"
        }
      }
    ],
    "total_matches": 1,
    "search_term": "LoginButton"
  }
}
```

**Claude Code Example:**
```
Find all elements with names containing "Login" in MainWindow.xaml for refactoring purposes
```

---

### `vs_get_xaml_binding_statistics`

**Purpose:** Get comprehensive statistics about data binding usage in a XAML file

**Parameters:**
- `xaml_file_path` (required): Path to XAML file

**Returns:**
```json
{
  "success": true,
  "data": {
    "file_path": "MainWindow.xaml",
    "total_bindings": 23,
    "data_bindings": 15,
    "static_resource_bindings": 5,
    "dynamic_resource_bindings": 2,
    "relative_source_bindings": 1,
    "validation_errors": 0,
    "validation_warnings": 2,
    "performance_issues": 1,
    "elements_with_bindings": 12,
    "analysis_timestamp": "2025-08-14T14:30:22Z"
  }
}
```

**Claude Code Example:**
```
Show me binding usage statistics for MainWindow.xaml to understand data binding patterns
```

---

## Visual Capture

### `vs_capture_window`

**Purpose:** Capture a screenshot of any Visual Studio window or panel

**Parameters:**
- `window_title` (optional): Specific window title to capture (captures main VS window if not specified)
- `include_chrome` (optional): Include window borders and title bar (default: false)
- `image_quality` (optional): JPEG quality 1-100 (default: 85)

**Returns:**
```json
{
  "success": true,
  "data": {
    "imageData": "iVBORw0KGgoAAAANSUhEUgAA...base64_encoded_image...",
    "imageFormat": "PNG",
    "width": 1920,
    "height": 1080,
    "fileSizeBytes": 425600,
    "windowTitle": "MyApp - Microsoft Visual Studio",
    "captureRegion": {
      "x": 0,
      "y": 0,
      "width": 1920,
      "height": 1080
    },
    "captureTimestamp": "2025-08-12T16:45:30.123Z"
  }
}
```

**Claude Code Example:**
```
Capture the entire Visual Studio window including all panels so I can see the current development environment layout
```

---

### `vs_capture_code_editor`

**Purpose:** Capture a screenshot of the active code editor window

**Parameters:**
- `include_line_numbers` (optional): Include line numbers in capture (default: true)
- `highlight_selection` (optional): Highlight current text selection (default: false)

**Returns:**
```json
{
  "success": true,
  "data": {
    "imageData": "iVBORw0KGgoAAAANSUhEUgAA...base64_encoded_image...",
    "imageFormat": "PNG",
    "activeFile": "UserService.cs",
    "language": "CSharp",
    "lineRange": {
      "start": 1,
      "end": 50,
      "current": 25
    },
    "selectionInfo": {
      "hasSelection": true,
      "selectedText": "public class UserService",
      "startLine": 12,
      "endLine": 12
    }
  }
}
```

**Claude Code Example:**
```
Capture the current code editor showing UserService.cs with line numbers visible for code review
```

---

## Project Analysis

### `vs_get_project_info`

**Purpose:** Get detailed information about a project's structure and configuration

**Parameters:**
- `project_path` (required): Full path to project file
- `include_references` (optional): Include project and NuGet references (default: true)
- `include_files` (optional): Include source file listing (default: false)

**Returns:**
```json
{
  "success": true,
  "data": {
    "projectName": "MyApp.Core",
    "projectPath": "C:\\Projects\\MyApp\\src\\MyApp.Core\\MyApp.Core.csproj",
    "targetFramework": "net8.0",
    "outputType": "Library",
    "packageReferences": [
      {
        "name": "Microsoft.Extensions.DependencyInjection",
        "version": "8.0.0"
      },
      {
        "name": "Newtonsoft.Json",
        "version": "13.0.3"
      }
    ],
    "projectReferences": [
      {
        "name": "MyApp.Shared",
        "path": "..\\MyApp.Shared\\MyApp.Shared.csproj"
      }
    ],
    "sourceFileCount": 25,
    "totalLinesOfCode": 2450
  }
}
```

**Claude Code Example:**
```
Analyze the MyApp.Core project to show me its dependencies and configuration for architecture review
```

---

### `vs_get_solution_info`

**Purpose:** Get comprehensive information about the loaded solution

**Parameters:**
- `include_build_config` (optional): Include build configuration details (default: true)
- `include_project_dependencies` (optional): Include project dependency graph (default: false)

**Returns:**
```json
{
  "success": true,
  "data": {
    "solutionName": "MyApp",
    "solutionPath": "C:\\Projects\\MyApp\\MyApp.sln",
    "projectCount": 8,
    "projects": [
      {
        "name": "MyApp.Core",
        "type": "Library",
        "targetFramework": "net8.0",
        "buildOrder": 1
      },
      {
        "name": "MyApp.UI",
        "type": "WinExe",
        "targetFramework": "net8.0-windows",
        "buildOrder": 2,
        "isStartupProject": true
      }
    ],
    "buildConfigurations": ["Debug", "Release"],
    "platforms": ["Any CPU", "x64"]
  }
}
```

**Claude Code Example:**
```
Show me the complete solution structure including all projects and their build dependencies
```

---

## Common Response Patterns

### Success Response
```json
{
  "success": true,
  "data": { /* tool-specific data */ }
}
```

### Error Response
```json
{
  "success": false,
  "errorMessage": "Human-readable error description",
  "errorCode": "ERROR_CODE_CONSTANT",
  "errorContext": {
    "parameter": "value that caused the error",
    "suggestion": "Try checking if Visual Studio is running"
  }
}
```

### Common Error Codes

| Error Code | Description | Common Causes |
|------------|-------------|---------------|
| `VS_NOT_FOUND` | Visual Studio instance not found | VS not running, incorrect instance ID |
| `COM_ERROR` | COM interop operation failed | Permission issues, VS crashed |
| `FILE_NOT_FOUND` | Specified file path not found | Wrong path, file moved or deleted |
| `BUILD_FAILED` | Build operation failed | Compilation errors, missing dependencies |
| `ACCESS_DENIED` | Insufficient permissions | Need admin rights, VS access restricted |
| `TIMEOUT` | Operation timed out | Long-running operation, slow system |
| `INVALID_PARAMETER` | Invalid parameter value | Wrong data type, out of range value |

## Usage Best Practices

### 1. Instance Management
- Always call `vs_list_instances` before other operations
- Use `vs_connect_instance` to establish connection before automation
- Handle connection failures gracefully

### 2. Build Operations
- Check solution is loaded before building
- Handle build failures and provide meaningful error context
- Use appropriate build configuration for the task

### 3. Debugging Workflows
- Ensure project is built before starting debugging
- Set meaningful breakpoints with conditions when needed
- Always stop debugging sessions cleanly

### 4. Screenshot Capture
- Be mindful of image size and quality settings
- Capture at appropriate times when UI is stable
- Clean up temporary image data promptly

### 5. Error Handling
- Always check the `success` field in responses
- Provide contextual error information to users
- Implement retry logic for transient failures

---

**Next:** Review [Service Interfaces](service-interfaces.md) for detailed API contracts.