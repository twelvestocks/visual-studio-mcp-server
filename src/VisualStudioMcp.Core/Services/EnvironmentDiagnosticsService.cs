using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using VisualStudioMcp.Shared.Models;

namespace VisualStudioMcp.Core.Services;

/// <summary>
/// Service for capturing comprehensive environment diagnostic information for error reporting and debugging.
/// </summary>
public class EnvironmentDiagnosticsService : IEnvironmentDiagnosticsService
{
    private readonly ILogger<EnvironmentDiagnosticsService> _logger;
    private readonly object _captureLock = new();
    private EnvironmentDiagnostics? _cachedDiagnostics;
    private DateTime _lastCaptureTime = DateTime.MinValue;
    private readonly TimeSpan _cacheValidDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of the EnvironmentDiagnosticsService.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public EnvironmentDiagnosticsService(ILogger<EnvironmentDiagnosticsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Captures current environment diagnostics with optional caching.
    /// </summary>
    /// <param name="forceRefresh">Whether to force a fresh capture, ignoring cache.</param>
    /// <returns>Comprehensive environment diagnostic information.</returns>
    public EnvironmentDiagnostics CaptureEnvironmentDiagnostics(bool forceRefresh = false)
    {
        lock (_captureLock)
        {
            try
            {
                // Use cached diagnostics if available and still valid
                if (!forceRefresh && _cachedDiagnostics != null && 
                    DateTime.UtcNow - _lastCaptureTime < _cacheValidDuration)
                {
                    _logger.LogDebug("Returning cached environment diagnostics from {CacheTime}",
                        _lastCaptureTime);
                    return _cachedDiagnostics;
                }

                _logger.LogDebug("Capturing fresh environment diagnostics");
                var startTime = DateTime.UtcNow;

                var diagnostics = new EnvironmentDiagnostics
                {
                    OperatingSystem = CaptureOperatingSystemInfo(),
                    DotNetRuntime = CaptureDotNetRuntimeInfo(),
                    SystemPerformance = CaptureSystemPerformanceInfo(),
                    EnvironmentVariables = CaptureRelevantEnvironmentVariables(),
                    Capabilities = CaptureSystemCapabilities(),
                    CaptureTime = startTime
                };

                _cachedDiagnostics = diagnostics;
                _lastCaptureTime = startTime;

                var captureTime = DateTime.UtcNow - startTime;
                _logger.LogDebug("Environment diagnostics captured in {CaptureTime}ms", 
                    captureTime.TotalMilliseconds);

                return diagnostics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing environment diagnostics");
                
                // Return minimal diagnostics on failure
                return new EnvironmentDiagnostics
                {
                    CaptureTime = DateTime.UtcNow,
                    OperatingSystem = new OperatingSystemInfo
                    {
                        Name = "Unknown (capture failed)",
                        Version = ex.Message
                    }
                };
            }
        }
    }

    /// <summary>
    /// Captures operating system diagnostic information.
    /// </summary>
    private OperatingSystemInfo CaptureOperatingSystemInfo()
    {
        try
        {
            var osInfo = new OperatingSystemInfo
            {
                Name = Environment.OSVersion.ToString(),
                Version = Environment.OSVersion.Version.ToString(),
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                Culture = CultureInfo.CurrentCulture.Name,
                TimeZone = TimeZoneInfo.Local.DisplayName,
                Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
                IsServer = IsServerOperatingSystem()
            };

            // Try to get more detailed OS information
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    osInfo.Name = GetWindowsProductName() ?? osInfo.Name;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve detailed OS information");
            }

            return osInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error capturing OS information");
            return new OperatingSystemInfo
            {
                Name = "Unknown",
                Version = "Unknown",
                Architecture = RuntimeInformation.OSArchitecture.ToString()
            };
        }
    }

