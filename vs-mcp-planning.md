# PLANNING.md - Visual Studio MCP Server

### Project Vision
**Project Name:** Visual Studio MCP Server  
**Version:** 1.0.0  
**Last Updated:** 12 August 2025  
**Owner:** Automint Ltd Development Team  

**Technical Vision:**
A robust, headless C# MCP server that provides Claude Code with comprehensive Visual Studio automation capabilities through COM interop, enabling real-time debugging control, XAML designer interaction, and complete visual context capture for enhanced AI-assisted development.

**Success Criteria:**
- Debugging session response times under 500ms
- Screenshot capture operations complete within 2 seconds
- 99% uptime during development sessions
- Support for concurrent connections to multiple VS instances

### System Architecture

**Architecture Pattern:**
Service-oriented architecture with dependency injection, implementing MCP protocol communication through a console application host.

**High-Level Components:**
- **MCP Server Host:** Console application managing MCP protocol communication and tool routing
- **Visual Studio Services:** COM interop layer providing VS automation through EnvDTE interfaces
- **Imaging Services:** Windows API integration for screenshot capture and image processing
- **Debug Services:** Specialised debugging automation with state management and event handling

**Data Flow:**
Claude Code → MCP Protocol → Tool Router → Service Layer → EnvDTE COM → Visual Studio Instance → Response Data → MCP Protocol → Claude Code

### Technology Stack

**Backend Technologies:**
- **Runtime:** .NET 8.0 (LTS)
- **Framework:** Console Application with Microsoft.Extensions.Hosting
- **Language:** C# 12.0
- **API Style:** MCP Protocol (JSON-RPC based)
- **COM Interop:** EnvDTE, EnvDTE80, EnvDTE90 interfaces

**Core Dependencies:**
- **MCP Protocol:** ModelContextProtocol 0.3.0-preview.3
- **Visual Studio APIs:** EnvDTE 17.0.32112.339, EnvDTE80 8.0.1
- **VS SDK:** Microsoft.VisualStudio.SDK 17.0.32112.339
- **Dependency Injection:** Microsoft.Extensions.DependencyInjection 8.0.0
- **Logging:** Microsoft.Extensions.Logging 8.0.0
- **Configuration:** Microsoft.Extensions.Configuration 8.0.0

**Platform Technologies:**
- **Target Framework:** net8.0-windows
- **Platform:** Windows 10/11 (Visual Studio host requirement)
- **Image Processing:** System.Drawing.Common 8.0.0
- **Windows APIs:** User32.dll, GDI32.dll for screenshot capture

**Infrastructure & DevOps:**
- **Version Control:** Git with GitHub
- **CI/CD:** GitHub Actions for automated builds
- **Package Distribution:** NuGet package as .NET Global Tool
- **Deployment:** `dotnet tool install -g` for easy installation

### Development Tools & Environment

**Required Tools:**
- **IDE:** Visual Studio 2022 (17.8 or later)
- **Version Control:** Git 2.40+
- **Package Manager:** NuGet (integrated with VS)
- **Testing:** Visual Studio Test Explorer

**Development Environment:**
- **.NET SDK:** 8.0.100 or later
- **Visual Studio:** 2022 Professional or Enterprise
- **Windows SDK:** 10.0.22621.0 (Windows 11 SDK)
- **Required Workloads:** .NET desktop development, Visual Studio extension development

**Local Setup Requirements:**
```bash
# Verify .NET 8 SDK installation
dotnet --version

# Clone repository and restore packages
git clone https://github.com/automint/visual-studio-mcp-server.git
cd visual-studio-mcp-server
dotnet restore

# Build and install as global tool
dotnet pack --configuration Release
dotnet tool install --global --add-source ./nupkg VisualStudioMcp
```

**Required Extensions:**
- Visual Studio SDK (for debugging VS automation)
- Code Analysis tools
- Live Unit Testing (for continuous test feedback)

### Integration Requirements

**Visual Studio Integration:**
- **EnvDTE Interface:** Primary automation interface for VS 2022
- **DTE2 Interface:** Enhanced automation with VS 2005+ features  
- **Debugger Services:** IVsDebugger interfaces for debugging control
- **XAML Designer APIs:** Microsoft.VisualStudio.DesignTools.Xaml interfaces

