# Visual Capture User Guide

A comprehensive guide for using the advanced visual capture capabilities of the Visual Studio MCP Server with Claude Code. This guide covers everything from basic operations to advanced workflows and troubleshooting.

## 📋 Quick Start Guide

### Prerequisites

Before using visual capture features, ensure you have:

- **Visual Studio 2022** (17.8 or later) installed and running
- **Visual Studio MCP Server** properly configured and connected
- **Claude Code** with MCP integration enabled
- **Windows 10/11** with appropriate display drivers
- **Minimum 8GB RAM** (16GB recommended for large captures)

### Basic Setup Verification

```bash
# Verify MCP server is running and connected
vsmcp --status

# Test basic connectivity
vsmcp --test-connection

# Check capture capabilities
vsmcp --list-capabilities
```

### Your First Capture

```markdown
# In Claude Code
@vsmcp vs_capture_window --window-handle <handle> --include-annotations true

# Alternative: Capture entire IDE
@vsmcp vs_capture_full_ide --include-layout-metadata true
```

---

## 🎯 Core Features and Use Cases

### 1. Single Window Capture

**Purpose**: Capture specific Visual Studio windows with intelligent processing and annotation.

#### Basic Usage
```markdown
# Capture a specific window (e.g., code editor)
@vsmcp vs_capture_window --window-handle 0x12345678

# Enhanced capture with annotations
@vsmcp vs_capture_window 
  --window-handle 0x12345678
  --include-annotations true
  --capture-quality balanced
```

#### Advanced Options
```markdown
# High-quality capture for detailed analysis
@vsmcp vs_capture_window 
  --window-handle 0x12345678
  --capture-quality high
  --include-annotations true
  --include-layout-metadata true
  --timeout 30

# Performance-optimised capture
@vsmcp vs_capture_window 
  --window-handle 0x12345678
  --capture-quality fast
  --max-dimensions 1280x720
```

#### Window Types and Specialisations

| Window Type | Specialised Processing | Best Use Cases |
|------------|----------------------|----------------|
| **Code Editor** | Syntax highlighting preservation, line number clarity | Code review, debugging assistance |
| **Designer Surface** | High colour accuracy, DPI scaling | UI design analysis, layout review |
| **Solution Explorer** | Tree structure clarity, expansion state | Project structure analysis |
| **Debugger Windows** | Variable highlighting, state clarity | Debugging assistance, issue diagnosis |
| **Tool Windows** | Optimised processing for smaller windows | Quick tool status checks |

### 2. Full IDE Layout Capture

**Purpose**: Capture complete Visual Studio layout with intelligent stitching and comprehensive metadata.

#### Basic Usage
```markdown
# Complete IDE capture
@vsmcp vs_capture_full_ide

# Enhanced capture with full metadata
@vsmcp vs_capture_full_ide 
  --include-layout-metadata true
  --include-annotations true
```

#### Advanced Workflow
```markdown
# Comprehensive capture for development session analysis
@vsmcp vs_capture_full_ide 
  --include-layout-metadata true
  --include-annotations true
  --capture-quality high
  --include-component-captures true
  --timeout 60
```

#### Layout Analysis Features

- **Window Positioning**: Exact placement and sizing of all components
- **Docking Relationships**: Parent-child window relationships
- **Z-Order Analysis**: Window layering and visibility states
- **Component Classification**: Automatic identification of 20+ window types
- **Interaction Mapping**: Clickable areas and interaction hotspots

### 3. Visual State Analysis

**Purpose**: Intelligent comparison and analysis of Visual Studio states over time.

#### Basic Analysis
```markdown
# Analyse current visual state
@vsmcp vs_analyse_visual_state

# Compare with previous state
@vsmcp vs_analyse_visual_state 
  --compare-with-previous true
  --include-change-detection true
```

