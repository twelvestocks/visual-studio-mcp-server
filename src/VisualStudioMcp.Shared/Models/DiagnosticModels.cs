namespace VisualStudioMcp.Shared.Models;

/// <summary>
/// Comprehensive environment diagnostic information.
/// </summary>
public class EnvironmentDiagnostics
{
    /// <summary>
    /// Operating system information.
    /// </summary>
    public OperatingSystemInfo OperatingSystem { get; set; } = new();

    /// <summary>
    /// .NET runtime information.
    /// </summary>
    public DotNetRuntimeInfo DotNetRuntime { get; set; } = new();

    /// <summary>
    /// Memory and performance information.
    /// </summary>
    public SystemPerformanceInfo SystemPerformance { get; set; } = new();

    /// <summary>
    /// Environment variables relevant to the operation.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// System capabilities and features.
    /// </summary>
    public SystemCapabilities Capabilities { get; set; } = new();

    /// <summary>
    /// Timestamp when diagnostics were captured.
    /// </summary>
    public DateTime CaptureTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Operating system diagnostic information.
/// </summary>
public class OperatingSystemInfo
{
    /// <summary>
    /// Operating system name and version.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// OS version string.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Platform architecture (x86, x64, ARM64).
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// System language and locale.
    /// </summary>
    public string Culture { get; set; } = string.Empty;

    /// <summary>
    /// Time zone information.
    /// </summary>
    public string TimeZone { get; set; } = string.Empty;

    /// <summary>
    /// System uptime.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Whether the OS is a server edition.
    /// </summary>
    public bool IsServer { get; set; }
}

/// <summary>
/// .NET runtime diagnostic information.
/// </summary>
public class DotNetRuntimeInfo
{
    /// <summary>
    /// .NET framework or Core version.
    /// </summary>
    public string FrameworkVersion { get; set; } = string.Empty;

    /// <summary>
    /// Runtime version.
    /// </summary>
    public string RuntimeVersion { get; set; } = string.Empty;

    /// <summary>
    /// Target framework of the running application.
    /// </summary>
    public string TargetFramework { get; set; } = string.Empty;

    /// <summary>
    /// Whether running in debug or release mode.
    /// </summary>
    public string BuildConfiguration { get; set; } = string.Empty;

    /// <summary>
    /// Garbage collection information.
    /// </summary>
    public GarbageCollectionInfo GarbageCollection { get; set; } = new();

    /// <summary>
    /// Threading information.
    /// </summary>
    public ThreadingInfo Threading { get; set; } = new();
}

/// <summary>
/// Garbage collection diagnostic information.
/// </summary>
public class GarbageCollectionInfo
{
    /// <summary>
    /// Whether server GC is enabled.
    /// </summary>
    public bool IsServerGC { get; set; }

    /// <summary>
    /// Whether concurrent GC is enabled.
    /// </summary>
    public bool IsConcurrentGC { get; set; }

    /// <summary>
    /// Generation 0 collection count.
    /// </summary>
    public int Gen0Collections { get; set; }

    /// <summary>
    /// Generation 1 collection count.
    /// </summary>
    public int Gen1Collections { get; set; }

    /// <summary>
    /// Generation 2 collection count.
    /// </summary>
    public int Gen2Collections { get; set; }

    /// <summary>
    /// Total managed memory in bytes.
    /// </summary>
    public long TotalMemoryBytes { get; set; }
}

/// <summary>
/// Threading diagnostic information.
/// </summary>
public class ThreadingInfo
{
    /// <summary>
    /// Number of worker threads in the thread pool.
    /// </summary>
    public int WorkerThreads { get; set; }

    /// <summary>
    /// Number of completion port threads in the thread pool.
    /// </summary>
    public int CompletionPortThreads { get; set; }

    /// <summary>
    /// Maximum worker threads.
    /// </summary>
    public int MaxWorkerThreads { get; set; }

    /// <summary>
    /// Maximum completion port threads.
    /// </summary>
    public int MaxCompletionPortThreads { get; set; }

    /// <summary>
    /// Current thread ID.
    /// </summary>
    public int CurrentThreadId { get; set; }