**MCP Protocol Integration:**
- **Tool Registration:** Dynamic tool registration with Claude Code
- **Request Handling:** JSON-RPC request processing with structured responses
- **Error Handling:** MCP-compliant error responses with diagnostic information

**Windows Platform Integration:**
- **COM Object Management:** Proper lifetime management of EnvDTE objects
- **Window Enumeration:** FindWindow/EnumWindows APIs for VS window discovery
- **Screenshot APIs:** BitBlt/PrintWindow for high-quality image capture

### Security & Compliance

**Security Requirements:**
- **Local Access Only:** No network exposure, localhost MCP communication only
- **Process Isolation:** Sandboxed execution with minimal system permissions
- **Data Protection:** Temporary file cleanup, no persistent sensitive data storage

**Security Implementation:**
- **COM Security:** Use of CLSCTX_LOCAL_SERVER for secure COM activation
- **File System Access:** Limited to VS workspace and temp directories
- **Memory Management:** Proper disposal of COM objects and image resources

### Performance & Scalability

**Performance Requirements:**
- **Tool Response Time:** < 500ms for debugging operations
- **Screenshot Capture:** < 2 seconds for full VS window capture
- **Memory Usage:** < 100MB baseline, < 500MB during intensive operations
- **CPU Utilisation:** < 20% during normal operations

**Scalability Strategy:**
- **Concurrent Requests:** Async/await patterns for non-blocking operations
- **Multiple VS Instances:** Support for connecting to multiple VS processes
- **Resource Management:** Pooling of expensive resources like image capture contexts

**Performance Optimisations:**
- **Lazy Loading:** COM object creation only when needed
- **Caching Strategy:** Cache frequently accessed VS state information
- **Image Compression:** Optimised PNG compression for screenshot data

### Testing Strategy

**Testing Approach:**
- **Unit Testing:** MSTest with Moq for service layer testing
- **Integration Testing:** Live Visual Studio instance testing
- **Contract Testing:** MCP protocol compliance validation
- **Visual Testing:** Screenshot capture quality validation

**Test Coverage Requirements:**
- **Code Coverage:** Minimum 80% line coverage for core services
- **Integration Coverage:** All MCP tools tested with live VS instances
- **Error Path Coverage:** COM exception handling and recovery testing

**Quality Gates:**
- **Automated Tests:** All tests must pass before merge
- **Code Analysis:** No critical or high severity issues
- **Performance Benchmarks:** All operations within performance targets

### Deployment & Release

**Deployment Strategy:**
- **Distribution Method:** .NET Global Tool via NuGet package
- **Installation Command:** `dotnet tool install -g VisualStudioMcp`
- **Update Strategy:** `dotnet tool update -g VisualStudioMcp`

**Release Process:**
- **Versioning:** Semantic versioning (major.minor.patch)
- **Release Branches:** Dedicated release branches for version management
- **Package Publishing:** Automated NuGet package publishing via GitHub Actions

**Configuration Management:**
```json
{
  "VisualStudioMcp": {
    "LogLevel": "Information",
    "ScreenshotFormat": "PNG",
    "ComTimeoutMs": 30000,
    "MaxConcurrentRequests": 10
  }
}
```

### Monitoring & Observability

**Logging Strategy:**
- **Structured Logging:** JSON formatted logs with correlation IDs
- **Log Levels:** Debug, Information, Warning, Error, Critical
- **Log Outputs:** Console (development), File (production)

**Metrics & Alerting:**
- **Performance Metrics:** Tool execution times, memory usage, error rates
- **Health Checks:** VS connection status, COM object health
- **Diagnostic Information:** VS version, loaded extensions, project state

**Error Tracking:**
- **Exception Handling:** Comprehensive try-catch with context preservation
- **Error Classification:** COM errors, VS state errors, MCP protocol errors
- **Recovery Strategies:** Automatic retry for transient failures

### Data Architecture

**Core Data Models:**
- **VisualStudioInstance:** Process ID, version, solution state
- **DebugState:** Current execution point, variables, call stack
- **XamlDesignerCapture:** Image data, element metadata, design surface bounds
- **BuildResult:** Success status, errors, warnings, output logs