#### Advanced Comparison Workflows
```markdown
# Comprehensive state analysis with change tracking
@vsmcp vs_analyse_visual_state 
  --compare-with-previous true
  --include-change-detection true
  --include-performance-impact true
  --generate-insights true
```

#### Analysis Capabilities

- **Component-Level Changes**: Detection of modifications in specific windows
- **Layout Structural Changes**: Docking, sizing, and positioning modifications
- **Content Semantic Changes**: Code changes, file modifications, debug state changes
- **Performance Impact Assessment**: Resource usage implications of changes
- **Actionable Insights**: Recommendations for workflow optimisation

---

## 🔧 Configuration and Settings

### Capture Quality Settings

#### Quality Profiles

| Profile | Resolution | Processing | Memory Usage | Best For |
|---------|-----------|------------|--------------|----------|
| **Fast** | 720p max | Minimal | <10MB | Quick status checks |
| **Balanced** | 1080p max | Standard | 10-25MB | General use cases |
| **High** | 1440p max | Enhanced | 25-50MB | Detailed analysis |
| **Ultra** | 4K max | Maximum | 50-100MB | Production documentation |

#### Custom Quality Configuration
```json
{
  "captureSettings": {
    "defaultQuality": "balanced",
    "customProfiles": {
      "codeReview": {
        "maxDimensions": "1920x1080",
        "compressionQuality": 90,
        "includeAnnotations": true,
        "optimiseForText": true
      },
      "uiDesign": {
        "maxDimensions": "2560x1440",
        "compressionQuality": 95,
        "preserveColorAccuracy": true,
        "includeDpiMetadata": true
      }
    }
  }
}
```

### Memory Management Configuration

#### Memory Thresholds
```json
{
  "memoryManagement": {
    "warningThreshold": "50MB",
    "rejectionThreshold": "100MB",
    "emergencyThreshold": "200MB",
    "enablePredictiveAssessment": true,
    "enableEmergencyRecovery": true
  }
}
```

#### Advanced Memory Settings
```json
{
  "memoryManagement": {
    "adaptiveThresholds": {
      "lowMemorySystem": {
        "warningThreshold": "25MB",
        "rejectionThreshold": "50MB"
      },
      "highMemorySystem": {
        "warningThreshold": "100MB",
        "rejectionThreshold": "200MB"
      }
    },
    "garbageCollectionStrategy": "aggressive",
    "resourceCleanupInterval": "30s"
  }
}
```

### Performance Optimisation Settings

#### Threading Configuration
```json
{
  "performance": {
    "concurrentCaptureLimit": 4,
    "timeoutSettings": {
      "singleWindow": "30s",
      "fullIde": "60s",
      "visualAnalysis": "45s"
    },
    "caching": {
      "enableSmartCaching": true,
      "cacheExpirationTime": "5m",
      "maxCacheSize": "200MB"
    }
  }
}
```

---

## 🚀 Advanced Workflows

### 1. Development Session Documentation

**Scenario**: Creating comprehensive documentation of a development session for team review.

#### Workflow Steps
```markdown
# 1. Initial state capture
@vsmcp vs_capture_full_ide 
  --include-layout-metadata true
  --session-name "feature-development-session"

# 2. Code changes capture
@vsmcp vs_capture_window 
  --window-type CodeEditor
  --include-annotations true
  --session-name "feature-development-session"

# 3. Debugging session capture
@vsmcp vs_capture_window 
  --window-type DebuggerWindow
  --include-state-annotations true
  --session-name "feature-development-session"

# 4. Final state analysis
@vsmcp vs_analyse_visual_state 
  --compare-with-session-start true
  --generate-summary-report true
  --session-name "feature-development-session"
```

#### Expected Outputs
- Complete visual timeline of development session
- Annotated code changes with context
- Debugging state progression
- Session summary with productivity insights

### 2. UI/UX Design Review

**Scenario**: Capturing and analysing Visual Studio's designer surfaces for design review and iteration.

