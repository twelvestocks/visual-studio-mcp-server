namespace VisualStudioMcp.Core.Tests.TestUtilities;

/// <summary>
/// Standard test categories for organizing and filtering test execution.
/// </summary>
public static class TestCategories
{
    /// <summary>
    /// Unit tests that test individual components in isolation with mocked dependencies.
    /// These tests should run quickly and not require external resources.
    /// </summary>
    public const string Unit = "Unit";

    /// <summary>
    /// Integration tests that test interactions between components or with external systems.
    /// These tests may take longer and might require specific environment setup.
    /// </summary>
    public const string Integration = "Integration";

    /// <summary>
    /// Performance tests that validate timing, memory usage, and scalability requirements.
    /// These tests may take longer to execute and verify system performance characteristics.
    /// </summary>
    public const string Performance = "Performance";

    /// <summary>
    /// Security tests that validate access controls, input validation, and security boundaries.
    /// These tests focus on preventing security vulnerabilities and ensuring proper authorization.
    /// </summary>
    public const string Security = "Security";

    /// <summary>
    /// COM interop tests that specifically test COM object lifecycle management and interaction.
    /// These tests validate proper COM object disposal, exception handling, and marshalling.
    /// </summary>
    public const string ComInterop = "ComInterop";

    /// <summary>
    /// MCP protocol tests that validate Model Context Protocol compliance and message handling.
    /// These tests ensure proper protocol implementation and message serialization.
    /// </summary>
    public const string McpProtocol = "McpProtocol";

    /// <summary>
    /// Visual Studio automation tests that interact with VS through EnvDTE or other VS APIs.
    /// These tests may require Visual Studio to be running or available.
    /// </summary>
    public const string VisualStudioAutomation = "VisualStudioAutomation";

    /// <summary>
    /// XAML designer tests that test XAML parsing, modification, and designer interaction.
    /// These tests validate XAML manipulation and designer window detection.
    /// </summary>
    public const string XamlDesigner = "XamlDesigner";

    /// <summary>
    /// Imaging and screenshot tests that validate screen capture and image processing.
    /// These tests may require graphics subsystem and window management capabilities.
    /// </summary>
    public const string Imaging = "Imaging";

    /// <summary>
    /// Memory management tests that validate proper resource disposal and memory usage.
    /// These tests monitor memory leaks and garbage collection behavior.
    /// </summary>
    public const string MemoryManagement = "MemoryManagement";

    /// <summary>
    /// Concurrency tests that validate thread safety and concurrent operation handling.
    /// These tests check for race conditions and proper synchronization.
    /// </summary>
    public const string Concurrency = "Concurrency";

    /// <summary>
    /// Debugging automation tests that validate debugging session management and control.
    /// These tests interact with Visual Studio debugging capabilities.
    /// </summary>
    public const string DebuggingAutomation = "DebuggingAutomation";
}