**Data Validation:**
- **Input Validation:** Parameter validation for all MCP tool calls
- **COM Response Validation:** Null checks and type validation for EnvDTE responses
- **Image Data Validation:** Screenshot quality and format validation

**Data Lifecycle:**
- **Temporary Files:** Automatic cleanup of screenshot and log files
- **Memory Management:** Immediate disposal of COM objects and image resources
- **Cache Management:** Time-based expiration of cached VS state information

### Development Workflow

**Branch Strategy:**
- **Main Branch:** Production-ready code with required PR reviews
- **Feature Branches:** Feature-specific development with naming convention `feature/vs-debugging-control`
- **Release Branches:** Version preparation branches `release/1.0.0`

**Code Review Process:**
- **Required Reviewers:** Minimum 1 reviewer, 2 for architecture changes
- **Automated Checks:** Build validation, test execution, code analysis
- **Review Criteria:** Code quality, performance impact, COM interop safety

**Git Workflow Guidelines:**
- Always create feature branches from latest main
- Commit frequently with descriptive messages
- Push feature branches and create pull requests
- Merge via pull requests only, never direct to main
- Delete feature branches after successful merge

### Assumptions & Constraints

**Technical Assumptions:**
- Visual Studio 2022 is the primary target version
- Windows 10/11 development environment availability
- Stable network connectivity not required (local automation)
- Single-user development scenarios (not multi-tenant)

**Constraints:**
- **Platform Limitation:** Windows-only due to Visual Studio platform requirements
- **COM Dependency:** Limited by EnvDTE interface capabilities and stability
- **Performance Constraints:** Screenshot capture limited by display resolution and VS responsiveness

**Development Constraints:**
- **Team Size:** Single developer initially, expanding to small team
- **Timeline:** 4-6 weeks for initial implementation
- **Resource Availability:** Part-time development within existing responsibilities

### Risks & Mitigation

**Technical Risks:**
- **COM Interop Stability:** Medium probability, high impact - Mitigation: Comprehensive error handling, connection retry logic, fallback strategies
- **Visual Studio API Changes:** Low probability, high impact - Mitigation: Use stable EnvDTE interfaces, version compatibility matrix
- **Screenshot Performance:** Medium probability, medium impact - Mitigation: Async capture, image compression, quality vs speed optimisation

**Operational Risks:**
- **Visual Studio Crashes:** High probability, medium impact - Mitigation: Graceful disconnection handling, automatic reconnection attempts
- **Memory Leaks:** Medium probability, medium impact - Mitigation: Proper COM object disposal, memory profiling, automated testing

**Development Risks:**
- **Complexity Underestimation:** Medium probability, high impact - Mitigation: Iterative development, early prototype validation, regular progress reviews

### Future Considerations

**Technical Evolution:**
- **Visual Studio Code Support:** Extend automation to VS Code via Language Server Protocol
- **Remote Development:** Support for Visual Studio in cloud/container environments
- **AI Integration:** Enhanced Claude Code integration with VS IntelliSense and CodeLens

**Scalability Roadmap:**
- **Multi-Developer Support:** Team collaboration features with shared debugging sessions
- **Enterprise Features:** Integration with Azure DevOps, advanced security, audit logging
- **Performance Optimisation:** GPU-accelerated screenshot capture, predictive caching

**Integration Opportunities:**
- **Azure DevOps Integration:** Work item tracking, build pipeline integration
- **GitHub Copilot Compatibility:** Complementary AI assistance features
- **Debugging Tools Integration:** Integration with specialised debugging and profiling tools

---

## Implementation Phases

**Phase 1: Foundation (Weeks 1-2)**
- Project structure and dependency injection setup
- Basic MCP server implementation with tool registration
- Visual Studio instance discovery and connection
- Simple debugging control (start/stop)

**Phase 2: Core Features (Weeks 3-4)**
- XAML designer screenshot capture
- Runtime state inspection (variables, call stack)
- Output window content streaming
- Comprehensive error handling

**Phase 3: Enhancement (Weeks 5-6)**
- Advanced debugging features (breakpoint management, expression evaluation)
- Multi-instance support
- Performance optimisation and testing
- Documentation and deployment automation

This planning document provides the complete technical foundation for building a robust Visual Studio MCP server that will significantly enhance Claude Code's development capabilities within the Automint Ltd environment.