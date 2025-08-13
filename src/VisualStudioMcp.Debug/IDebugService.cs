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
    
    /// <summary>
    /// Steps into the next statement or method call.
    /// </summary>
    /// <returns>The debug state after stepping.</returns>
    Task<DebugState> StepIntoAsync();
    
    /// <summary>
    /// Steps over the next statement or method call.
    /// </summary>
    /// <returns>The debug state after stepping.</returns>
    Task<DebugState> StepOverAsync();
    
    /// <summary>
    /// Steps out of the current method.
    /// </summary>
    /// <returns>The debug state after stepping.</returns>
    Task<DebugState> StepOutAsync();
    
    /// <summary>
    /// Gets variables from a specific stack frame.
    /// </summary>
    /// <param name="frameIndex">The index of the stack frame (0 = top frame).</param>
    /// <returns>Array of variables from the specified frame.</returns>
    Task<Variable[]> GetVariablesFromFrameAsync(int frameIndex = 0);
    
    /// <summary>
    /// Gets a specific stack frame with detailed information.
    /// </summary>
    /// <param name="frameIndex">The index of the stack frame (0 = top frame).</param>
    /// <returns>The specified call stack frame.</returns>
    Task<CallStackFrame?> GetStackFrameAsync(int frameIndex = 0);
    
    /// <summary>
    /// Evaluates an expression in the current debugging context.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <returns>The result of the expression evaluation.</returns>
    Task<string> EvaluateExpressionAsync(string expression);
    
    /// <summary>
    /// Modifies the value of a variable in the current debugging context.
    /// </summary>
    /// <param name="variableName">The name of the variable to modify.</param>
    /// <param name="newValue">The new value to assign to the variable.</param>
    /// <returns>True if the modification was successful, false otherwise.</returns>
    Task<bool> ModifyVariableAsync(string variableName, string newValue);
    
    /// <summary>
    /// Gets memory usage information for the debugged process.
    /// </summary>
    /// <returns>Memory usage information.</returns>
    Task<MemoryInfo> GetMemoryInfoAsync();
    
    /// <summary>
    /// Gets detailed information about an object in the current debugging context.
    /// </summary>
    /// <param name="objectName">The name of the object to inspect.</param>
    /// <returns>Detailed information about the object.</returns>
    Task<ObjectInfo> InspectObjectAsync(string objectName);
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

/// <summary>
/// Represents memory usage information for a debugged process.
/// </summary>
public class MemoryInfo
{
    /// <summary>
    /// The working set size in bytes.
    /// </summary>
    public long WorkingSet { get; set; }
    
    /// <summary>
    /// The private memory size in bytes.
    /// </summary>
    public long PrivateMemory { get; set; }
    
    /// <summary>
    /// The virtual memory size in bytes.
    /// </summary>
    public long VirtualMemory { get; set; }
    
    /// <summary>
    /// The number of handles.
    /// </summary>
    public int HandleCount { get; set; }
    
    /// <summary>
    /// The number of threads.
    /// </summary>
    public int ThreadCount { get; set; }
    
    /// <summary>
    /// The process ID.
    /// </summary>
    public int ProcessId { get; set; }
    
    /// <summary>
    /// The process name.
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;
}

/// <summary>
/// Represents detailed information about an object in the debugging context.
/// </summary>
public class ObjectInfo
{
    /// <summary>
    /// The name of the object.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The type of the object.
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// The string representation of the object's value.
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// The size of the object in bytes, if available.
    /// </summary>
    public long Size { get; set; }
    
    /// <summary>
    /// The memory address of the object, if available.
    /// </summary>
    public string Address { get; set; } = string.Empty;
    
    /// <summary>
    /// Properties of the object, if it's a complex type.
    /// </summary>
    public ObjectProperty[] Properties { get; set; } = Array.Empty<ObjectProperty>();
}

/// <summary>
/// Represents a property of an object.
/// </summary>
public class ObjectProperty
{
    /// <summary>
    /// The name of the property.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The type of the property.
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// The value of the property.
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the property is read-only.
    /// </summary>
    public bool IsReadOnly { get; set; }
}