#### Workflow Steps
```markdown
# 1. Designer surface capture with high fidelity
@vsmcp vs_capture_window 
  --window-type DesignerSurface
  --capture-quality ultra
  --preserve-color-accuracy true
  --include-dpi-metadata true

# 2. Properties panel capture for design context
@vsmcp vs_capture_window 
  --window-type PropertiesWindow
  --include-annotations true

# 3. Toolbox capture for available controls
@vsmcp vs_capture_window 
  --window-type ToolboxWindow
  --include-annotations true

# 4. Comprehensive layout analysis
@vsmcp vs_capture_full_ide 
  --focus-on-design-elements true
  --include-accessibility-metadata true
```

### 3. Debugging Session Analysis

**Scenario**: Comprehensive debugging session capture for issue resolution and team collaboration.

#### Workflow Steps
```markdown
# 1. Pre-debugging state
@vsmcp vs_capture_full_ide 
  --debugging-session-start true
  --include-solution-state true

# 2. Breakpoint and variable capture
@vsmcp vs_capture_window 
  --window-type DebuggerWindow
  --include-variable-annotations true
  --include-call-stack true

# 3. Watch window and immediate window capture
@vsmcp vs_capture_window 
  --window-type WatchWindow
  --include-expression-evaluation true

# 4. Issue resolution state
@vsmcp vs_analyse_visual_state 
  --debugging-session-complete true
  --generate-debugging-report true
```

### 4. Performance Profiling Visualisation

**Scenario**: Capturing performance profiling results and analysis for optimisation work.

#### Workflow Steps
```markdown
# 1. Profiler window capture
@vsmcp vs_capture_window 
  --window-type ProfilerWindow
  --capture-quality high
  --include-performance-annotations true

# 2. Performance metrics overlay
@vsmcp vs_capture_window 
  --window-type PerformanceWindow
  --include-metrics-overlay true

# 3. Code hotspot identification
@vsmcp vs_capture_window 
  --window-type CodeEditor
  --highlight-performance-hotspots true

# 4. Comprehensive performance analysis
@vsmcp vs_analyse_visual_state 
  --performance-analysis-mode true
  --generate-optimisation-suggestions true
```

---

## 🎨 Annotation and Metadata Features

### Automatic Annotation Types

#### Window-Specific Annotations

**Code Editor Annotations**
- Syntax highlighting preservation
- Line number clarity enhancement
- Code folding state indicators
- Error and warning markers
- IntelliSense popup context

**Designer Surface Annotations**
- Control type identification
- Property binding indicators
- Layout guide overlays
- Alignment and spacing annotations
- Resource reference tracking

**Debugger Window Annotations**
- Variable value highlighting
- Type information overlays
- Memory address annotations
- Call stack level indicators
- Exception state markers

#### Interactive Hotspot Detection

```markdown
# Enable advanced hotspot detection
@vsmcp vs_capture_window 
  --window-handle 0x12345678
  --include-interaction-hotspots true
  --hotspot-detail-level detailed
```

**Hotspot Categories**
- **Clickable Elements**: Buttons, menu items, tool icons
- **Editable Areas**: Text boxes, code editors, property fields
- **Navigation Elements**: Tree nodes, tabs, scrollbars
- **Context Menus**: Right-click areas and context-sensitive regions
- **Drag-Drop Zones**: Dockable panels, file drop areas

### Custom Annotation Configuration

#### Annotation Profiles
```json
{
  "annotationProfiles": {
    "codeReview": {
      "highlightSyntax": true,
      "showLineNumbers": true,
      "markErrors": true,
      "includeComments": false
    },
    "uiDesign": {
      "showControlBounds": true,
      "highlightAlignment": true,
      "showPropertyBindings": true,
      "includeAccessibilityInfo": true
    },
    "debugging": {
      "highlightVariables": true,
      "showCallStack": true,
      "markBreakpoints": true,
      "includeMemoryInfo": true
    }
  }
}
```

