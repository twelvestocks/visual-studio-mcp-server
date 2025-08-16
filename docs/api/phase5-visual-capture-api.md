# Phase 5 Visual Capture API Reference

This document provides comprehensive API documentation for the three new MCP tools introduced in Phase 5 Advanced Visual Capture: `vs_capture_window`, `vs_capture_full_ide`, and `vs_analyse_visual_state`.

## 📋 Overview

Phase 5 introduces powerful visual capture capabilities that enable Claude Code to understand the complete Visual Studio interface state, providing context-aware assistance for UI/UX issues, debugging visual problems, and analyzing code layout.

### 🔧 New MCP Tools

| Tool Name | Purpose | Primary Use Cases |
|-----------|---------|-------------------|
| [`vs_capture_window`](#vs_capture_window) | Capture specific VS windows | Targeted screenshots, error dialogs, specific panels |
| [`vs_capture_full_ide`](#vs_capture_full_ide) | Capture complete IDE layout | Full context analysis, layout comparison, comprehensive debugging |
| [`vs_analyse_visual_state`](#vs_analyse_visual_state) | Compare and analyze visual states | Before/after comparisons, layout change detection |

---

## 🎯 `vs_capture_window`

Captures a specific Visual Studio window by handle, title pattern, or window type with optional annotations and metadata.

### Request Schema

```json
{
  "tool": "vs_capture_window",
  "arguments": {
    "window_handle": "string (optional)",
    "window_title": "string (optional)", 
    "window_type": "string (optional)",
    "include_metadata": "boolean (default: true)",
    "include_annotations": "boolean (default: true)",
    "capture_children": "boolean (default: false)",
    "image_format": "string (default: PNG)",
    "max_dimension": "integer (optional)"
  }
}
```

### Parameters

| Parameter | Type | Required | Description | Default | Validation |
|-----------|------|----------|-------------|---------|------------|
| `window_handle` | string | No | Window handle as hex string (e.g., "0x1A2B3C") | null | Valid IntPtr format |
| `window_title` | string | No | Full or partial window title to match | null | 1-256 characters |
| `window_type` | string | No | VS window type (see [Window Types](#window-types)) | null | Valid enum value |
| `include_metadata` | boolean | No | Include window bounds, properties, relationships | true | - |
| `include_annotations` | boolean | No | Add visual annotations (borders, labels, highlights) | true | - |
| `capture_children` | boolean | No | Include child windows in capture | false | - |
| `image_format` | string | No | Output format: PNG, JPEG, WebP | "PNG" | PNG, JPEG, WebP |
| `max_dimension` | integer | No | Maximum width/height (scales proportionally) | null | 100-8192 |

### Response Schema

```json
{
  "success": true,
  "window": {
    "handle": "string",
    "title": "string", 
    "window_type": "string",
    "bounds": {
      "x": "integer",
      "y": "integer", 
      "width": "integer",
      "height": "integer"
    },
    "is_visible": "boolean",
    "is_active": "boolean",
    "process_id": "integer",
    "parent_handle": "string",
    "child_windows": "array"
  },
  "capture": {
    "image_data": "string (base64)",
    "image_format": "string",
    "width": "integer",
    "height": "integer",
    "file_size": "integer",
    "captured_at": "string (ISO 8601)"
  },
  "annotations": {
    "borders_added": "boolean",
    "labels_added": "boolean", 
    "highlights_added": "boolean",
    "annotation_count": "integer"
  },
  "performance": {
    "capture_time_ms": "integer",
    "memory_used_mb": "number",
    "windows_enumerated": "integer"
  }
}
```

### Window Types

The following window types are supported for the `window_type` parameter:

| Type | Description | Detection Method |
|------|-------------|------------------|
| `MainWindow` | Primary VS IDE window | Title contains "Microsoft Visual Studio" |
| `SolutionExplorer` | Project structure panel | Title "Solution Explorer" |
| `PropertiesWindow` | Object properties panel | Title "Properties" |
| `ErrorList` | Build errors and warnings | Title "Error List" |
| `OutputWindow` | Build and debug output | Title "Output" |
| `CodeEditor` | Source code editing windows | File extensions in title |
| `XamlDesigner` | XAML visual designer | Title contains ".xaml [Design]" |
| `Toolbox` | Draggable controls panel | Title "Toolbox" |
| `ServerExplorer` | Database connections | Title "Server Explorer" |
| `TeamExplorer` | Source control panel | Title "Team Explorer" |
| `PackageManagerConsole` | NuGet PowerShell console | Title "Package Manager Console" |
| `FindAndReplace` | Text search dialog | Title "Find and Replace" |
| `ImmediateWindow` | Debug expression evaluator | Title "Immediate" |
| `WatchWindow` | Variable monitoring | Title "Watch" |
| `CallStackWindow` | Execution stack | Title "Call Stack" |
| `LocalsWindow` | Local variables | Title "Locals" |

### Usage Examples

#### Example 1: Capture Solution Explorer
```json
{
  "tool": "vs_capture_window",
  "arguments": {
    "window_type": "SolutionExplorer",
    "include_annotations": true,
    "image_format": "PNG"
  }
}
```

#### Example 2: Capture by Window Title
```json
{
  "tool": "vs_capture_window", 
  "arguments": {
    "window_title": "Program.cs",
    "include_metadata": true,
    "capture_children": false
  }
}
```

#### Example 3: Capture with Size Limit
```json
{
  "tool": "vs_capture_window",
  "arguments": {
    "window_handle": "0x1A2B3C4D",
    "max_dimension": 1920,
    "image_format": "JPEG"
  }
}
```

### Error Responses

| Error Code | Description | Resolution |
|------------|-------------|------------|
| `WINDOW_NOT_FOUND` | Specified window not found | Verify window exists and is visible |
| `INVALID_HANDLE` | Window handle format invalid | Use hex format: "0x1A2B3C4D" |
| `ACCESS_DENIED` | Cannot access window process | Window may be system-protected |
| `CAPTURE_FAILED` | Image capture operation failed | Check memory available, window state |
| `MEMORY_PRESSURE` | Insufficient memory for capture | Reduce max_dimension or close applications |
| `TIMEOUT` | Operation exceeded time limit | Window may be unresponsive |

### Performance Characteristics

| Operation | Typical Time | Memory Usage | Notes |
|-----------|-------------|--------------|--------|
| Window Discovery | 50-200ms | <1MB | Cached after first call |
| Single Window Capture | 100-500ms | 5-25MB | Depends on window size |
| Annotation Processing | 50-150ms | <2MB | Per annotation type |
| Large Window (4K) | 1-3 seconds | 50MB+ | Memory pressure monitoring |

---

## 🖥️ `vs_capture_full_ide`

Captures the complete Visual Studio IDE layout including all visible windows, panels, and their spatial relationships with comprehensive metadata.

### Request Schema

```json
{
  "tool": "vs_capture_full_ide", 
  "arguments": {
    "include_layout_metadata": "boolean (default: true)",
    "capture_annotations": "boolean (default: true)", 
    "multi_monitor_support": "boolean (default: true)",
    "layout_analysis_depth": "string (default: complete)",
    "image_format": "string (default: PNG)",
    "compression_level": "integer (default: 6)",
    "max_total_dimension": "integer (optional)"
  }
}
```

### Parameters

| Parameter | Type | Required | Description | Default | Validation |
|-----------|------|----------|-------------|---------|------------|
| `include_layout_metadata` | boolean | No | Complete layout analysis with relationships | true | - |
| `capture_annotations` | boolean | No | Visual annotations (window borders, labels) | true | - |
| `multi_monitor_support` | boolean | No | Detect and capture across multiple monitors | true | - |
| `layout_analysis_depth` | string | No | Analysis level: basic, standard, complete | "complete" | Valid enum value |
| `image_format` | string | No | Output format with size optimization | "PNG" | PNG, JPEG, WebP |
| `compression_level` | integer | No | Compression level (1-9 for PNG, 1-100 for JPEG) | 6 | 1-100 |
| `max_total_dimension` | integer | No | Maximum total dimension (all monitors) | null | 1000-16384 |

### Response Schema

```json
{
  "success": true,
  "layout": {
    "main_window": {
      "handle": "string",
      "title": "string", 
      "bounds": "object",
      "is_maximized": "boolean"
    },
    "all_windows": [
      {
        "handle": "string",
        "title": "string",
        "window_type": "string", 
        "bounds": "object",
        "is_visible": "boolean",
        "is_active": "boolean",
        "parent_handle": "string",
        "z_order": "integer"
      }
    ],
    "windows_by_type": {
      "CodeEditor": "array",
      "SolutionExplorer": "array",
      "PropertiesWindow": "array"
    },
    "docking_layout": {
      "left_docked_panels": "array",
      "right_docked_panels": "array", 
      "top_docked_panels": "array",
      "bottom_docked_panels": "array",
      "floating_panels": "array",
      "editor_area": "object"
    },
    "active_window": "object",
    "total_windows": "integer",
    "analysis_time": "string (ISO 8601)",
    "layout_hash": "string"
  },
  "capture": {
    "image_data": "string (base64)",
    "image_format": "string", 
    "total_width": "integer",
    "total_height": "integer",
    "file_size": "integer",
    "captured_at": "string (ISO 8601)",
    "monitor_count": "integer",
    "monitor_info": [
      {
        "monitor_id": "integer",
        "bounds": "object",
        "is_primary": "boolean",
        "dpi": "integer",
        "scale_factor": "number"
      }
    ]
  },
  "annotations": {
    "window_borders": "boolean",
    "window_labels": "boolean",
    "docking_indicators": "boolean", 
    "active_window_highlight": "boolean",
    "layout_grid": "boolean"
  },
  "performance": {
    "total_capture_time_ms": "integer",
    "layout_analysis_time_ms": "integer", 
    "image_processing_time_ms": "integer",
    "memory_peak_mb": "number",
    "windows_processed": "integer",
    "monitors_captured": "integer"
  }
}
```

### Layout Analysis Depth

| Depth Level | Analysis Included | Performance Impact | Use Cases |
|-------------|-------------------|-------------------|-----------|
| `basic` | Window enumeration, basic positioning | Fast (100-300ms) | Quick layout overview |
| `standard` | + Window relationships, docking detection | Medium (300-800ms) | Standard layout analysis |
| `complete` | + Spatial analysis, overlap detection, metadata | Full (500-1500ms) | Comprehensive analysis |

### Usage Examples

#### Example 1: Complete IDE Capture
```json
{
  "tool": "vs_capture_full_ide",
  "arguments": {
    "include_layout_metadata": true,
    "capture_annotations": true,
    "layout_analysis_depth": "complete"
  }
}
```

#### Example 2: Multi-Monitor Capture
```json
{
  "tool": "vs_capture_full_ide",
  "arguments": {
    "multi_monitor_support": true,
    "max_total_dimension": 7680,
    "compression_level": 8,
    "image_format": "WebP"
  }
}
```

#### Example 3: Fast Layout Overview
```json
{
  "tool": "vs_capture_full_ide",
  "arguments": {
    "layout_analysis_depth": "basic",
    "capture_annotations": false,
    "include_layout_metadata": false
  }
}
```

### Memory Management

| Scenario | Memory Usage | Mitigation Strategy |
|----------|--------------|-------------------|
| Single Monitor 1080p | 15-30MB | Standard processing |
| Single Monitor 4K | 50-80MB | Memory pressure monitoring |
| Dual Monitor 1080p | 30-60MB | Automatic compression |
| Dual Monitor 4K | 100MB+ | Size limiting, WebP format |
| Triple Monitor+ | 150MB+ | Capture rejection, scaling |

---

## 🔍 `vs_analyse_visual_state`

Compares visual states between captures, detecting layout changes, window modifications, and generating comprehensive diff reports.

### Request Schema

```json
{
  "tool": "vs_analyse_visual_state",
  "arguments": {
    "comparison_mode": "string (default: layout_diff)",
    "baseline_capture": "object (optional)",
    "current_capture": "object (optional)", 
    "capture_current": "boolean (default: true)",
    "diff_sensitivity": "string (default: medium)",
    "highlight_changes": "boolean (default: true)",
    "generate_report": "boolean (default: true)",
    "report_format": "string (default: markdown)"
  }
}
```

### Parameters

| Parameter | Type | Required | Description | Default | Validation |
|-----------|------|----------|-------------|---------|------------|
| `comparison_mode` | string | No | Type of comparison to perform | "layout_diff" | Valid mode |
| `baseline_capture` | object | No | Previous capture for comparison | null | Valid capture object |
| `current_capture` | object | No | Current capture for comparison | null | Valid capture object |
| `capture_current` | boolean | No | Automatically capture current state | true | - |
| `diff_sensitivity` | string | No | Change detection sensitivity | "medium" | low, medium, high |
| `highlight_changes` | boolean | No | Visual highlighting of differences | true | - |
| `generate_report` | boolean | No | Generate detailed analysis report | true | - |
| `report_format` | string | No | Report output format | "markdown" | markdown, json, html |

### Comparison Modes

| Mode | Description | Use Cases |
|------|-------------|-----------|
| `layout_diff` | Window positions, sizes, docking changes | Layout reorganization detection |
| `content_diff` | Visual content changes within windows | Code changes, UI updates |
| `window_diff` | Window appearance/disappearance | New panels, closed dialogs |
| `complete_diff` | All change types combined | Comprehensive state analysis |

### Response Schema

```json
{
  "success": true,
  "analysis": {
    "comparison_mode": "string",
    "baseline_timestamp": "string (ISO 8601)",
    "current_timestamp": "string (ISO 8601)",
    "analysis_timestamp": "string (ISO 8601)"
  },
  "changes_detected": {
    "layout_changes": {
      "window_moves": "array",
      "window_resizes": "array", 
      "docking_changes": "array",
      "new_windows": "array",
      "closed_windows": "array"
    },
    "content_changes": {
      "modified_windows": "array",
      "text_changes": "array",
      "visual_updates": "array"
    },
    "summary": {
      "total_changes": "integer",
      "change_severity": "string",
      "major_layout_change": "boolean",
      "new_errors_detected": "boolean"
    }
  },
  "diff_visualization": {
    "image_data": "string (base64)",
    "image_format": "string",
    "width": "integer", 
    "height": "integer",
    "highlights_applied": "boolean",
    "legend_included": "boolean"
  },
  "detailed_report": {
    "format": "string",
    "content": "string",
    "sections": "array",
    "recommendations": "array"
  },
  "performance": {
    "analysis_time_ms": "integer",
    "memory_used_mb": "number",
    "changes_processed": "integer"
  }
}
```

### Diff Sensitivity Levels

| Sensitivity | Detection Threshold | Performance | Use Cases |
|-------------|-------------------|-------------|-----------|
| `low` | Major changes only (>50px movement) | Fast | High-level layout monitoring |
| `medium` | Moderate changes (>10px movement) | Standard | General change detection |
| `high` | Minor changes (>2px movement) | Slower | Precise layout analysis |

### Usage Examples

#### Example 1: Layout Change Detection
```json
{
  "tool": "vs_analyse_visual_state",
  "arguments": {
    "comparison_mode": "layout_diff",
    "capture_current": true,
    "diff_sensitivity": "medium",
    "highlight_changes": true
  }
}
```

#### Example 2: Before/After Comparison
```json
{
  "tool": "vs_analyse_visual_state",
  "arguments": {
    "comparison_mode": "complete_diff",
    "baseline_capture": {
      "layout_hash": "abc123def456",
      "timestamp": "2025-08-15T10:00:00Z"
    },
    "capture_current": true,
    "generate_report": true,
    "report_format": "html"
  }
}
```

#### Example 3: Content Change Analysis
```json
{
  "tool": "vs_analyse_visual_state", 
  "arguments": {
    "comparison_mode": "content_diff",
    "diff_sensitivity": "high",
    "highlight_changes": true,
    "generate_report": false
  }
}
```

---

## 🛠️ Integration Guidelines

### Claude Code Integration

The Phase 5 visual capture tools integrate seamlessly with Claude Code workflows:

```python
# Example Claude Code integration
async def analyze_visual_studio_issue():
    # Capture current IDE state
    ide_capture = await mcp_client.call_tool("vs_capture_full_ide", {
        "include_layout_metadata": True,
        "capture_annotations": True
    })
    
    # Analyze specific problem window  
    error_window = await mcp_client.call_tool("vs_capture_window", {
        "window_type": "ErrorList",
        "include_annotations": True
    })
    
    # Generate analysis report
    return f"""
    Visual Studio Analysis:
    - Total windows: {ide_capture['layout']['total_windows']}
    - Active window: {ide_capture['layout']['active_window']['title']}
    - Errors detected: {len(error_window.get('errors', []))}
    - Layout complexity: {ide_capture['layout']['docking_layout']}
    """
```

### Best Practices

#### 1. Memory Management
- Use `max_dimension` for large displays to prevent memory issues
- Enable compression for multi-monitor setups
- Monitor memory usage in logs for optimization

#### 2. Performance Optimization  
- Use `basic` analysis depth for frequent captures
- Cache window discovery results when possible
- Leverage `window_type` filtering for targeted captures

#### 3. Error Handling
- Always handle `MEMORY_PRESSURE` responses gracefully
- Implement retry logic for `TIMEOUT` errors
- Check `success` field before processing results

#### 4. Security Considerations
- Validate all window handles before capture
- Monitor for suspicious access patterns
- Implement rate limiting for frequent captures

---

## 🚨 Error Handling and Troubleshooting

### Common Error Scenarios

| Error | Cause | Solution |
|-------|-------|----------|
| `WINDOW_NOT_FOUND` | Window closed/hidden | Verify window existence first |
| `MEMORY_PRESSURE` | Large capture request | Reduce dimensions, use compression |
| `ACCESS_DENIED` | System window access | Skip protected windows |
| `TIMEOUT` | Unresponsive window | Increase timeout, retry later |
| `INVALID_PARAMETER` | Bad request format | Validate against schema |

### Performance Troubleshooting

| Issue | Symptoms | Resolution |
|-------|----------|------------|
| Slow captures | >5 second response | Check system memory, reduce quality |
| Memory errors | OutOfMemory exceptions | Lower max_dimension, enable compression |
| Incomplete layouts | Missing windows | Increase timeout, check window visibility |
| Large file sizes | >50MB responses | Use WebP format, increase compression |

### Monitoring and Diagnostics

The tools provide comprehensive performance metrics:

```json
{
  "performance": {
    "capture_time_ms": 1250,
    "memory_used_mb": 45.2,
    "windows_enumerated": 23,
    "timeout_events": 0,
    "memory_pressure_events": 1
  }
}
```

Monitor these metrics for:
- **Performance degradation** - Increasing capture times
- **Memory pressure** - High memory usage values  
- **Timeout issues** - Non-zero timeout events
- **System health** - Overall resource utilization

---

## 📈 API Evolution and Versioning

### Current Version: 1.0.0
Phase 5 introduces the initial stable API with comprehensive feature set.

### Backward Compatibility
- All APIs maintain backward compatibility within major versions
- Deprecated features provide 6-month migration timeline
- Schema validation ensures request/response consistency

### Planned Enhancements
- **Real-time capture streaming** for live monitoring
- **AI-powered layout optimization** suggestions
- **Custom annotation plugins** for specialized analysis
- **Performance profiling integration** with capture data

---

## 📚 Additional Resources

### Code Examples
- [Complete integration examples](../examples/phase5-code-examples.md)
- [Error handling patterns](../development/error-handling-patterns.md)
- [Performance optimization guide](../performance/phase5-optimization-guide.md)

### Related Documentation  
- [Window Management Architecture](../architecture/window-management-architecture.md)
- [Security Implementation](../security/phase5-security-improvements.md)
- [Testing Strategy](../testing/phase5-test-strategy.md)

### Support and Community
- **GitHub Issues**: Report bugs and request features
- **Documentation**: Comprehensive guides and tutorials
- **Performance**: Optimization guidelines and best practices

---

*This API reference is maintained as part of the Phase 5 implementation and updated with each release.*