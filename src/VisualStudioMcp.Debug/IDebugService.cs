namespace VisualStudioMcp.Debug;

/// <summary>
/// Service interface for Visual Studio debugging automation.
/// </summary>
public interface IDebugService : IDisposable
{
    /// <summary>
    /// Starts debugging the specified project or the startup project.
    /// </summary>
    /// <param name="projectName">Optional project name to debug. If null, uses startup project.</param>
    /// <returns>The current debug state after starting.</returns>
    Task<DebugState> StartDebuggingAsync(string? projectName = null);

    /// <summary>
    /// Stops the current debugging session.
    /// </summary>
    Task StopDebuggingAsync();

    /// <summary>
    /// Gets the current state of the debugger.
    /// </summary>
    /// <returns>The current debug state.</returns>
    Task<DebugState> GetCurrentStateAsync();

    /// <summary>
    /// Gets all breakpoints in the current debugging session.
    /// </summary>
    /// <returns>Array of breakpoints.</returns>
    Task<Breakpoint[]> GetBreakpointsAsync();

    /// <summary>
    /// Adds a breakpoint at the specified location.
    /// </summary>
    /// <param name="file">The file path.</param>
    /// <param name="line">The line number.</param>
    /// <param name="condition">Optional breakpoint condition.</param>
    /// <returns>The created breakpoint.</returns>
    Task<Breakpoint> AddBreakpointAsync(string file, int line, string? condition = null);

    /// <summary>
    /// Gets the local variables in the current debugging context.
    /// </summary>
    /// <returns>Array of local variables.</returns>
    Task<Variable[]> GetLocalVariablesAsync();

    /// <summary>
    /// Gets the current call stack.
    /// </summary>
    /// <returns>Array of call stack frames.</returns>
    Task<CallStackFrame[]> GetCallStackAsync();
}

/// <summary>
/// Represents the current state of the debugger.
/// </summary>
public class DebugState
{
    /// <summary>
    /// Whether debugging is currently active.
    /// </summary>
    public bool IsDebugging { get; set; }

    /// <summary>
    /// Whether execution is currently paused at a breakpoint.
    /// </summary>
    public bool IsPaused { get; set; }

    /// <summary>
    /// The current execution mode.
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// The current file being debugged, if paused.
    /// </summary>
    public string? CurrentFile { get; set; }

    /// <summary>
    /// The current line being debugged, if paused.
    /// </summary>
    public int CurrentLine { get; set; }
}

/// <summary>
/// Represents a breakpoint in the debugger.
/// </summary>
public class Breakpoint
{
    /// <summary>
    /// The unique identifier for this breakpoint.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The file path where the breakpoint is set.
    /// </summary>
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// The line number where the breakpoint is set.
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// The condition for this breakpoint, if any.
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    /// Whether this breakpoint is currently enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// Represents a variable in the debugging context.
/// </summary>
public class Variable
{
    /// <summary>
    /// The name of the variable.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The current value of the variable.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// The type of the variable.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The scope of the variable (local, parameter, etc.).
    /// </summary>
    public string Scope { get; set; } = string.Empty;
}

/// <summary>
/// Represents a frame in the call stack.
/// </summary>
public class CallStackFrame
{
    /// <summary>
    /// The method name for this frame.
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// The file path for this frame.
    /// </summary>
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// The line number for this frame.
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// The module/assembly name for this frame.
    /// </summary>
    public string Module { get; set; } = string.Empty;
}