    /// <summary>
    /// Whether the current thread is a thread pool thread.
    /// </summary>
    public bool IsThreadPoolThread { get; set; }
}

/// <summary>
/// System performance diagnostic information.
/// </summary>
public class SystemPerformanceInfo
{
    /// <summary>
    /// Current process memory usage.
    /// </summary>
    public ProcessMemoryInfo ProcessMemory { get; set; } = new();

    /// <summary>
    /// System-wide memory information.
    /// </summary>
    public SystemMemoryInfo SystemMemory { get; set; } = new();

    /// <summary>
    /// CPU usage information.
    /// </summary>
    public CpuInfo Cpu { get; set; } = new();

    /// <summary>
    /// Disk usage information.
    /// </summary>
    public DiskInfo[] Disks { get; set; } = Array.Empty<DiskInfo>();
}

/// <summary>
/// Process-specific memory information.
/// </summary>
public class ProcessMemoryInfo
{
    /// <summary>
    /// Private memory usage in MB.
    /// </summary>
    public long PrivateMemoryMb { get; set; }

    /// <summary>
    /// Working set size in MB.
    /// </summary>
    public long WorkingSetMb { get; set; }

    /// <summary>
    /// Virtual memory usage in MB.
    /// </summary>
    public long VirtualMemoryMb { get; set; }

    /// <summary>
    /// Paged memory usage in MB.
    /// </summary>
    public long PagedMemoryMb { get; set; }

    /// <summary>
    /// Peak working set size in MB.
    /// </summary>
    public long PeakWorkingSetMb { get; set; }
}

/// <summary>
/// System-wide memory information.
/// </summary>
public class SystemMemoryInfo
{
    /// <summary>
    /// Total physical memory in MB.
    /// </summary>
    public long TotalPhysicalMb { get; set; }

    /// <summary>
    /// Available physical memory in MB.
    /// </summary>
    public long AvailablePhysicalMb { get; set; }

    /// <summary>
    /// Memory usage percentage.
    /// </summary>
    public double MemoryUsagePercent { get; set; }

    /// <summary>
    /// Page file usage in MB.
    /// </summary>
    public long PageFileUsageMb { get; set; }
}

/// <summary>
/// CPU diagnostic information.
/// </summary>
public class CpuInfo
{
    /// <summary>
    /// Number of processor cores.
    /// </summary>
    public int CoreCount { get; set; }

    /// <summary>
    /// Current CPU usage percentage (if available).
    /// </summary>
    public double? CpuUsagePercent { get; set; }

    /// <summary>
    /// Processor architecture.
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// Processor name/description.
    /// </summary>
    public string ProcessorName { get; set; } = string.Empty;
}

/// <summary>
/// Disk usage information.
/// </summary>
public class DiskInfo
{
    /// <summary>
    /// Drive letter or mount point.
    /// </summary>
    public string Drive { get; set; } = string.Empty;

    /// <summary>
    /// File system type.
    /// </summary>
    public string FileSystem { get; set; } = string.Empty;

    /// <summary>
    /// Total disk space in MB.
    /// </summary>
    public long TotalSpaceMb { get; set; }

    /// <summary>
    /// Available disk space in MB.
    /// </summary>
    public long AvailableSpaceMb { get; set; }

    /// <summary>
    /// Disk usage percentage.
    /// </summary>
    public double UsagePercent { get; set; }
}

/// <summary>
/// System capabilities information.
/// </summary>
public class SystemCapabilities
{
    /// <summary>
    /// Whether COM interop is available.
    /// </summary>
    public bool ComInteropAvailable { get; set; }

    /// <summary>
    /// Whether Visual Studio is installed.
    /// </summary>
    public bool VisualStudioInstalled { get; set; }

    /// <summary>
    /// Available Visual Studio versions.
    /// </summary>
    public string[] VisualStudioVersions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether Windows Forms is available.
    /// </summary>
    public bool WindowsFormsAvailable { get; set; }

    /// <summary>
    /// Whether WPF is available.
    /// </summary>
    public bool WpfAvailable { get; set; }

    /// <summary>
    /// Security and permissions information.
    /// </summary>
    public SecurityInfo Security { get; set; } = new();
}

/// <summary>
/// Security and permissions information.
/// </summary>
public class SecurityInfo
{
    /// <summary>
    /// Whether running with administrator privileges.
    /// </summary>
    public bool IsAdministrator { get; set; }

    /// <summary>
    /// Current user identity.
    /// </summary>
    public string UserIdentity { get; set; } = string.Empty;

