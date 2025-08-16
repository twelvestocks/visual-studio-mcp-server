# Visual Studio MCP Server

A .NET 8 console application that provides Claude Code with comprehensive Visual Studio automation capabilities including debugging control, XAML designer interaction, and visual context capture through COM interop.

[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Windows](https://img.shields.io/badge/OS-Windows-blue.svg)](https://www.microsoft.com/windows)
[![Visual Studio](https://img.shields.io/badge/VS-2022-purple.svg)](https://visualstudio.microsoft.com/vs/)
[![MCP](https://img.shields.io/badge/MCP-0.3.0--preview.3-green.svg)](https://github.com/modelcontextprotocol)

## 🚀 Quick Start

**5-Minute Setup** - Get Visual Studio automation running with Claude Code in under 10 minutes!

### Prerequisites
- Windows 10/11
- Visual Studio 2022 (17.8 or later)
- .NET 8 SDK
- Claude Code

### Installation
```bash
# Install as .NET global tool
dotnet tool install --global VisualStudioMcp

# Verify installation
vsmcp --version
```

### Claude Code Configuration
Add to your `mcp_servers.json`:
```json
{
  "mcpServers": {
    "visual-studio": {
      "command": "vsmcp",
      "args": [],
      "env": {}
    }
  }
}
```

### Your First Automation
1. Open Visual Studio 2022 with a solution
2. Start Claude Code
3. Try: "List my Visual Studio instances and build my solution"

**📋 Complete Setup Guide:** [Getting Started](docs/getting-started.md)

## ✨ Key Features

### Visual Studio Integration
- 🔍 **Instance Discovery** - Find and connect to running Visual Studio instances
- 🔨 **Build Automation** - Trigger builds and capture detailed error information
- 🐛 **Debug Control** - Start, stop, and monitor debugging sessions
- 📊 **Solution Management** - Load, build, and manage Visual Studio solutions

### XAML Designer Automation
- 📸 **Designer Screenshots** - Capture XAML designer surfaces for visual context
- 🖼️ **Window Capture** - Screenshot any Visual Studio window or panel
- 🎨 **Visual Context** - Provide Claude Code with visual understanding of UI designs

### Development Workflow Enhancement  
- ⚡ **Real-time Integration** - Seamless Claude Code workflow integration
- 📝 **Error Analysis** - Detailed build error capture and context
- 🔄 **Automated Testing** - Integration with test execution and reporting
- 🗂️ **Project Analysis** - Deep inspection of project structure and dependencies

## 🏗️ Architecture

### Technology Stack
- **.NET 8** - Modern cross-platform runtime with Windows-specific features
- **COM Interop** - Direct integration with Visual Studio via EnvDTE APIs  
- **MCP Protocol** - ModelContextProtocol for Claude Code communication
- **Dependency Injection** - Microsoft.Extensions.Hosting service architecture

### Service Architecture
```
┌─────────────────────────────────────────────┐
│              MCP Server Host                │
├─────────────────────────────────────────────┤
│  VisualStudioMcp.Core     │  VS Automation  │
│  VisualStudioMcp.Debug    │  Debug Control  │  
│  VisualStudioMcp.Xaml     │  XAML Designer  │
│  VisualStudioMcp.Imaging  │  Screenshots    │
├─────────────────────────────────────────────┤
│         COM Interop Layer (EnvDTE)          │
├─────────────────────────────────────────────┤
│            Visual Studio 2022               │
└─────────────────────────────────────────────┘
```

## 📚 Documentation

Complete documentation is available in the [`/docs`](docs/) directory:

### Getting Started
- **[🚀 Getting Started](docs/getting-started.md)** - 5-minute setup guide
- **[📚 API Reference](docs/api/mcp-tools-reference.md)** - Complete MCP tools documentation (17 tools)
- **[👥 Claude Code Integration](docs/user-guides/claude-code-integration.md)** - Workflows and examples

### Operations & Support
- **[🔧 Installation Guide](docs/operations/installation-guide.md)** - Detailed installation and setup
- **[🐛 Troubleshooting Matrix](docs/operations/troubleshooting-matrix.md)** - Comprehensive error resolution
- **[📈 Performance Benchmarks](docs/operations/performance-benchmarks.md)** - Performance expectations and optimization

### Security & Enterprise
- **[🛡️ Security & Compliance](docs/security/security-compliance.md)** - Enterprise security documentation
- **[🏢 Enterprise Deployment](docs/operations/installation-guide.md#enterprise-deployment)** - Group Policy and corporate deployment

### Development
- **[🛠️ Development Setup](docs/development/development-setup.md)** - Environment configuration guide  
- **[🏗️ System Architecture](docs/architecture/system-architecture.md)** - Technical design and patterns
- **[📋 Project Overview](docs/project/project-overview.md)** - Vision, goals, and technology decisions

## 🔧 Development

### Building from Source
```bash
# Clone the repository
git clone https://github.com/your-org/MCP-VS-AUTOMATION
cd MCP-VS-AUTOMATION

# Restore dependencies and build
dotnet restore
dotnet build

# Run tests
dotnet test
```

### Project Structure
```
src/
├── VisualStudioMcp.Server/     # Console application entry point
├── VisualStudioMcp.Core/       # Core VS automation services
├── VisualStudioMcp.Debug/      # Debugging automation  
├── VisualStudioMcp.Xaml/       # XAML designer automation
├── VisualStudioMcp.Imaging/    # Screenshot and visual capture
└── VisualStudioMcp.Shared/     # Common models and interfaces

tests/
├── VisualStudioMcp.Core.Tests/
├── VisualStudioMcp.Debug.Tests/
└── VisualStudioMcp.Integration.Tests/
```

### Contributing
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 🔐 Security & Safety

- **Local Development Tool** - No network exposure, operates locally only
- **Input Validation** - All parameters validated before COM operations
- **Resource Management** - Proper COM object lifecycle and memory management
- **Sandboxed Operations** - Limited permissions with Visual Studio integration

## ⚠️ Requirements & Limitations  

### System Requirements
- **Operating System:** Windows 10/11 (Visual Studio COM dependency)
- **Visual Studio:** 2022 (17.8+) with .NET desktop development workload
- **.NET Runtime:** 8.0 or later
- **Memory:** Minimum 4GB RAM (8GB+ recommended)

### Known Limitations
- Windows-only deployment (Visual Studio COM interop requirement)
- Requires Visual Studio 2022 installation
- Single Visual Studio instance connection at a time
- COM threading limitations may affect concurrent operations

## 🐛 Troubleshooting

Quick solutions for common issues:

| Issue | Quick Fix |
|-------|-----------|
| `vsmcp: command not found` | Run `dotnet tool install --global VisualStudioMcp` |
| Claude Code connection failed | Check `mcp_servers.json` configuration and restart Claude Code |
| No Visual Studio instances found | Ensure VS 2022 is running with a solution loaded |
| Permission errors | Run Visual Studio as Administrator |

**📋 Complete Troubleshooting:** [Troubleshooting Matrix](docs/operations/troubleshooting-matrix.md)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Support

- **Documentation:** [/docs directory](docs/)
- **Issues:** [GitHub Issues](https://github.com/your-org/MCP-VS-AUTOMATION/issues)  
- **Discussions:** [GitHub Discussions](https://github.com/your-org/MCP-VS-AUTOMATION/discussions)

## 🎯 Roadmap

- [ ] **Phase 1:** Core Visual Studio automation and debugging control
- [ ] **Phase 2:** Advanced XAML designer integration and visual context
- [ ] **Phase 3:** Extended IDE automation and customisation support
- [ ] **Phase 4:** Multi-instance management and advanced workflows

---

**Built for Claude Code** | **Powered by .NET 8** | **Windows Development Excellence**