### Metadata Integration

#### Layout Metadata
```json
{
  "layoutMetadata": {
    "windowHierarchy": {
      "mainWindow": {
        "bounds": {"x": 0, "y": 0, "width": 1920, "height": 1080},
        "children": [
          {
            "type": "CodeEditor",
            "bounds": {"x": 300, "y": 100, "width": 1200, "height": 800},
            "properties": {
              "fileName": "Program.cs",
              "language": "csharp",
              "lineCount": 150,
              "cursorPosition": {"line": 42, "column": 15}
            }
          }
        ]
      }
    }
  }
}
```

#### Accessibility Metadata
```json
{
  "accessibilityMetadata": {
    "screenReaderSupport": true,
    "keyboardNavigation": true,
    "colorContrastInfo": {
      "foreground": "#000000",
      "background": "#FFFFFF",
      "contrastRatio": 21.0
    },
    "alternativeText": "Visual Studio Code Editor showing Program.cs"
  }
}
```

---

## 🚨 Troubleshooting and Common Issues

### Performance Issues

#### Slow Capture Operations

**Symptoms**
- Capture operations taking longer than 5 seconds
- Visual Studio becomes unresponsive during captures
- High memory usage warnings

**Diagnosis**
```markdown
# Check system performance impact
@vsmcp vs_analyse_performance 
  --include-system-metrics true
  --duration 60

# Monitor memory usage during capture
@vsmcp vs_capture_window 
  --window-handle 0x12345678
  --monitor-memory-usage true
```

**Solutions**
1. **Reduce Capture Quality**: Use `fast` or `balanced` quality settings
2. **Limit Capture Dimensions**: Set maximum dimensions to 1080p
3. **Disable Annotations**: Temporarily disable annotations for speed
4. **Close Unused Applications**: Free up system memory
5. **Restart Visual Studio**: Clear COM object accumulation

#### Memory Pressure Warnings

**Symptoms**
- "Memory pressure detected" warnings
- Capture operations rejected due to memory limits
- System slowdown during large captures

**Diagnosis**
```markdown
# Check memory pressure status
@vsmcp vs_check_memory_pressure

# Analyse memory usage patterns
@vsmcp vs_analyse_memory_usage 
  --historical-analysis true
  --duration 3600
```

**Solutions**
1. **Adjust Memory Thresholds**: Increase limits for high-memory systems
2. **Enable Emergency Recovery**: Activate automatic cleanup
3. **Use Progressive Captures**: Capture windows sequentially instead of parallel
4. **Restart MCP Server**: Clear accumulated memory usage

### Capture Quality Issues

#### Blurry or Low-Quality Images

**Symptoms**
- Text appears blurry or pixelated
- UI elements are difficult to distinguish
- Color accuracy issues

**Diagnosis**
```markdown
# Test capture quality across different settings
@vsmcp vs_test_capture_quality 
  --window-handle 0x12345678
  --test-all-quality-levels true

# Check DPI scaling issues
@vsmcp vs_check_dpi_scaling 
  --window-handle 0x12345678
```

**Solutions**
1. **Increase Capture Quality**: Use `high` or `ultra` quality settings
2. **Adjust DPI Settings**: Ensure proper DPI scaling compensation
3. **Check Monitor Configuration**: Verify multi-monitor DPI consistency
4. **Update Graphics Drivers**: Ensure latest graphics drivers are installed

#### Missing Window Content

**Symptoms**
- Windows appear black or empty in captures
- Partial window content missing
- Overlapping windows not properly handled

**Diagnosis**
```markdown
# Check window visibility and layering
@vsmcp vs_analyse_window_state 
  --window-handle 0x12345678
  --include-z-order true

# Test window enumeration
@vsmcp vs_enumerate_windows 
  --detailed-analysis true
```