    /// <summary>
    /// Whether User Account Control (UAC) is enabled.
    /// </summary>
    public bool UacEnabled { get; set; }

    /// <summary>
    /// Whether running in a restricted environment.
    /// </summary>
    public bool IsRestricted { get; set; }
}

/// <summary>
/// Visual Studio specific diagnostic information.
/// </summary>
public class VisualStudioDiagnostics
{
    /// <summary>
    /// Connected Visual Studio instances.
    /// </summary>
    public VisualStudioInstanceDiagnostics[] Instances { get; set; } = Array.Empty<VisualStudioInstanceDiagnostics>();

    /// <summary>
    /// Currently active or primary instance.
    /// </summary>
    public VisualStudioInstanceDiagnostics? ActiveInstance { get; set; }

    /// <summary>
    /// COM connection health information.
    /// </summary>
    public ComConnectionHealth ComHealth { get; set; } = new();

    /// <summary>
    /// Recent operation history.
    /// </summary>
    public OperationHistoryEntry[] RecentOperations { get; set; } = Array.Empty<OperationHistoryEntry>();

    /// <summary>
    /// Timestamp when diagnostics were captured.
    /// </summary>
    public DateTime CaptureTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Diagnostic information for a specific Visual Studio instance.
/// </summary>
public class VisualStudioInstanceDiagnostics
{
    /// <summary>
    /// Process ID of the Visual Studio instance.
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Visual Studio version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Main window title.
    /// </summary>
    public string WindowTitle { get; set; } = string.Empty;

    /// <summary>
    /// Currently loaded solution.
    /// </summary>
    public SolutionDiagnostics? Solution { get; set; }

    /// <summary>
    /// Process health information.
    /// </summary>
    public ProcessHealthInfo ProcessHealth { get; set; } = new();

    /// <summary>
    /// Whether the instance is responsive.
    /// </summary>
    public bool IsResponsive { get; set; }

    /// <summary>
    /// Connection status to this instance.
    /// </summary>
    public ConnectionStatus ConnectionStatus { get; set; }

    /// <summary>
    /// Last successful communication time.
    /// </summary>
    public DateTime? LastCommunication { get; set; }

    /// <summary>
    /// Active debugging sessions.
    /// </summary>
    public DebugSessionInfo[] DebugSessions { get; set; } = Array.Empty<DebugSessionInfo>();
}

/// <summary>
/// Solution diagnostic information.
/// </summary>
public class SolutionDiagnostics
{
    /// <summary>
    /// Solution file path.
    /// </summary>
    public string SolutionPath { get; set; } = string.Empty;

    /// <summary>
    /// Solution name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the solution is fully loaded.
    /// </summary>
    public bool IsLoaded { get; set; }

    /// <summary>
    /// Number of projects in the solution.
    /// </summary>
    public int ProjectCount { get; set; }

    /// <summary>
    /// Current build configuration.
    /// </summary>
    public string BuildConfiguration { get; set; } = string.Empty;

    /// <summary>
    /// Last build status.
    /// </summary>
    public BuildStatus LastBuildStatus { get; set; }

    /// <summary>
    /// Error and warning counts.
    /// </summary>
    public ErrorCounts ErrorCounts { get; set; } = new();
}

/// <summary>
/// Process health information.
/// </summary>
public class ProcessHealthInfo
{
    /// <summary>
    /// Process start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Process uptime.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Memory usage information.
    /// </summary>
    public ProcessMemoryInfo Memory { get; set; } = new();

    /// <summary>
    /// CPU usage percentage.
    /// </summary>
    public double? CpuUsagePercent { get; set; }

    /// <summary>
    /// Number of threads.
    /// </summary>
    public int ThreadCount { get; set; }

    /// <summary>
    /// Whether the process is responding.
    /// </summary>
    public bool IsResponding { get; set; }
}

/// <summary>
/// COM connection health information.
/// </summary>
public class ComConnectionHealth
{
    /// <summary>
    /// Number of active COM connections.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Recent COM operation success rate.
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Average response time for COM operations.
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }

    /// <summary>
    /// Recent COM errors.
    /// </summary>
    public ComErrorInfo[] RecentErrors { get; set; } = Array.Empty<ComErrorInfo>();

