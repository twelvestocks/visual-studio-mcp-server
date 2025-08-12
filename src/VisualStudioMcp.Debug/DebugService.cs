using Microsoft.Extensions.Logging;
using VisualStudioMcp.Core;
using EnvDTE;
using EnvDTE80;
using System.Runtime.InteropServices;

namespace VisualStudioMcp.Debug;

/// <summary>
/// Implementation of Visual Studio debugging automation service.
/// </summary>
public class DebugService : IDebugService
{
    private readonly ILogger<DebugService> _logger;
    private readonly IVisualStudioService _visualStudioService;
    private readonly Dictionary<int, WeakReference<Debugger2>> _debuggerInstances = new();
    private readonly Timer _healthCheckTimer;

    /// <summary>
    /// Initializes a new instance of the DebugService with required dependencies.
    /// </summary>
    /// <param name="logger">Logger for debugging operations.</param>
    /// <param name="visualStudioService">Core Visual Studio automation service.</param>
    public DebugService(ILogger<DebugService> logger, IVisualStudioService visualStudioService)
    {
        _logger = logger;
        _visualStudioService = visualStudioService;
        
        // Set up health monitoring for debugger instances
        _healthCheckTimer = new Timer(PerformHealthCheck, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Starts debugging the specified project or the startup project.
    /// </summary>
    /// <param name="projectName">Optional project name to debug. If null, uses startup project.</param>
    /// <returns>The current debug state after starting.</returns>
    public async Task<DebugState> StartDebuggingAsync(string? projectName = null)
    {
        return await Task.Run(async () =>
        {
            _logger.LogInformation("Starting debugging for project: {ProjectName}", projectName ?? "startup project");

            try
            {
                var debugger = await GetActiveDebuggerAsync();
                if (debugger == null)
                {
                    throw new InvalidOperationException("No active Visual Studio debugger found. Ensure Visual Studio is connected.");
                }

                // If already debugging, return current state
                if (debugger.CurrentMode != dbgDebugMode.dbgDesignMode)
                {
                    _logger.LogWarning("Debugging is already active with mode: {Mode}", debugger.CurrentMode);
                    return GetDebugStateFromDebugger(debugger);
                }

                // Get the DTE from the debugger to access solution
                var dte = debugger.Parent;
                
                if (string.IsNullOrEmpty(projectName))
                {
                    // Start debugging with startup project
                    _logger.LogDebug("Starting debugging with startup project");
                    debugger.Go(false);
                }
                else
                {
                    // Find and set specific project as startup project
                    var projects = dte.Solution.Projects;
                    EnvDTE.Project? targetProject = null;

                    foreach (EnvDTE.Project project in projects)
                    {
                        if (project.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase))
                        {
                            targetProject = project;
                            break;
                        }
                    }

                    if (targetProject == null)
                    {
                        throw new ArgumentException($"Project '{projectName}' not found in solution");
                    }

                    _logger.LogDebug("Starting debugging with project: {ProjectName}", targetProject.Name);
                    
                    // Set as startup project and start debugging
                    var solutionBuild = dte.Solution.SolutionBuild;
                    solutionBuild.StartupProjects = targetProject.UniqueName;
                    debugger.Go(false);
                }

                // Wait a moment for debugging to start
                System.Threading.Thread.Sleep(1000);
                
                var state = GetDebugStateFromDebugger(debugger);
                _logger.LogInformation("Debugging started successfully. Mode: {Mode}, IsPaused: {IsPaused}", state.Mode, state.IsPaused);
                
                return state;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start debugging");
                throw;
            }
        });
    }

    /// <summary>
    /// Stops the current debugging session.
    /// </summary>
    public async Task StopDebuggingAsync()
    {
        await Task.Run(async () =>
        {
            _logger.LogInformation("Stopping debugging session");

            try
            {
                var debugger = await GetActiveDebuggerAsync();
                if (debugger == null)
                {
                    _logger.LogWarning("No active debugger found. Debugging may already be stopped.");
                    return;
                }

                if (debugger.CurrentMode == dbgDebugMode.dbgDesignMode)
                {
                    _logger.LogInformation("Debugging is already stopped");
                    return;
                }

                _logger.LogDebug("Terminating debugging session with mode: {Mode}", debugger.CurrentMode);
                debugger.Stop(true); // true = terminate all processes

                // Wait a moment for debugging to stop
                System.Threading.Thread.Sleep(500);
                
                _logger.LogInformation("Debugging session stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop debugging session");
                throw;
            }
        });
    }

    /// <summary>
    /// Gets the current state of the debugger.
    /// </summary>
    /// <returns>The current debug state.</returns>
    public async Task<DebugState> GetCurrentStateAsync()
    {
        return await Task.Run(async () =>
        {
            _logger.LogDebug("Getting current debug state");

            try
            {
                var debugger = await GetActiveDebuggerAsync();
                if (debugger == null)
                {
                    _logger.LogDebug("No active debugger found, returning design mode state");
                    return new DebugState { IsDebugging = false, IsPaused = false, Mode = "Design" };
                }

                return GetDebugStateFromDebugger(debugger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get current debug state");
                throw;
            }
        });
    }

    /// <summary>
    /// Gets all breakpoints in the current debugging session.
    /// </summary>
    /// <returns>Array of breakpoints.</returns>
    public async Task<Breakpoint[]> GetBreakpointsAsync()
    {
        return await Task.Run(async () =>
        {
            _logger.LogDebug("Getting all breakpoints");

            try
            {
                var debugger = await GetActiveDebuggerAsync();
                if (debugger == null)
                {
                    _logger.LogDebug("No active debugger found, returning empty breakpoints array");
                    return Array.Empty<Breakpoint>();
                }

                var breakpoints = new List<Breakpoint>();
                
                foreach (EnvDTE.Breakpoint dteBreakpoint in debugger.Breakpoints)
                {
                    try
                    {
                        var breakpoint = new Breakpoint
                        {
                            Id = $"bp_{dteBreakpoint.Name}",
                            File = dteBreakpoint.File ?? string.Empty,
                            Line = dteBreakpoint.FileLine,
                            Condition = dteBreakpoint.Condition ?? string.Empty,
                            IsEnabled = dteBreakpoint.Enabled
                        };
                        breakpoints.Add(breakpoint);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract breakpoint information for breakpoint: {Name}", dteBreakpoint.Name);
                    }
                }

                _logger.LogDebug("Retrieved {Count} breakpoints", breakpoints.Count);
                return breakpoints.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get breakpoints");
                throw;
            }
        });
    }

    /// <summary>
    /// Adds a breakpoint at the specified location.
    /// </summary>
    /// <param name="file">The file path.</param>
    /// <param name="line">The line number.</param>
    /// <param name="condition">Optional breakpoint condition.</param>
    /// <returns>The created breakpoint.</returns>
    public async Task<Breakpoint> AddBreakpointAsync(string file, int line, string? condition = null)
    {
        return await Task.Run(async () =>
        {
            _logger.LogInformation("Adding breakpoint at {File}:{Line} with condition: {Condition}", file, line, condition);

            try
            {
                var debugger = await GetActiveDebuggerAsync();
                if (debugger == null)
                {
                    throw new InvalidOperationException("No active Visual Studio debugger found. Ensure Visual Studio is connected.");
                }

                // Validate file path
                if (string.IsNullOrWhiteSpace(file))
                {
                    throw new ArgumentException("File path cannot be null or empty", nameof(file));
                }

                if (line <= 0)
                {
                    throw new ArgumentException("Line number must be greater than zero", nameof(line));
                }

                // Add breakpoint using DTE - first add without condition
                var breakpointCollection = debugger.Breakpoints.Add(null, file, line);
                
                // Get the actual breakpoint from the collection (should be the last one added)
                EnvDTE.Breakpoint? dteBreakpoint = null;
                if (breakpointCollection.Count > 0)
                {
                    dteBreakpoint = breakpointCollection.Item(breakpointCollection.Count);
                }
                
                if (dteBreakpoint == null)
                {
                    throw new InvalidOperationException($"Failed to create breakpoint at {file}:{line}");
                }
                
                // For conditions, we need to work with the breakpoint after it's created
                // Note: Condition setting in EnvDTE can be complex and may require different approaches
                // For now, we'll log the condition request but may need to implement via different DTE methods

                var breakpoint = new Breakpoint
                {
                    Id = $"bp_{dteBreakpoint.Name}",
                    File = dteBreakpoint.File ?? string.Empty,
                    Line = dteBreakpoint.FileLine,
                    Condition = condition ?? string.Empty, // Use the requested condition for now
                    IsEnabled = dteBreakpoint.Enabled
                };
                
                // Log condition request for future implementation
                if (!string.IsNullOrWhiteSpace(condition))
                {
                    _logger.LogWarning("Breakpoint condition requested but not yet implemented: {Condition}", condition);
                }

                _logger.LogInformation("Successfully added breakpoint: {Id} at {File}:{Line}", breakpoint.Id, breakpoint.File, breakpoint.Line);
                return breakpoint;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add breakpoint at {File}:{Line}", file, line);
                throw;
            }
        });
    }

    /// <summary>
    /// Gets the local variables in the current debugging context.
    /// </summary>
    /// <returns>Array of local variables.</returns>
    public async Task<Variable[]> GetLocalVariablesAsync()
    {
        return await Task.Run(async () =>
        {
            _logger.LogDebug("Getting local variables");

            try
            {
                var debugger = await GetActiveDebuggerAsync();
                if (debugger == null)
                {
                    _logger.LogDebug("No active debugger found, returning empty variables array");
                    return Array.Empty<Variable>();
                }

                // Check if debugging is paused (variables only available when paused)
                if (debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                {
                    _logger.LogDebug("Debugger is not paused (mode: {Mode}), cannot retrieve variables", debugger.CurrentMode);
                    return Array.Empty<Variable>();
                }

                var variables = new List<Variable>();

                try
                {
                    // Get current stack frame
                    var currentThread = debugger.CurrentThread;
                    if (currentThread?.StackFrames?.Count > 0)
                    {
                        var topFrame = currentThread.StackFrames.Item(1); // 1-based indexing
                        
                        // Get locals from the top frame
                        foreach (EnvDTE.Expression expression in topFrame.Locals)
                        {
                            try
                            {
                                var variable = new Variable
                                {
                                    Name = expression.Name,
                                    Value = expression.Value?.ToString() ?? "<null>",
                                    Type = expression.Type ?? "unknown",
                                    Scope = "Local"
                                };
                                variables.Add(variable);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to extract variable information for: {Name}", expression.Name);
                            }
                        }

                        // Get arguments/parameters
                        foreach (EnvDTE.Expression expression in topFrame.Arguments)
                        {
                            try
                            {
                                var variable = new Variable
                                {
                                    Name = expression.Name,
                                    Value = expression.Value?.ToString() ?? "<null>",
                                    Type = expression.Type ?? "unknown",
                                    Scope = "Parameter"
                                };
                                variables.Add(variable);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to extract parameter information for: {Name}", expression.Name);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to access stack frame for variable inspection");
                }

                _logger.LogDebug("Retrieved {Count} variables", variables.Count);
                return variables.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get local variables");
                throw;
            }
        });
    }

    /// <summary>
    /// Gets the current call stack.
    /// </summary>
    /// <returns>Array of call stack frames.</returns>
    public async Task<CallStackFrame[]> GetCallStackAsync()
    {
        return await Task.Run(async () =>
        {
            _logger.LogDebug("Getting call stack");

            try
            {
                var debugger = await GetActiveDebuggerAsync();
                if (debugger == null)
                {
                    _logger.LogDebug("No active debugger found, returning empty call stack");
                    return Array.Empty<CallStackFrame>();
                }

                // Check if debugging is paused (call stack only available when paused)
                if (debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                {
                    _logger.LogDebug("Debugger is not paused (mode: {Mode}), cannot retrieve call stack", debugger.CurrentMode);
                    return Array.Empty<CallStackFrame>();
                }

                var frames = new List<CallStackFrame>();

                try
                {
                    var currentThread = debugger.CurrentThread;
                    if (currentThread?.StackFrames != null)
                    {
                        foreach (EnvDTE.StackFrame stackFrame in currentThread.StackFrames)
                        {
                            try
                            {
                                var frame = new CallStackFrame
                                {
                                    Method = stackFrame.FunctionName ?? "Unknown",
                                    File = ExtractFileName(stackFrame.FunctionName),
                                    Line = 0, // Line number not directly available from StackFrame
                                    Module = stackFrame.Module ?? "Unknown"
                                };
                                frames.Add(frame);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to extract stack frame information");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to access call stack");
                }

                _logger.LogDebug("Retrieved {Count} call stack frames", frames.Count);
                return frames.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get call stack");
                throw;
            }
        });
    }

    #region Helper Methods

    /// <summary>
    /// Gets the active debugger instance from the connected Visual Studio instance.
    /// </summary>
    /// <returns>The active debugger or null if not available.</returns>
    private async Task<Debugger2?> GetActiveDebuggerAsync()
    {
        try
        {
            // Get the connected Visual Studio instances
            var instances = await _visualStudioService.GetRunningInstancesAsync();
            
            foreach (var instance in instances)
            {
                // Check if we already have a debugger reference for this process
                if (_debuggerInstances.TryGetValue(instance.ProcessId, out var weakRef) &&
                    weakRef.TryGetTarget(out var existingDebugger))
                {
                    return existingDebugger;
                }

                // Try to connect to this instance to get the DTE
                try
                {
                    await _visualStudioService.ConnectToInstanceAsync(instance.ProcessId);
                    
                    // For now, we'll use reflection to access the private method
                    // This is not ideal but necessary until we can refactor the architecture
                    var serviceType = _visualStudioService.GetType();
                    var getConnectedInstanceMethod = serviceType.GetMethod("GetConnectedInstance", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (getConnectedInstanceMethod != null)
                    {
                        var dte = getConnectedInstanceMethod.Invoke(_visualStudioService, new object[] { instance.ProcessId }) as DTE;
                        if (dte is DTE2 dte2)
                        {
                            var debugger = dte2.Debugger as Debugger2;
                            if (debugger != null)
                            {
                                // Store weak reference for future use
                                _debuggerInstances[instance.ProcessId] = new WeakReference<Debugger2>(debugger);
                                return debugger;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to connect to Visual Studio instance {ProcessId}", instance.ProcessId);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active debugger");
            return null;
        }
    }

    /// <summary>
    /// Converts a DTE debugger state to our DebugState model.
    /// </summary>
    /// <param name="debugger">The DTE debugger instance.</param>
    /// <returns>The converted debug state.</returns>
    private DebugState GetDebugStateFromDebugger(Debugger2 debugger)
    {
        try
        {
            var mode = debugger.CurrentMode;
            var isDebugging = mode != dbgDebugMode.dbgDesignMode;
            var isPaused = mode == dbgDebugMode.dbgBreakMode;

            var state = new DebugState
            {
                IsDebugging = isDebugging,
                IsPaused = isPaused,
                Mode = mode.ToString().Replace("dbg", "").Replace("Mode", "")
            };

            // Try to get current execution location if paused
            if (isPaused && debugger.CurrentThread?.StackFrames?.Count > 0)
            {
                try
                {
                    var topFrame = debugger.CurrentThread.StackFrames.Item(1);
                    if (!string.IsNullOrEmpty(topFrame.FunctionName))
                    {
                        state.CurrentFile = ExtractFileName(topFrame.FunctionName);
                        // Line number would need more complex extraction from debug context
                        state.CurrentLine = 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not extract current execution location");
                }
            }

            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert debugger state");
            return new DebugState { IsDebugging = false, IsPaused = false, Mode = "Unknown" };
        }
    }

    /// <summary>
    /// Extracts file name from function name or returns empty string if not extractable.
    /// </summary>
    /// <param name="functionName">The full function name from stack frame.</param>
    /// <returns>The extracted file name or empty string.</returns>
    private static string ExtractFileName(string? functionName)
    {
        if (string.IsNullOrEmpty(functionName))
            return string.Empty;

        // Try to extract file information from function name
        // This is a simplified extraction - actual implementation may vary
        var parts = functionName.Split('.');
        if (parts.Length > 1)
        {
            return $"{parts[^2]}.cs"; // Assume C# files
        }

        return string.Empty;
    }

    /// <summary>
    /// Performs health check on debugger instances and cleans up dead references.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    private void PerformHealthCheck(object? state)
    {
        try
        {
            _logger.LogTrace("Performing debugger health check");

            var deadInstances = new List<int>();

            foreach (var kvp in _debuggerInstances.ToArray())
            {
                if (!kvp.Value.TryGetTarget(out var debugger))
                {
                    deadInstances.Add(kvp.Key);
                    continue;
                }

                try
                {
                    // Try to access a simple property to test if COM object is alive
                    _ = debugger.CurrentMode;
                }
                catch
                {
                    deadInstances.Add(kvp.Key);
                }
            }

            // Clean up dead instances
            foreach (var processId in deadInstances)
            {
                _debuggerInstances.Remove(processId);
                _logger.LogDebug("Removed dead debugger reference for process {ProcessId}", processId);
            }

            if (deadInstances.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} dead debugger references", deadInstances.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during debugger health check");
        }
    }

    /// <summary>
    /// Disposes resources and stops health monitoring.
    /// </summary>
    public void Dispose()
    {
        try
        {
            _healthCheckTimer?.Dispose();

            // Clean up all debugger references
            foreach (var kvp in _debuggerInstances.ToArray())
            {
                if (kvp.Value.TryGetTarget(out var debugger))
                {
                    try
                    {
                        Marshal.ReleaseComObject(debugger);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error releasing COM object during disposal");
                    }
                }
            }

            _debuggerInstances.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during DebugService disposal");
        }
    }

    #endregion
}