**Solutions**
1. **Ensure Window Visibility**: Bring target windows to foreground
2. **Check Window State**: Verify windows are not minimised or hidden
3. **Retry Capture**: Some windows may need multiple attempts
4. **Use Alternative Capture Methods**: Try different capture strategies

### Integration Issues

#### Claude Code Integration Problems

**Symptoms**
- MCP tools not responding
- Timeout errors in Claude Code
- Incomplete results returned

**Diagnosis**
```bash
# Test MCP server connectivity
vsmcp --test-connection

# Check MCP server logs
vsmcp --show-logs --level debug

# Verify tool registration
vsmcp --list-tools
```

**Solutions**
1. **Restart MCP Server**: Clear connection state
2. **Check Configuration**: Verify MCP server settings
3. **Update Claude Code**: Ensure latest version compatibility
4. **Review Logs**: Check for specific error messages

#### Visual Studio COM Errors

**Symptoms**
- "Visual Studio instance not found" errors
- COM exception errors
- "Access denied" errors

**Diagnosis**
```markdown
# Check Visual Studio instances
@vsmcp vs_list_instances

# Test COM connectivity
@vsmcp vs_test_com_connection 
  --instance-id <id>

# Check permissions
@vsmcp vs_check_permissions
```

**Solutions**
1. **Restart Visual Studio**: Clear COM object state
2. **Run as Administrator**: Ensure sufficient permissions
3. **Check Visual Studio Version**: Verify compatibility
4. **Update EnvDTE References**: Ensure latest COM interfaces

---

## 📊 Performance Monitoring and Optimization

### Built-in Performance Metrics

#### Capture Operation Metrics

```markdown
# Enable performance monitoring for captures
@vsmcp vs_capture_window 
  --window-handle 0x12345678
  --enable-performance-monitoring true
  --detailed-metrics true
```

**Collected Metrics**
- **Operation Duration**: Total time for capture completion
- **Memory Usage**: Peak memory during operation
- **CPU Usage**: Processor utilisation during capture
- **GDI Resource Usage**: Graphics resource consumption
- **Thread Utilisation**: Concurrent operation efficiency

#### System Impact Assessment

```markdown
# Analyse system impact of capture operations
@vsmcp vs_analyse_system_impact 
  --duration 300
  --include-baseline true
  --generate-recommendations true
```

**Impact Categories**
- **Visual Studio Responsiveness**: IDE performance during captures
- **System Memory Pressure**: Overall system memory impact
- **Graphics Performance**: GPU and graphics system impact
- **Network Usage**: Data transmission and storage impact

### Performance Optimization Techniques

#### Capture Strategy Optimization

**Intelligent Strategy Selection**
```json
{
  "optimizationSettings": {
    "enableAdaptiveStrategy": true,
    "strategySelection": {
      "lowMemory": "sequential",
      "highMemory": "parallel",
      "lowCpu": "simplified",
      "highCpu": "enhanced"
    }
  }
}
```

**Caching Optimization**
```json
{
  "cachingOptimization": {
    "enableSmartCaching": true,
    "cacheStrategy": "contentAware",
    "invalidationRules": {
      "windowContentChange": true,
      "layoutModification": true,
      "timeBasedExpiration": "5m"
    }
  }
}
```

#### Resource Management Optimization

**Memory Management**
```json
{
  "resourceOptimization": {
    "enablePredictiveCleanup": true,
    "aggressiveCleanupThreshold": "75%",
    "resourcePooling": {
      "enableObjectPooling": true,
      "poolSizes": {
        "bitmaps": 10,
        "graphics": 5,
        "deviceContexts": 3
      }
    }
  }
}
```

---

## 🔮 Advanced Features and Future Capabilities

### Experimental Features

#### Machine Learning Integration (Preview)

```markdown
# Enable ML-powered visual analysis
@vsmcp vs_capture_window 
  --window-handle 0x12345678
  --enable-ml-analysis true
  --ml-models "ui-element-detection,code-analysis"
```