    /// <summary>
    /// Captures .NET runtime diagnostic information.
    /// </summary>
    private DotNetRuntimeInfo CaptureDotNetRuntimeInfo()
    {
        try
        {
            return new DotNetRuntimeInfo
            {
                FrameworkVersion = RuntimeInformation.FrameworkDescription,
                RuntimeVersion = Environment.Version.ToString(),
                TargetFramework = AppContext.TargetFrameworkName ?? "Unknown",
                BuildConfiguration = IsDebugBuild() ? "Debug" : "Release",
                GarbageCollection = new GarbageCollectionInfo
                {
                    IsServerGC = GCSettings.IsServerGC,
                    IsConcurrentGC = GCSettings.LatencyMode != GCLatencyMode.Batch,
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2),
                    TotalMemoryBytes = GC.GetTotalMemory(false)
                },
                Threading = new ThreadingInfo
                {
                    CurrentThreadId = Environment.CurrentManagedThreadId,
                    IsThreadPoolThread = Thread.CurrentThread.IsThreadPoolThread
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error capturing .NET runtime information");
            return new DotNetRuntimeInfo
            {
                FrameworkVersion = "Unknown",
                RuntimeVersion = Environment.Version.ToString()
            };
        }
    }

    /// <summary>
    /// Captures system performance diagnostic information.
    /// </summary>
    private SystemPerformanceInfo CaptureSystemPerformanceInfo()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            
            var processMemory = new ProcessMemoryInfo
            {
                PrivateMemoryMb = currentProcess.PrivateMemorySize64 / (1024 * 1024),
                WorkingSetMb = currentProcess.WorkingSet64 / (1024 * 1024),
                VirtualMemoryMb = currentProcess.VirtualMemorySize64 / (1024 * 1024),
                PagedMemoryMb = currentProcess.PagedMemorySize64 / (1024 * 1024),
                PeakWorkingSetMb = currentProcess.PeakWorkingSet64 / (1024 * 1024)
            };

            var systemMemory = CaptureSystemMemoryInfo();
            var cpuInfo = CaptureCpuInfo();
            var diskInfo = CaptureDiskInfo();

            return new SystemPerformanceInfo
            {
                ProcessMemory = processMemory,
                SystemMemory = systemMemory,
                Cpu = cpuInfo,
                Disks = diskInfo
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error capturing system performance information");
            return new SystemPerformanceInfo();
        }
    }

