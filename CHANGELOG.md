# Changelog

All notable changes to the Visual Studio MCP Server project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Comprehensive Visual Studio automation through MCP protocol
- Complete debugging session management and control
- XAML designer automation and visual capture capabilities
- High-quality screenshot capture with annotations
- Production-ready COM interop with proper resource management

### Security
- Input validation for all MCP tool parameters
- Secure COM object lifecycle management
- Path validation and traversal protection

## [1.0.0] - 2025-08-16

### Added
- **Core Visual Studio Integration**
  - Instance discovery and connection management
  - Solution and project operations
  - Build automation with detailed error capture
  - Project enumeration and dependency analysis

- **Debugging Automation (9 tools)**
  - `vs_start_debugging` - Start debugging sessions
  - `vs_stop_debugging` - Stop debugging sessions  
  - `vs_get_debug_state` - Get current debug state
  - `vs_set_breakpoint` - Set conditional breakpoints
  - `vs_get_breakpoints` - List all breakpoints
  - `vs_get_local_variables` - Inspect local variables
  - `vs_get_call_stack` - Examine call stack
  - `vs_step_debug` - Step through code execution
  - `vs_evaluate_expression` - Evaluate debug expressions

- **Visual Capture System (3 tools)**
  - `vs_capture_window` - Capture any VS window
  - `vs_capture_full_ide` - Capture complete IDE state
  - `vs_analyse_visual_state` - Analyse and compare visual states

- **XAML Designer Automation**
  - XAML designer window detection and capture
  - Element highlighting and annotation
  - Data binding analysis and validation
  - Multi-monitor support with DPI handling

- **Global Tool Packaging**
  - .NET global tool distribution (`vsmcp` command)
  - NuGet package with comprehensive metadata
  - Automated CI/CD pipeline with GitHub Actions
  - Production-ready deployment automation

- **Comprehensive Documentation**
  - Complete API reference with 17 MCP tools
  - Installation and setup guides
  - Claude Code integration workflows
  - Real-world usage examples and patterns

- **Testing Infrastructure**
  - Unit tests for all core components
  - Integration tests for Visual Studio automation
  - MCP protocol compliance testing
  - Performance and memory management validation

### Technical
- **.NET 8** target framework with Windows-specific features
- **COM Interop** through EnvDTE APIs with proper disposal patterns
- **MCP Protocol 0.3.0-preview.3** implementation
- **Microsoft.Extensions.Hosting** service architecture
- **Comprehensive error handling** with structured logging
- **Memory monitoring** and COM object lifecycle management

### Security
- Input validation and path sanitisation for all tools
- COM security boundary enforcement
- Secure temporary file handling for screenshots
- No persistent sensitive data storage

### Performance
- Optimised COM object lifecycle management
- Efficient screenshot capture with configurable quality
- Memory usage monitoring and cleanup
- Concurrent operation support with proper synchronisation

---

## Release Guidelines

### Version Numbering
- **Major.Minor.Patch** format following Semantic Versioning
- **Major**: Breaking changes or significant new functionality
- **Minor**: New features, backwards compatible
- **Patch**: Bug fixes and minor improvements

### Release Process
1. Update version in project files
2. Update CHANGELOG.md with new version
3. Create Git tag with `v` prefix (e.g., `v1.0.0`)
4. GitHub Actions automatically builds and publishes
5. Create GitHub release with release notes
6. Update documentation as needed

### Categories
- **Added**: New features and capabilities
- **Changed**: Changes to existing functionality
- **Deprecated**: Features marked for removal
- **Removed**: Features removed in this version
- **Fixed**: Bug fixes and issue resolutions
- **Security**: Security improvements and vulnerability fixes
- **Technical**: Internal improvements and technical changes
- **Performance**: Performance optimisations and improvements