**ML Capabilities**
- **UI Element Recognition**: Automatic detection and classification of UI elements
- **Code Pattern Analysis**: Intelligent code structure and pattern recognition
- **Workflow Optimization**: AI-driven workflow improvement suggestions
- **Predictive Caching**: ML-powered cache management and prediction

#### Real-Time Collaboration (Preview)

```markdown
# Start collaborative capture session
@vsmcp vs_start_collaboration_session 
  --session-name "team-review"
  --participants "user1,user2,user3"

# Share visual context in real-time
@vsmcp vs_share_visual_context 
  --session-name "team-review"
  --include-live-updates true
```

### Integration Roadmap

#### Planned Integrations

**Version Control Integration**
- Visual diff capabilities for UI changes
- Commit-triggered capture automation
- Branch comparison visualisation

**CI/CD Pipeline Integration**
- Automated capture during build processes
- Visual regression testing integration
- Deployment validation captures

**Team Collaboration Tools**
- Microsoft Teams integration
- Slack visual context sharing
- Azure DevOps work item integration

#### API Extension Points

**Custom Capture Processors**
```csharp
public interface ICustomCaptureProcessor
{
    Task<ProcessedCapture> ProcessCaptureAsync(
        SpecializedCapture capture, 
        ProcessingOptions options);
}
```

**Plugin Architecture**
```csharp
public interface ICapturePlugin
{
    string Name { get; }
    Version Version { get; }
    Task<PluginResult> ProcessAsync(CaptureContext context);
}
```

---

## 📚 Resources and Further Reading

### Documentation Links

- [Advanced Capture Architecture](../architecture/advanced-capture-architecture.md)
- [Memory Management Guide](../development/memory-management-guide.md)
- [COM Development Patterns](../development/com-development-patterns.md)
- [Visual Component Testing](../development/visual-component-testing.md)
- [Phase 5 Security Improvements](../security/phase5-security-improvements.md)

### Configuration Examples

- [Sample Configuration Files](../configuration/sample-configs/)
- [Performance Tuning Examples](../configuration/performance-tuning/)
- [Advanced Workflow Templates](../configuration/workflow-templates/)

### Community Resources

- [GitHub Issues and Discussions](https://github.com/your-org/vs-mcp-server)
- [Stack Overflow Tags](https://stackoverflow.com/questions/tagged/vs-mcp-server)
- [Community Wiki](https://wiki.community.com/vs-mcp-server)

### Support Channels

- **Technical Support**: support@your-org.com
- **Bug Reports**: bugs@your-org.com
- **Feature Requests**: features@your-org.com
- **Community Forum**: https://community.your-org.com

---

## 🔖 Quick Reference

### Essential Commands

```markdown
# Basic window capture
@vsmcp vs_capture_window --window-handle <handle>

# Full IDE capture
@vsmcp vs_capture_full_ide

# Visual state analysis
@vsmcp vs_analyse_visual_state

# Performance monitoring
@vsmcp vs_analyse_performance

# System status check
@vsmcp vs_check_status
```

### Quality Settings Quick Reference

| Use Case | Quality | Memory | Speed | Best For |
|----------|---------|--------|-------|----------|
| Quick checks | fast | Low | Fast | Status verification |
| General use | balanced | Medium | Good | Daily development |
| Documentation | high | High | Slower | Detailed analysis |
| Presentations | ultra | Very High | Slowest | Professional output |

### Troubleshooting Quick Fixes

1. **Slow Performance**: Use `fast` quality, reduce dimensions
2. **Memory Issues**: Restart MCP server, check system memory
3. **Blurry Images**: Increase quality, check DPI settings
4. **COM Errors**: Restart Visual Studio, check permissions
5. **Connection Issues**: Restart MCP server, verify configuration

---

*This user guide provides comprehensive coverage of visual capture capabilities. For specific technical issues or advanced customisation requirements, refer to the detailed architecture and development documentation.*