    /// <summary>
    /// Captures system memory information.
    /// </summary>
    private SystemMemoryInfo CaptureSystemMemoryInfo()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return CaptureWindowsMemoryInfo();
            }
            
            // Fallback for other platforms
            return new SystemMemoryInfo();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error capturing system memory information");
            return new SystemMemoryInfo();
        }
    }

    /// <summary>
    /// Captures Windows-specific memory information using WMI.
    /// </summary>
    private SystemMemoryInfo CaptureWindowsMemoryInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            using var results = searcher.Get();
            
            foreach (ManagementObject result in results)
            {
                var totalMemory = Convert.ToInt64(result["TotalVisibleMemorySize"]) * 1024 / (1024 * 1024);
                var availableMemory = Convert.ToInt64(result["AvailablePhysicalMemory"]) * 1024 / (1024 * 1024);
                var usedMemory = totalMemory - availableMemory;
                
                return new SystemMemoryInfo
                {
                    TotalPhysicalMb = totalMemory,
                    AvailablePhysicalMb = availableMemory,
                    MemoryUsagePercent = totalMemory > 0 ? (double)usedMemory / totalMemory * 100 : 0,
                    PageFileUsageMb = Convert.ToInt64(result["SizeStoredInPagingFiles"]) * 1024 / (1024 * 1024)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error capturing Windows memory information via WMI");
        }
        
        return new SystemMemoryInfo();
    }

    /// <summary>
    /// Captures CPU information.
    /// </summary>
    private CpuInfo CaptureCpuInfo()
    {
        try
        {
            var cpuInfo = new CpuInfo
            {
                CoreCount = Environment.ProcessorCount,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString()
            };

            // Try to get CPU name on Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                    using var results = searcher.Get();
                    
                    foreach (ManagementObject result in results)
                    {
                        cpuInfo.ProcessorName = result["Name"]?.ToString() ?? "Unknown";
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not retrieve CPU name via WMI");
                }
            }

            return cpuInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error capturing CPU information");
            return new CpuInfo
            {
                CoreCount = Environment.ProcessorCount,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString()
            };
        }
    }

    /// <summary>
    /// Captures disk usage information for all drives.
    /// </summary>
    private DiskInfo[] CaptureDiskInfo()
    {
        try
        {
            var diskInfoList = new List<DiskInfo>();
            
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady)
                    {
                        var totalSpaceMb = drive.TotalSize / (1024 * 1024);
                        var availableSpaceMb = drive.AvailableFreeSpace / (1024 * 1024);
                        var usedSpaceMb = totalSpaceMb - availableSpaceMb;
                        
                        diskInfoList.Add(new DiskInfo
                        {
                            Drive = drive.Name,
                            FileSystem = drive.DriveFormat,
                            TotalSpaceMb = totalSpaceMb,
                            AvailableSpaceMb = availableSpaceMb,
                            UsagePercent = totalSpaceMb > 0 ? (double)usedSpaceMb / totalSpaceMb * 100 : 0
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not capture information for drive {DriveName}", drive.Name);
                }
            }
            
            return diskInfoList.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error capturing disk information");
            return Array.Empty<DiskInfo>();
        }
    }

    /// <summary>
    /// Captures relevant environment variables (filtered for security).
    /// </summary>
    private Dictionary<string, string> CaptureRelevantEnvironmentVariables()
    {
        try
        {
            var relevantVars = new[]
            {
                "OS", "PROCESSOR_ARCHITECTURE", "PROCESSOR_IDENTIFIER",
                "COMPUTERNAME", "USERDOMAIN", "USERNAME",
                "DOTNET_VERSION", "DOTNET_ROOT", "MSBuildSDKsPath",
                "VisualStudioVersion", "VSINSTALLDIR", "DevEnvDir",
                "WindowsSDKVersion", "FrameworkVersion",
                "PATH", "TEMP", "TMP"
            };

            var envVars = new Dictionary<string, string>();
            
            foreach (var varName in relevantVars)
            {
                try
                {
                    var value = Environment.GetEnvironmentVariable(varName);
                    if (!string.IsNullOrEmpty(value))
                    {
                        // Sanitize sensitive paths
                        if (varName == "PATH")
                        {
                            value = SanitizePath(value);
                        }
                        
                        envVars[varName] = value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not capture environment variable {VarName}", varName);
                }
            }
            
            return envVars;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error capturing environment variables");
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Captures system capabilities and features.
    /// </summary>
    private SystemCapabilities CaptureSystemCapabilities()
    {
        try
        {
            return new SystemCapabilities
            {
                ComInteropAvailable = RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
                VisualStudioInstalled = IsVisualStudioInstalled(),
                VisualStudioVersions = GetInstalledVisualStudioVersions(),
                WindowsFormsAvailable = IsWindowsFormsAvailable(),
                WpfAvailable = IsWpfAvailable(),
                Security = new SecurityInfo
                {
                    IsAdministrator = IsRunningAsAdministrator(),
                    UserIdentity = GetCurrentUserIdentity(),
                    UacEnabled = IsUacEnabled(),
                    IsRestricted = IsRunningInRestrictedEnvironment()
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error capturing system capabilities");
            return new SystemCapabilities();
        }
    }

    /// <summary>
    /// Determines if this is a server operating system.
    /// </summary>
    private bool IsServerOperatingSystem()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProductType FROM Win32_OperatingSystem");
                using var results = searcher.Get();
                
                foreach (ManagementObject result in results)
                {
                    var productType = Convert.ToInt32(result["ProductType"]);
                    return productType != 1; // 1 = Workstation, 2 = Domain Controller, 3 = Server
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not determine server OS status");
        }
        
        return false;
    }

    /// <summary>
    /// Gets the Windows product name.
    /// </summary>
    private string? GetWindowsProductName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
            using var results = searcher.Get();
            
            foreach (ManagementObject result in results)
            {
                return result["Caption"]?.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not retrieve Windows product name");
        }
        
        return null;
    }

    /// <summary>
    /// Determines if this is a debug build.
    /// </summary>
    private bool IsDebugBuild()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var debugAttribute = assembly.GetCustomAttributes(typeof(DebuggableAttribute), false)
                .Cast<DebuggableAttribute>()
                .FirstOrDefault();
            
            return debugAttribute?.IsJITOptimizerDisabled == true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if Visual Studio is installed.
    /// </summary>
    private bool IsVisualStudioInstalled()
    {
        try
        {
            var vsInstallDir = Environment.GetEnvironmentVariable("VSINSTALLDIR");
            if (!string.IsNullOrEmpty(vsInstallDir) && Directory.Exists(vsInstallDir))
            {
                return true;
            }

            // Check common installation paths
            var commonPaths = new[]
            {
                @"C:\Program Files\Microsoft Visual Studio",
                @"C:\Program Files (x86)\Microsoft Visual Studio"
            };

            return commonPaths.Any(path => Directory.Exists(path) && 
                Directory.GetDirectories(path).Any(dir => 
                    Path.GetFileName(dir).StartsWith("2022") || 
                    Path.GetFileName(dir).StartsWith("2019")));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking Visual Studio installation");
            return false;
        }
    }

    /// <summary>
    /// Gets installed Visual Studio versions.
    /// </summary>
    private string[] GetInstalledVisualStudioVersions()
    {
        try
        {
            var versions = new List<string>();
            
            // Check environment variable
            var vsVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");
            if (!string.IsNullOrEmpty(vsVersion))
            {
                versions.Add(vsVersion);
            }

            return versions.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting Visual Studio versions");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Checks if Windows Forms is available.
    /// </summary>
    private bool IsWindowsFormsAvailable()
    {
        try
        {
            var winFormsAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name?.StartsWith("System.Windows.Forms") == true);
            return winFormsAssembly != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if WPF is available.
    /// </summary>
    private bool IsWpfAvailable()
    {
        try
        {
            var wpfAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name?.StartsWith("PresentationFramework") == true);
            return wpfAssembly != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if running with administrator privileges.
    /// </summary>
    private bool IsRunningAsAdministrator()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not determine administrator status");
        }
        
        return false;
    }

    /// <summary>
    /// Gets the current user identity.
    /// </summary>
    private string GetCurrentUserIdentity()
    {
        try
        {
            return Environment.UserDomainName + "\\" + Environment.UserName;
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// Checks if UAC is enabled (Windows only).
    /// </summary>
    private bool IsUacEnabled()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // This is a simplified check - actual UAC detection is more complex
                return !IsRunningAsAdministrator();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not determine UAC status");
        }
        
        return false;
    }

    /// <summary>
    /// Checks if running in a restricted environment.
    /// </summary>
    private bool IsRunningInRestrictedEnvironment()
    {
        try
        {
            // Check if we can write to the temp directory
            var tempPath = Path.GetTempPath();
            var testFile = Path.Combine(tempPath, $"test_{Guid.NewGuid()}.tmp");
            
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            
            return false; // If we can write/delete, not restricted
        }
        catch
        {
            return true; // If we can't write to temp, likely restricted
        }
    }

    /// <summary>
    /// Sanitizes path information for security.
    /// </summary>
    private string SanitizePath(string path)
    {
        try
        {
            // Remove sensitive user information from paths
            var sanitized = path;
            var userName = Environment.UserName;
            
            if (!string.IsNullOrEmpty(userName))
            {
                sanitized = sanitized.Replace(userName, "[USERNAME]", StringComparison.OrdinalIgnoreCase);
            }
            
            return sanitized;
        }
        catch
        {
            return "[SANITIZATION_FAILED]";
        }
    }
}

/// <summary>
/// Interface for environment diagnostics service.
/// </summary>
public interface IEnvironmentDiagnosticsService
{
    /// <summary>
    /// Captures current environment diagnostics.
    /// </summary>
    /// <param name="forceRefresh">Whether to force a fresh capture, ignoring cache.</param>
    /// <returns>Comprehensive environment diagnostic information.</returns>
    EnvironmentDiagnostics CaptureEnvironmentDiagnostics(bool forceRefresh = false);
}