    /// <summary>
    /// Connection retry statistics.
    /// </summary>
    public RetryStatistics RetryStats { get; set; } = new();
}

/// <summary>
/// COM error information.
/// </summary>
public class ComErrorInfo
{
    /// <summary>
    /// COM HRESULT value.
    /// </summary>
    public int HResult { get; set; }

    /// <summary>
    /// Error description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// When the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Operation that caused the error.
    /// </summary>
    public string Operation { get; set; } = string.Empty;
}

/// <summary>
/// Operation history entry.
/// </summary>
public class OperationHistoryEntry
{
    /// <summary>
    /// Operation name or identifier.
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID for tracking.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// When the operation started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Operation duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error information if the operation failed.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Performance metrics for operations.
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// Operation start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Operation end time.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Total operation duration.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Memory usage before operation.
    /// </summary>
    public long MemoryBeforeMb { get; set; }

    /// <summary>
    /// Memory usage after operation.
    /// </summary>
    public long MemoryAfterMb { get; set; }

    /// <summary>
    /// Memory change during operation.
    /// </summary>
    public long MemoryDeltaMb => MemoryAfterMb - MemoryBeforeMb;

    /// <summary>
    /// CPU time consumed by the operation.
    /// </summary>
    public TimeSpan? CpuTime { get; set; }

    /// <summary>
    /// Number of COM objects created/used.
    /// </summary>
    public int ComObjectCount { get; set; }

    /// <summary>
    /// Number of garbage collections triggered.
    /// </summary>
    public GcMetrics GarbageCollection { get; set; } = new();

    /// <summary>
    /// Additional performance counters.
    /// </summary>
    public Dictionary<string, object> CustomMetrics { get; set; } = new();
}

/// <summary>
/// Garbage collection metrics.
/// </summary>
public class GcMetrics
{
    /// <summary>
    /// Gen 0 collections during operation.
    /// </summary>
    public int Gen0Collections { get; set; }

    /// <summary>
    /// Gen 1 collections during operation.
    /// </summary>
    public int Gen1Collections { get; set; }

    /// <summary>
    /// Gen 2 collections during operation.
    /// </summary>
    public int Gen2Collections { get; set; }

    /// <summary>
    /// Memory allocated during operation.
    /// </summary>
    public long AllocatedBytes { get; set; }
}

/// <summary>
/// Debug session information.
/// </summary>
public class DebugSessionInfo
{
    /// <summary>
    /// Debug session ID.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Current debug state.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Target process being debugged.
    /// </summary>
    public string? TargetProcess { get; set; }

    /// <summary>
    /// Number of active breakpoints.
    /// </summary>
    public int BreakpointCount { get; set; }

    /// <summary>
    /// Whether the debuggee is currently running.
    /// </summary>
    public bool IsRunning { get; set; }
}

/// <summary>
/// Retry operation statistics.
/// </summary>
public class RetryStatistics
{
    /// <summary>
    /// Total number of operations attempted.
    /// </summary>
    public int TotalAttempts { get; set; }

    /// <summary>
    /// Number of successful operations.
    /// </summary>
    public int SuccessfulAttempts { get; set; }

    /// <summary>
    /// Number of operations that required retry.
    /// </summary>
    public int RetriedOperations { get; set; }

    /// <summary>
    /// Average number of retries per operation.
    /// </summary>
    public double AverageRetries { get; set; }

    /// <summary>
    /// Success rate percentage.
    /// </summary>
    public double SuccessRate => TotalAttempts > 0 ? (double)SuccessfulAttempts / TotalAttempts * 100 : 0;
}

/// <summary>
/// Error counts for build operations.
/// </summary>
public class ErrorCounts
{
    /// <summary>
    /// Number of build errors.
    /// </summary>
    public int Errors { get; set; }

    /// <summary>
    /// Number of build warnings.
    /// </summary>
    public int Warnings { get; set; }

    /// <summary>
    /// Number of informational messages.
    /// </summary>
    public int Messages { get; set; }
}

/// <summary>
/// Connection status enumeration.
/// </summary>
public enum ConnectionStatus
{
    Unknown,
    Disconnected,
    Connecting,
    Connected,
    Failed,
    Timeout
}

/// <summary>
/// Build status enumeration.
/// </summary>
public enum BuildStatus
{
    Unknown,
    NotBuilt,
    Building,
    Succeeded,
    Failed,
    Cancelled
}