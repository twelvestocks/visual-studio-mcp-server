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
                                
                                // Try to get additional type information if available
                                try
                                {
                                    if (!string.IsNullOrEmpty(expression.Type))
                                    {
                                        // For complex types, try to get more detailed information
                                        if (expression.Type.Contains(".") || expression.Type.Contains("["))
                                        {
                                            variable.Type = expression.Type;
                                        }
                                    }
                                }
                                catch (Exception typeEx)
                                {
                                    _logger.LogDebug(typeEx, "Could not extract detailed type information for: {Name}", expression.Name);
                                }
                                
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
                                
                                // Try to get additional type information if available
                                try
                                {
                                    if (!string.IsNullOrEmpty(expression.Type))
                                    {
                                        // For complex types, try to get more detailed information
                                        if (expression.Type.Contains(".") || expression.Type.Contains("["))
                                        {
                                            variable.Type = expression.Type;
                                        }
                                    }
                                }
                                catch (Exception typeEx)
                                {
                                    _logger.LogDebug(typeEx, "Could not extract detailed type information for parameter: {Name}", expression.Name);
                                }
                                
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
                                    Line = 0,
                                    Module = stackFrame.Module ?? "Unknown"
                                };
                                
                                // Note: StackFrame in EnvDTE doesn't have FileName and LineNumber properties
                                // We'll rely on the existing ExtractFileName method for file information
                                
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

    /// <summary>
    /// Steps into the next statement or method call.
    /// </summary>
    /// <returns>The debug state after stepping.</returns>
    public async Task<DebugState> StepIntoAsync()
    {
        return await Task.Run(async () =>
        {
            _logger.LogInformation("Stepping into next statement");

            try
            {
                var debugger = await GetActiveDebuggerAsync();
                if (debugger == null)
                {
                    throw new InvalidOperationException("No active Visual Studio debugger found. Ensure Visual Studio is connected.");
                }

                // Check if debugging is paused
                if (debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                {
                    throw new InvalidOperationException("Debugger is not paused. Stepping is only available when execution is paused.");
                }

                // Perform step into operation
                debugger.StepInto();

                // Wait a moment for the step to complete
                await Task.Delay(100);

                var state = GetDebugStateFromDebugger(debugger);
                _logger.LogInformation("Step into completed. Mode: {Mode}, IsPaused: {IsPaused}", state.Mode, state.IsPaused);

                return state;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to step into");
                throw;
            }
        });
    }

    /// <summary>
    /// Steps over the next statement or method call.
    /// </summary>
    /// <returns>The debug state after stepping.</returns>
    public async Task<DebugState> StepOverAsync()
    {
        return await Task.Run(async () =>
        {
            _logger.LogInformation("Stepping over next statement");

            try
            {
                var debugger = await GetActiveDebuggerAsync();
                if (debugger == null)
                {
                    throw new InvalidOperationException("No active Visual Studio debugger found. Ensure Visual Studio is connected.");
                }

                // Check if debugging is paused
                if (debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                {
                    throw new InvalidOperationException("Debugger is not paused. Stepping is only available when execution is paused.");
                }

                // Perform step over operation
                debugger.StepOver();

                // Wait a moment for the step to complete
                await Task.Delay(100);

                var state = GetDebugStateFromDebugger(debugger);
                _logger.LogInformation("Step over completed. Mode: {Mode}, IsPaused: {IsPaused}", state.Mode, state.IsPaused);

                return state;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to step over");
                throw;
            }
        });
    }

    /// <summary>
    /// Steps out of the current method.
    /// </summary>
    /// <returns>The debug state after stepping.</returns>
    public async Task<DebugState> StepOutAsync()
    {
        return await Task.Run(async () =>
        {
            _logger.LogInformation("Stepping out of current method");

            try
            {
                var debugger = await GetActiveDebuggerAsync();
                if (debugger == null)
                {
                    throw new InvalidOperationException("No active Visual Studio debugger found. Ensure Visual Studio is connected.");
                }

                // Check if debugging is paused
                if (debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                {
                    throw new InvalidOperationException("Debugger is not paused. Stepping is only available when execution is paused.");
                }

                // Perform step out operation
                debugger.StepOut();

                // Wait a moment for the step to complete
                await Task.Delay(100);

                var state = GetDebugStateFromDebugger(debugger);
                _logger.LogInformation("Step out completed. Mode: {Mode}, IsPaused: {IsPaused}", state.Mode, state.IsPaused);

                return state;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to step out");
                throw;
            }
        });
    }

    /// <summary>
    /// Gets variables from a specific stack frame.
    /// </summary>
    /// <param name="frameIndex">The index of the stack frame (0 = top frame).</param>
    /// <returns>Array of variables from the specified frame.</returns>
    public async Task<Variable[]> GetVariablesFromFrameAsync(int frameIndex = 0)
    {
        return await Task.Run(async () =>
        {
            _logger.LogDebug("Getting variables from stack frame {FrameIndex}", frameIndex);

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
                        // Convert 0-based index to 1-based indexing used by EnvDTE
                        var dteFrameIndex = frameIndex + 1;
                        
                        if (dteFrameIndex <= currentThread.StackFrames.Count)
                        {
                            var frame = currentThread.StackFrames.Item(dteFrameIndex);
                            
                            // Get locals from the specified frame
                            foreach (EnvDTE.Expression expression in frame.Locals)
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
                                    
                                    // Try to get additional type information if available
                                    try
                                    {
                                        if (!string.IsNullOrEmpty(expression.Type))
                                        {
                                            // For complex types, preserve the full type information
                                            if (expression.Type.Contains(".") || expression.Type.Contains("["))
                                            {
                                                variable.Type = expression.Type;
                                            }
                                        }
                                    }
                                    catch (Exception typeEx)
                                    {
                                        _logger.LogDebug(typeEx, "Could not extract detailed type information for: {Name}", expression.Name);
                                    }
                                    
                                    variables.Add(variable);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to extract variable information for: {Name}", expression.Name);
                                }
                            }

                            // Get arguments/parameters
                            foreach (EnvDTE.Expression expression in frame.Arguments)
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
                                    
                                    // Try to get additional type information if available
                                    try
                                    {
                                        if (!string.IsNullOrEmpty(expression.Type))
                                        {
                                            // For complex types, preserve the full type information
                                            if (expression.Type.Contains(".") || expression.Type.Contains("["))
                                            {
                                                variable.Type = expression.Type;
                                            }
                                        }
                                    }
                                    catch (Exception typeEx)
                                    {
                                        _logger.LogDebug(typeEx, "Could not extract detailed type information for parameter: {Name}", expression.Name);
                                    }
                                    
                                    variables.Add(variable);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to extract parameter information for: {Name}", expression.Name);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Frame index {FrameIndex} is out of range. Stack has {FrameCount} frames", 
                                frameIndex, currentThread.StackFrames.Count);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to access stack frame {FrameIndex} for variable inspection", frameIndex);
                }

                _logger.LogDebug("Retrieved {Count} variables from frame {FrameIndex}", variables.Count, frameIndex);
                return variables.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get variables from frame {FrameIndex}", frameIndex);
                throw;
            }
        });
    }

    /// <summary>
    /// Gets a specific stack frame with detailed information.
    /// </summary>
    /// <param name="frameIndex">The index of the stack frame (0 = top frame).</param>
    /// <returns>The specified call stack frame.</returns>
    public async Task<CallStackFrame?> GetStackFrameAsync(int frameIndex = 0)
    {
        return await Task.Run(async () =>
        {
            _logger.LogDebug("Getting stack frame {FrameIndex}", frameIndex);

            try
            {
                var debugger = await GetActiveDebuggerAsync();
                if (debugger == null)
                {
                    _logger.LogDebug("No active debugger found");
                    return null;
                }

                // Check if debugging is paused (call stack only available when paused)
                if (debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                {
                    _logger.LogDebug("Debugger is not paused (mode: {Mode}), cannot retrieve call stack", debugger.CurrentMode);
                    return null;
                }

                try
                {
                    var currentThread = debugger.CurrentThread;
                    if (currentThread?.StackFrames?.Count > 0)
                    {
                        // Convert 0-based index to 1-based indexing used by EnvDTE
                        var dteFrameIndex = frameIndex + 1;
                        
                        if (dteFrameIndex <= currentThread.StackFrames.Count)
                        {
                            var stackFrame = currentThread.StackFrames.Item(dteFrameIndex);
                            
                            var frame = new CallStackFrame
                            {
                                Method = stackFrame.FunctionName ?? "Unknown",
                                File = ExtractFileName(stackFrame.FunctionName),
                                Line = 0,
                                Module = stackFrame.Module ?? "Unknown"
                            };
                            
                            // Note: StackFrame in EnvDTE doesn't have FileName and LineNumber properties
                            // We'll rely on the existing ExtractFileName method for file information
                            
                            _logger.LogDebug("Retrieved stack frame {FrameIndex}: {Method} at {File}:{Line}", 
                                frameIndex, frame.Method, frame.File, frame.Line);
                            return frame;
                        }
                        else
                        {
                            _logger.LogWarning("Frame index {FrameIndex} is out of range. Stack has {FrameCount} frames", 
                                frameIndex, currentThread.StackFrames.Count);
                            return null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to access stack frame {FrameIndex}", frameIndex);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get stack frame {FrameIndex}", frameIndex);
                throw;
            }
        });
    }

    /// <summary>
    /// Evaluates an expression in the current debugging context.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <returns>The result of the expression evaluation.</returns>
    public async Task<string> EvaluateExpressionAsync(string expression)
    {
        _logger.LogDebug("Evaluating expression: {Expression}", expression);

        var debugger = await GetActiveDebuggerAsync();
        if (debugger == null)
        {
            throw new InvalidOperationException("No active Visual Studio debugger found. Ensure Visual Studio is connected.");
        }

        // Check if debugging is paused (expression evaluation only available when paused)
        if (debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
        {
            throw new InvalidOperationException("Debugger is not paused. Expression evaluation is only available when execution is paused.");
        }

        // Note: StackFrame in EnvDTE doesn't have an Evaluate method
        // Expression evaluation is not currently supported
        _logger.LogWarning("Expression evaluation not implemented due to missing StackFrame.Evaluate method");
        throw new NotImplementedException("Expression evaluation not supported with current EnvDTE interface");
    }

    /// <summary>
    /// Modifies the value of a variable in the current debugging context.
    /// </summary>
    /// <param name="variableName">The name of the variable to modify.</param>
    /// <param name="newValue">The new value to assign to the variable.</param>
    /// <returns>True if the modification was successful, false otherwise.</returns>
    public async Task<bool> ModifyVariableAsync(string variableName, string newValue)
    {
        return await Task.Run(async () =>
        {
            _logger.LogDebug("Modifying variable {VariableName} to {NewValue}", variableName, newValue);

            try
            {
                var debugger = await GetActiveDebuggerAsync();
                if (debugger == null)
                {
                    throw new InvalidOperationException("No active Visual Studio debugger found. Ensure Visual Studio is connected.");
                }

                // Check if debugging is paused (variable modification only available when paused)
                if (debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                {
                    throw new InvalidOperationException("Debugger is not paused. Variable modification is only available when execution is paused.");
                }

                // Get the current stack frame for variable access
                var currentThread = debugger.CurrentThread;
                if (currentThread?.StackFrames?.Count > 0)
                {
                    var topFrame = currentThread.StackFrames.Item(1); // 1-based indexing
                    
                    // Try to find the variable in locals first
                    foreach (EnvDTE.Expression expression in topFrame.Locals)
                    {
                        if (expression.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase))
                        {
                            // Modify the variable value
                            expression.Value = newValue;
                            _logger.LogDebug("Successfully modified variable {VariableName} to {NewValue}", variableName, newValue);
                            return true;
                        }
                    }
                    
                    // If not found in locals, try arguments/parameters
                    foreach (EnvDTE.Expression expression in topFrame.Arguments)
                    {
                        if (expression.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase))
                        {
                            // Modify the variable value
                            expression.Value = newValue;
                            _logger.LogDebug("Successfully modified parameter {VariableName} to {NewValue}", variableName, newValue);
                            return true;
                        }
                    }
                    
                    _logger.LogWarning("Variable {VariableName} not found in current context", variableName);
                    return false;
                }
                else
                {
                    throw new InvalidOperationException("No stack frames available for variable modification.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to modify variable {VariableName} to {NewValue}", variableName, newValue);
                throw;
            }
        });
    }

    /// <summary>
    /// Gets memory usage information for the debugged process.
    /// </summary>
    /// <returns>Memory usage information.</returns>
    public async Task<MemoryInfo> GetMemoryInfoAsync()
    {
        return await Task.Run(async () =>
        {
            _logger.LogDebug("Getting memory information");

            try
            {
                var debugger = await GetActiveDebuggerAsync();
                if (debugger == null)
                {
                    throw new InvalidOperationException("No active Visual Studio debugger found. Ensure Visual Studio is connected.");
                }

                // Get the DTE from the debugger to access process information
                var dte = debugger.Parent;
                if (dte == null)
                {
                    throw new InvalidOperationException("Could not access Visual Studio DTE object.");
                }

                var memoryInfo = new MemoryInfo
                {
                    ProcessId = 0,
                    ProcessName = "Unknown"
                };

                try
                {
                    // Try to get process information from DTE
                    memoryInfo.ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
                    memoryInfo.ProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                    
                    // Get memory information from the current process as a fallback
                    var process = System.Diagnostics.Process.GetCurrentProcess();
                    memoryInfo.WorkingSet = process.WorkingSet64;
                    memoryInfo.PrivateMemory = process.PrivateMemorySize64;
                    memoryInfo.VirtualMemory = process.VirtualMemorySize64;
                    memoryInfo.HandleCount = process.HandleCount;
                    memoryInfo.ThreadCount = process.Threads.Count;
                }
                catch (Exception processEx)
                {
                    _logger.LogWarning(processEx, "Could not get detailed process memory information");
                }

                _logger.LogDebug("Retrieved memory information for process {ProcessId}", memoryInfo.ProcessId);
                return memoryInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get memory information");
                throw;
            }
        });
    }

    /// <summary>
    /// Gets detailed information about an object in the current debugging context.
    /// </summary>
    /// <param name="objectName">The name of the object to inspect.</param>
    /// <returns>Detailed information about the object.</returns>
    public async Task<ObjectInfo> InspectObjectAsync(string objectName)
    {
        return await Task.Run(async () =>
        {
            _logger.LogDebug("Inspecting object: {ObjectName}", objectName);

            try
            {
                var debugger = await GetActiveDebuggerAsync();
                if (debugger == null)
                {
                    throw new InvalidOperationException("No active Visual Studio debugger found. Ensure Visual Studio is connected.");
                }

                // Check if debugging is paused (object inspection only available when paused)
                if (debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
                {
                    throw new InvalidOperationException("Debugger is not paused. Object inspection is only available when execution is paused.");
                }

                // Get the current stack frame for variable access
                var currentThread = debugger.CurrentThread;
                if (currentThread?.StackFrames?.Count > 0)
                {
                    var topFrame = currentThread.StackFrames.Item(1); // 1-based indexing
                    
                    // Try to find the object in locals first
                    foreach (EnvDTE.Expression expression in topFrame.Locals)
                    {
                        if (expression.Name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                        {
                            return CreateObjectInfoFromExpression(expression, objectName);
                        }
                    }
                    
                    // If not found in locals, try arguments/parameters
                    foreach (EnvDTE.Expression expression in topFrame.Arguments)
                    {
                        if (expression.Name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                        {
                            return CreateObjectInfoFromExpression(expression, objectName);
                        }
                    }
                    
                    // Note: StackFrame in EnvDTE doesn't have an Evaluate method
                    // We can't evaluate the object name as an expression
                    _logger.LogDebug("Expression evaluation not available due to missing StackFrame.Evaluate method");
                    
                    _logger.LogWarning("Object {ObjectName} not found in current context", objectName);
                    throw new InvalidOperationException($"Object '{objectName}' not found in current debugging context.");
                }
                else
                {
                    throw new InvalidOperationException("No stack frames available for object inspection.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to inspect object: {ObjectName}", objectName);
                throw;
            }
        });
    }

    /// <summary>
    /// Creates an ObjectInfo from an Expression.
    /// </summary>
    /// <param name="expression">The expression to convert.</param>
    /// <param name="name">The name of the object.</param>
    /// <returns>An ObjectInfo representing the expression.</returns>
    private ObjectInfo CreateObjectInfoFromExpression(EnvDTE.Expression expression, string name)
    {
        var objectInfo = new ObjectInfo
        {
            Name = name,
            Type = expression.Type ?? "unknown",
            Value = expression.Value?.ToString() ?? "<null>",
            Address = "Unknown",
            Size = 0
        };

        try
        {
            // Try to get properties for complex objects
            var properties = new List<ObjectProperty>();
            
            // Check if the expression has child expressions (properties)
            if (expression.DataMembers != null)
            {
                foreach (EnvDTE.Expression member in expression.DataMembers)
                {
                    try
                    {
                        var property = new ObjectProperty
                        {
                            Name = member.Name,
                            Type = member.Type ?? "unknown",
                            Value = member.Value?.ToString() ?? "<null>",
                            IsReadOnly = false // Default to false, as we can't easily determine this
                        };
                        properties.Add(property);
                    }
                    catch (Exception memberEx)
                    {
                        _logger.LogDebug(memberEx, "Could not extract property information for member: {MemberName}", member?.Name);
                    }
                }
            }
            
            objectInfo.Properties = properties.ToArray();
        }
        catch (Exception propEx)
        {
            _logger.LogDebug(propEx, "Could not extract properties for object: {ObjectName}", name);
        }

        return objectInfo;
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