# Test Utilities Guide

## Overview

This guide provides comprehensive documentation for the shared test utilities in the Visual Studio MCP Server project. These utilities standardise testing patterns, improve developer productivity, and ensure consistent test quality across all components.

## Test Utilities Architecture

### Core Utility Classes
- **ExceptionTestHelper**: Standardised exception testing patterns
- **LoggerTestExtensions**: Fluent API for logger verification
- **TestCategories**: Comprehensive test categorisation system
- **PerformanceProfiler**: Performance measurement utilities
- **SecureTestDataGenerator**: Cryptographically secure test data generation

### Design Principles
- **Consistency**: Uniform testing patterns across all test projects
- **Productivity**: Reduce boilerplate code and development time
- **Reliability**: Robust, well-tested utility functions
- **Maintainability**: Clear, documented APIs with comprehensive examples
- **Integration**: Seamless integration with MSTest and Moq frameworks

## ExceptionTestHelper Usage

### Purpose and Benefits
The `ExceptionTestHelper` provides standardised patterns for exception testing, eliminating inconsistent manual exception handling and providing clear, descriptive failure messages.

**Benefits**:
- Consistent exception testing patterns across all test projects
- Clear, informative error messages when tests fail
- Support for both synchronous and asynchronous exception testing
- Custom exception property validation capabilities
- Reduced boilerplate code for exception scenarios

### Basic Exception Testing

#### Synchronous Exception Testing
```csharp
[TestMethod]
[TestCategory(TestCategories.Unit)]
[TestCategory(TestCategories.ComInterop)]
public void SafeComOperation_WithNullOperation_ThrowsArgumentNullException()
{
    // Arrange
    var logger = Mock.Of<ILogger<ComInteropHelper>>();
    
    // Act & Assert: Basic exception testing
    var exception = ExceptionTestHelper.AssertThrows<ArgumentNullException>(
        () => ComInteropHelper.SafeComOperation(null, logger, "TestOperation"));
    
    // Additional validation on the caught exception
    Assert.AreEqual("operation", exception.ParamName);
}
```

#### Asynchronous Exception Testing
```csharp
[TestMethod]
[TestCategory(TestCategories.Unit)]
[TestCategory(TestCategories.Integration)]
public async Task GetRunningInstancesAsync_WithCancelledToken_ThrowsOperationCancelledException()
{
    // Arrange
    var service = new VisualStudioService(Mock.Of<ILogger<VisualStudioService>>());
    var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.Cancel(); // Pre-cancel the token
    
    // Act & Assert: Async exception testing
    var exception = await ExceptionTestHelper.AssertThrowsAsync<OperationCanceledException>(
        () => service.GetRunningInstancesAsync(cancellationTokenSource.Token));
    
    // Verify cancellation token propagation
    Assert.AreEqual(cancellationTokenSource.Token, exception.CancellationToken);
}
```

### Exception Message Validation

#### Partial Message Content Validation
```csharp
[TestMethod]
[TestCategory(TestCategories.Unit)]
[TestCategory(TestCategories.Security)]
public void ValidateInput_WithMaliciousScript_ThrowsSecurityExceptionWithContext()
{
    // Arrange
    var service = new InputValidationService(Mock.Of<ILogger<InputValidationService>>());
    var maliciousInput = "<script>alert('xss')</script>";
    
    // Act & Assert: Exception with message validation
    var exception = ExceptionTestHelper.AssertThrows<SecurityException>(
        () => service.ValidateInput(maliciousInput),
        "security validation failed"); // Partial message match (case-insensitive)
    
    // Additional context validation
    Assert.IsTrue(exception.Message.Contains("input validation", StringComparison.OrdinalIgnoreCase));
    Assert.IsFalse(exception.Message.Contains(maliciousInput)); // No sensitive data in message
}
```

### Complex Exception Scenario Testing

#### Custom Exception Property Validation
```csharp
[TestMethod]
[TestCategory(TestCategories.Unit)]
[TestCategory(TestCategories.ComInterop)]
public void ComOperation_WithSpecificError_ThrowsComInteropExceptionWithDetails()
{
    // Arrange
    var logger = Mock.Of<ILogger<ComInteropHelper>>();
    var mockComObject = new Mock<IComObject>();
    mockComObject.Setup(x => x.Execute()).Throws(new COMException("Access denied", unchecked((int)0x80070005)));
    
    // Act & Assert: Complex exception validation
    var exception = ExceptionTestHelper.AssertThrowsWithValidation<ComInteropException>(
        () => ComInteropHelper.SafeComOperation(() => mockComObject.Object.Execute(), logger, "TestOperation"),
        ex => {
            // Validate specific exception properties
            Assert.AreEqual("TestOperation", ex.OperationName);
            Assert.AreEqual(0x80070005, ex.HResult);
            Assert.IsTrue(ex.IsRetryable == false); // Access denied is not retryable
            Assert.IsNotNull(ex.InnerException);
            Assert.IsInstanceOfType(ex.InnerException, typeof(COMException));
            
            // Validate exception data
            Assert.IsTrue(ex.Data.Contains("OperationName"));
            Assert.AreEqual("TestOperation", ex.Data["OperationName"]);
        });
}
```

#### Async Complex Exception Validation
```csharp
[TestMethod]
[TestCategory(TestCategories.Integration)]
[TestCategory(TestCategories.Performance)]
public async Task PerformanceOperation_WithTimeout_ThrowsTimeoutExceptionWithMetrics()
{
    // Arrange
    var service = new PerformanceService(Mock.Of<ILogger<PerformanceService>>());
    var timeout = TimeSpan.FromMilliseconds(100);
    
    // Act & Assert: Async complex exception validation
    var exception = await ExceptionTestHelper.AssertThrowsWithValidationAsync<TimeoutException>(
        () => service.ExecuteWithTimeoutAsync(TimeSpan.FromSeconds(10), timeout), // Operation longer than timeout
        ex => {
            // Validate timeout exception properties
            Assert.IsTrue(ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(ex.Data.Contains("TimeoutMs"));
            Assert.AreEqual(timeout.TotalMilliseconds, ex.Data["TimeoutMs"]);
            
            // Validate performance metrics in exception data
            Assert.IsTrue(ex.Data.Contains("ElapsedMs"));
            var elapsedMs = (double)ex.Data["ElapsedMs"];
            Assert.IsTrue(elapsedMs >= timeout.TotalMilliseconds);
        });
}
```

### Exception Testing Best Practices

#### Do's and Don'ts
```csharp
// ✅ DO: Use ExceptionTestHelper for consistent patterns
var exception = ExceptionTestHelper.AssertThrows<ArgumentException>(
    () => service.ProcessInvalidInput(null),
    "parameter cannot be null");

// ❌ DON'T: Use manual try-catch for exception testing
try
{
    service.ProcessInvalidInput(null);
    Assert.Fail("Expected ArgumentException was not thrown");
}
catch (ArgumentException ex)
{
    Assert.IsTrue(ex.Message.Contains("parameter cannot be null"));
}

// ✅ DO: Validate specific exception properties when relevant
ExceptionTestHelper.AssertThrowsWithValidation<ComInteropException>(
    () => comService.FailingOperation(),
    ex => {
        Assert.AreEqual("OPERATION_FAILED", ex.ErrorCode);
        Assert.IsTrue(ex.IsRetryable);
    });

// ❌ DON'T: Ignore exception details when they're important
ExceptionTestHelper.AssertThrows<ComInteropException>(
    () => comService.FailingOperation());
// Missing validation of error codes, retry ability, etc.
```

## LoggerTestExtensions Guide

### Purpose and Benefits
The `LoggerTestExtensions` provide a fluent API for comprehensive logger verification, ensuring proper logging behaviour throughout the application.

**Benefits**:
- Fluent, readable API for logger verification
- Support for message content, log level, and exception validation
- Negative testing capabilities (verify logs were NOT written)
- Counting and timing-based verification
- Integration with Moq for seamless mock logger testing

### Basic Log Message Verification

#### Simple Log Message Testing
```csharp
[TestMethod]
[TestCategory(TestCategories.Unit)]
public void ProcessData_WithValidInput_LogsSuccessMessage()
{
    // Arrange
    var mockLogger = new Mock<ILogger<DataProcessor>>();
    var processor = new DataProcessor(mockLogger.Object);
    
    // Act
    var result = processor.ProcessData("valid-data");
    
    // Assert: Basic log verification
    mockLogger.VerifyLogMessage(LogLevel.Information, "Data processed successfully");
    
    // Verify specific timing (default: Times.AtLeastOnce())
    mockLogger.VerifyLogMessage(LogLevel.Debug, "Processing started", Times.Once());
}
```

#### Log Level and Count Verification
```csharp
[TestMethod]
[TestCategory(TestCategories.Unit)]
[TestCategory(TestCategories.Performance)]
public void BulkProcessor_WithLargeDataset_LogsProgressMessages()
{
    // Arrange
    var mockLogger = new Mock<ILogger<BulkProcessor>>();
    var processor = new BulkProcessor(mockLogger.Object);
    var dataItems = Enumerable.Range(1, 1000).Select(i => $"item-{i}").ToArray();
    
    // Act
    processor.ProcessBulkData(dataItems);
    
    // Assert: Verify log count for progress messages
    mockLogger.VerifyLogCount(LogLevel.Information, 10); // Progress logged every 100 items
    
    // Verify no error or warning logs
    mockLogger.VerifyNoLogsAtOrAboveLevel(LogLevel.Warning);
}
```

### Exception Logging Verification

#### Exception with Context Verification
```csharp
[TestMethod]
[TestCategory(TestCategories.Unit)]
[TestCategory(TestCategories.ComInterop)]
public void ComOperation_WithException_LogsExceptionWithContext()
{
    // Arrange
    var mockLogger = new Mock<ILogger<ComService>>();
    var service = new ComService(mockLogger.Object);
    
    // Act: Trigger COM exception
    try
    {
        service.ExecuteComOperation("invalid-operation");
    }
    catch (ComInteropException)
    {
        // Expected exception
    }
    
    // Assert: Verify exception logging with context
    mockLogger.VerifyLogMessageWithException(
        LogLevel.Error, 
        "COM operation failed", 
        typeof(ComInteropException),
        Times.Once());
    
    // Verify operation context is logged
    mockLogger.VerifyLogMessage(LogLevel.Error, "Operation: invalid-operation");
}
```

### Fluent API for Complex Log Verification

#### Fluent Assertion Patterns
```csharp
[TestMethod]
[TestCategory(TestCategories.Integration)]
[TestCategory(TestCategories.Security)]
public void SecurityService_WithUnauthorizedAccess_LogsSecurityEvent()
{
    // Arrange
    var mockLogger = new Mock<ILogger<SecurityService>>();
    var service = new SecurityService(mockLogger.Object);
    
    // Act: Attempt unauthorized access
    try
    {
        service.AccessProtectedResource("unauthorized-user", "invalid-token");
    }
    catch (UnauthorizedAccessException)
    {
        // Expected security exception
    }
    
    // Assert: Fluent log verification
    mockLogger.ShouldHaveLogged()
        .AtLevel(LogLevel.Warning)
        .WithMessage("Unauthorized access attempt")
        .WithException<UnauthorizedAccessException>()
        .Times(Times.Once())
        .Verify();
    
    // Verify security audit logging
    mockLogger.ShouldHaveLogged()
        .AtLevel(LogLevel.Critical)
        .WithMessage("Security violation detected")
        .Times(Times.Once())
        .Verify();
}
```

#### Multiple Log Verification in Single Test
```csharp
[TestMethod]
[TestCategory(TestCategories.Integration)]
public void ComplexWorkflow_ExecutesSuccessfully_LogsAllStages()
{
    // Arrange
    var mockLogger = new Mock<ILogger<WorkflowService>>();
    var service = new WorkflowService(mockLogger.Object);
    
    // Act
    var result = service.ExecuteComplexWorkflow("workflow-data");
    
    // Assert: Multiple fluent verifications
    // Verify workflow start
    mockLogger.ShouldHaveLogged()
        .AtLevel(LogLevel.Information)
        .WithMessage("Workflow started")
        .Times(Times.Once())
        .Verify();
    
    // Verify progress logging
    mockLogger.ShouldHaveLogged()
        .AtLevel(LogLevel.Debug)
        .WithMessage("Processing stage")
        .Times(Times.Exactly(3)) // 3 processing stages
        .Verify();
    
    // Verify completion
    mockLogger.ShouldHaveLogged()
        .AtLevel(LogLevel.Information)
        .WithMessage("Workflow completed successfully")
        .Times(Times.Once())
        .Verify();
    
    // Verify no errors occurred
    mockLogger.VerifyNoLogsAtOrAboveLevel(LogLevel.Warning);
}
```

### Advanced Logger Testing Patterns

#### Custom Mock Logger Setup
```csharp
public class LoggerTestBase<T>
{
    protected Mock<ILogger<T>> MockLogger { get; private set; }
    protected List<LogEntry> LogEntries { get; private set; }
    
    [TestInitialize]
    public void TestInitialize()
    {
        MockLogger = new Mock<ILogger<T>>();
        LogEntries = new List<LogEntry>();
        
        // Setup mock to capture all log entries
        MockLogger.Setup(x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception, string>>()))
            .Callback<LogLevel, EventId, It.IsAnyType, Exception, Func<It.IsAnyType, Exception, string>>(
                (level, eventId, state, exception, formatter) =>
                {
                    LogEntries.Add(new LogEntry
                    {
                        Level = level,
                        EventId = eventId,
                        Message = formatter(state, exception),
                        Exception = exception,
                        Timestamp = DateTime.UtcNow
                    });
                });
    }
    
    protected void AssertLogSequence(params (LogLevel level, string messageContains)[] expectedLogs)
    {
        Assert.AreEqual(expectedLogs.Length, LogEntries.Count, 
            "Log count mismatch. Expected: {0}, Actual: {1}", 
            expectedLogs.Length, LogEntries.Count);
        
        for (int i = 0; i < expectedLogs.Length; i++)
        {
            var expected = expectedLogs[i];
            var actual = LogEntries[i];
            
            Assert.AreEqual(expected.level, actual.Level, 
                $"Log level mismatch at index {i}");
            Assert.IsTrue(actual.Message.Contains(expected.messageContains, StringComparison.OrdinalIgnoreCase),
                $"Log message at index {i} does not contain expected text '{expected.messageContains}'. " +
                $"Actual message: '{actual.Message}'");
        }
    }
}

public class LogEntry
{
    public LogLevel Level { get; set; }
    public EventId EventId { get; set; }
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; }
}
```

## TestCategories System

### Comprehensive Category Reference

#### Core Categories
```csharp
// Unit tests - fast, isolated, no external dependencies
[TestCategory(TestCategories.Unit)]

// Integration tests - test component interactions
[TestCategory(TestCategories.Integration)]

// Performance tests - timing and resource usage validation
[TestCategory(TestCategories.Performance)]

// Security tests - access control and security boundary validation
[TestCategory(TestCategories.Security)]
```

#### Specialized Categories
```csharp
// COM interop tests - COM object lifecycle and interaction
[TestCategory(TestCategories.ComInterop)]

// MCP protocol tests - protocol compliance and message handling
[TestCategory(TestCategories.McpProtocol)]

// Visual Studio automation tests - VS API interaction
[TestCategory(TestCategories.VisualStudioAutomation)]

// XAML designer tests - XAML manipulation and designer interaction
[TestCategory(TestCategories.XamlDesigner)]

// Imaging tests - screen capture and image processing
[TestCategory(TestCategories.Imaging)]

// Memory management tests - resource disposal and leak detection
[TestCategory(TestCategories.MemoryManagement)]

// Concurrency tests - thread safety and concurrent operations
[TestCategory(TestCategories.Concurrency)]

// Debugging automation tests - debugging session management
[TestCategory(TestCategories.DebuggingAutomation)]
```

### Test Filtering and Execution Strategies

#### Category-Based Test Execution
```bash
# Quick developer feedback (Unit tests only)
dotnet test --filter TestCategory=Unit

# Security validation
dotnet test --filter TestCategory=Security

# Performance validation
dotnet test --filter TestCategory=Performance

# COM interop testing
dotnet test --filter TestCategory=ComInterop

# Integration testing (excluding external dependencies)
dotnet test --filter "TestCategory=Integration&TestCategory!=VisualStudioAutomation"

# Combined categories
dotnet test --filter "TestCategory=Unit|TestCategory=Security"

# Exclude slow tests
dotnet test --filter "TestCategory!=Performance&TestCategory!=Integration"
```

#### Category-Based CI/CD Pipeline Integration
```yaml
# Example pipeline configuration
stages:
  - name: "Unit Tests"
    command: "dotnet test --filter TestCategory=Unit"
    parallel: true
    timeout: "5 minutes"
    
  - name: "Security Tests"
    command: "dotnet test --filter TestCategory=Security"
    depends_on: ["Unit Tests"]
    timeout: "10 minutes"
    
  - name: "Performance Tests"
    command: "dotnet test --filter TestCategory=Performance"
    depends_on: ["Unit Tests"]
    timeout: "15 minutes"
    
  - name: "Integration Tests"
    command: "dotnet test --filter TestCategory=Integration"
    depends_on: ["Security Tests"]
    timeout: "20 minutes"
```

### Custom Category Creation Guidelines

#### Creating Project-Specific Categories
```csharp
public static class ProjectSpecificTestCategories
{
    // Extend base categories for project-specific needs
    public const string DatabaseMigration = "DatabaseMigration";
    public const string ExternalApi = "ExternalApi";
    public const string FileSystemIntegration = "FileSystemIntegration";
    public const string NetworkCommunication = "NetworkCommunication";
    
    // Composite categories for complex scenarios
    public const string EndToEndWorkflow = "EndToEndWorkflow";
    public const string DisasterRecovery = "DisasterRecovery";
    public const string PerformanceRegression = "PerformanceRegression";
}
```

#### Category Combination Patterns
```csharp
[TestMethod]
[TestCategory(TestCategories.Integration)]
[TestCategory(TestCategories.Security)]
[TestCategory(TestCategories.Performance)]
public void SecureHighPerformanceIntegration_AllRequirements_MeetsStandards()
{
    // Test that validates integration, security, and performance simultaneously
}

[TestMethod]
[TestCategory(TestCategories.Unit)]
[TestCategory(TestCategories.ComInterop)]
[TestCategory(TestCategories.MemoryManagement)]
public void ComObjectDisposal_UnitTest_ProperCleanup()
{
    // Unit test specifically for COM object disposal patterns
}
```

## Shared Test Utilities

### Mock Object Creation Patterns

#### Standardized Mock Setup
```csharp
public static class MockProviders
{
    public static Mock<ILogger<T>> CreateMockLogger<T>()
    {
        var mockLogger = new Mock<ILogger<T>>();
        
        // Standard setup for logger mocks
        mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        
        return mockLogger;
    }
    
    public static Mock<IVisualStudioService> CreateMockVisualStudioService()
    {
        var mockService = new Mock<IVisualStudioService>();
        
        // Default setup for successful operations
        mockService.Setup(x => x.GetRunningInstancesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VisualStudioInstance>
            {
                new VisualStudioInstance { ProcessId = 1234, Version = "17.0", SolutionPath = @"C:\Test\Solution.sln" }
            });
            
        mockService.Setup(x => x.IsConnectionHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
            
        return mockService;
    }
    
    public static Mock<IComInteropHelper> CreateMockComInteropHelper()
    {
        var mockHelper = new Mock<IComInteropHelper>();
        
        // Setup for successful COM operations
        mockHelper.Setup(x => x.SafeComOperation<object>(
            It.IsAny<Func<object>>(), 
            It.IsAny<ILogger>(), 
            It.IsAny<string>()))
            .Returns<Func<object>, ILogger, string>((operation, logger, name) => operation());
            
        return mockHelper;
    }
}
```

### Test Data Generation Strategies

#### Realistic Test Data Creation
```csharp
public static class TestDataGenerators
{
    private static readonly Random _random = new Random(12345); // Fixed seed for reproducibility
    
    public static VisualStudioInstance CreateTestVisualStudioInstance(
        int? processId = null,
        string version = null,
        string solutionPath = null)
    {
        return new VisualStudioInstance
        {
            ProcessId = processId ?? _random.Next(1000, 9999),
            Version = version ?? $"17.{_random.Next(0, 10)}.{_random.Next(0, 10)}",
            SolutionPath = solutionPath ?? $@"C:\TestSolutions\Solution{_random.Next(1, 100)}.sln"
        };
    }
    
    public static BuildResult CreateTestBuildResult(
        bool success = true,
        int errorCount = 0,
        int warningCount = 0)
    {
        var result = new BuildResult
        {
            Success = success,
            ErrorCount = errorCount,
            WarningCount = warningCount,
            ElapsedTime = TimeSpan.FromSeconds(_random.Next(1, 300))
        };
        
        // Add realistic errors if specified
        for (int i = 0; i < errorCount; i++)
        {
            result.Errors.Add(new BuildError
            {
                Code = $"CS{_random.Next(1000, 9999)}",
                Message = $"Test error message {i + 1}",
                File = $@"C:\TestProject\File{i + 1}.cs",
                Line = _random.Next(1, 1000),
                Column = _random.Next(1, 100)
            });
        }
        
        return result;
    }
    
    public static ProjectInfo CreateTestProjectInfo(
        string name = null,
        ProjectType type = ProjectType.CSharp,
        string targetFramework = null)
    {
        return new ProjectInfo
        {
            Name = name ?? $"TestProject{_random.Next(1, 100)}",
            ProjectType = type,
            TargetFrameworks = targetFramework ?? "net8.0",
            FilePath = $@"C:\TestProjects\{name ?? $"TestProject{_random.Next(1, 100)}"}\Project.csproj"
        };
    }
}
```

### Common Test Setup and Teardown Patterns

#### Base Test Class with Common Setup
```csharp
[TestClass]
public abstract class TestBase<T> where T : class
{
    protected Mock<ILogger<T>> MockLogger { get; private set; }
    protected CancellationTokenSource CancellationTokenSource { get; private set; }
    protected string TestTempDirectory { get; private set; }
    
    [TestInitialize]
    public virtual void TestInitialize()
    {
        MockLogger = MockProviders.CreateMockLogger<T>();
        CancellationTokenSource = new CancellationTokenSource();
        TestTempDirectory = Path.Combine(Path.GetTempPath(), "VSMcpTests", Guid.NewGuid().ToString());
        
        if (!Directory.Exists(TestTempDirectory))
        {
            Directory.CreateDirectory(TestTempDirectory);
        }
    }
    
    [TestCleanup]
    public virtual void TestCleanup()
    {
        CancellationTokenSource?.Dispose();
        
        if (Directory.Exists(TestTempDirectory))
        {
            try
            {
                Directory.Delete(TestTempDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                // Log cleanup failure but don't fail the test
                Console.WriteLine($"Failed to cleanup test directory {TestTempDirectory}: {ex.Message}");
            }
        }
    }
    
    protected void LogTestInfo(string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {typeof(T).Name}: {message}");
    }
}
```

#### Integration Test Base with Resource Management
```csharp
[TestClass]
public abstract class IntegrationTestBase<T> : TestBase<T> where T : class
{
    protected TestServiceProvider ServiceProvider { get; private set; }
    protected IServiceScope TestScope { get; private set; }
    
    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
        
        // Setup dependency injection for integration tests
        var services = new ServiceCollection();
        ConfigureServices(services);
        
        ServiceProvider = new TestServiceProvider(services.BuildServiceProvider());
        TestScope = ServiceProvider.CreateScope();
    }
    
    [TestCleanup]
    public override void TestCleanup()
    {
        TestScope?.Dispose();
        ServiceProvider?.Dispose();
        base.TestCleanup();
    }
    
    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Override in derived classes to configure specific services
        services.AddLogging(builder => builder.AddConsole());
        services.AddSingleton(MockLogger.Object);
    }
    
    protected T GetRequiredService<TService>()
    {
        return TestScope.ServiceProvider.GetRequiredService<TService>();
    }
}
```

## Integration with MSTest and Moq Frameworks

### MSTest Integration Patterns

#### Test Method Attributes and Organization
```csharp
[TestClass]
public class ExampleServiceTests : TestBase<ExampleService>
{
    [TestMethod]
    [TestCategory(TestCategories.Unit)]
    [TestCategory(TestCategories.Performance)]
    [Description("Verifies that the service processes data within performance thresholds")]
    public async Task ProcessData_WithLargeDataset_MeetsPerformanceThresholds()
    {
        // Arrange
        var service = new ExampleService(MockLogger.Object);
        var largeDataset = TestDataGenerators.CreateLargeDataset(10000);
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await service.ProcessDataAsync(largeDataset);
        stopwatch.Stop();
        
        // Assert
        Assert.IsTrue(result.Success);
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, 
            $"Processing took {stopwatch.ElapsedMilliseconds}ms, exceeds 5000ms threshold");
            
        // Verify logging
        MockLogger.VerifyLogMessage(LogLevel.Information, "Data processing completed");
    }
    
    [DataTestMethod]
    [DataRow(100, true)]
    [DataRow(1000, true)]
    [DataRow(10000, false)] // Expected to fail with large dataset
    [TestCategory(TestCategories.Unit)]
    public void ProcessData_WithVariousDataSizes_ReturnsExpectedResults(int dataSize, bool expectedSuccess)
    {
        // Arrange
        var service = new ExampleService(MockLogger.Object);
        var dataset = TestDataGenerators.CreateDataset(dataSize);
        
        // Act
        var result = service.ProcessData(dataset);
        
        // Assert
        Assert.AreEqual(expectedSuccess, result.Success);
        
        if (expectedSuccess)
        {
            MockLogger.VerifyLogMessage(LogLevel.Information, "processing completed");
        }
        else
        {
            MockLogger.VerifyLogMessage(LogLevel.Warning, "processing failed");
        }
    }
}
```

### Moq Framework Integration

#### Advanced Mock Setup and Verification
```csharp
[TestMethod]
[TestCategory(TestCategories.Integration)]
[TestCategory(TestCategories.ComInterop)]
public async Task ComplexService_WithMockedDependencies_ExecutesCorrectWorkflow()
{
    // Arrange: Setup complex mock interactions
    var mockVisualStudioService = new Mock<IVisualStudioService>();
    var mockComHelper = new Mock<IComInteropHelper>();
    var mockMemoryMonitor = new Mock<IMemoryMonitor>();
    
    // Setup sequential mock responses
    mockVisualStudioService.SetupSequence(x => x.IsConnectionHealthyAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(false) // First call - connection unhealthy
        .ReturnsAsync(true)  // Second call - connection restored
        .ReturnsAsync(true); // Third call - connection stable
    
    // Setup conditional mock behaviour
    mockComHelper.Setup(x => x.SafeComOperation<bool>(
            It.IsAny<Func<bool>>(),
            It.IsAny<ILogger>(),
            It.Is<string>(s => s.Contains("reconnect"))))
        .Returns(true)
        .Callback<Func<bool>, ILogger, string>((operation, logger, operationName) =>
        {
            // Verify operation is called with correct parameters
            Assert.IsTrue(operationName.Contains("reconnect"));
        });
    
    // Setup memory monitoring mock
    mockMemoryMonitor.Setup(x => x.GetCurrentMemoryUsageAsync())
        .ReturnsAsync(new MemoryInfo { UsedBytes = 1024 * 1024 * 50 }); // 50MB
    
    var service = new ComplexService(
        mockVisualStudioService.Object, 
        mockComHelper.Object, 
        mockMemoryMonitor.Object, 
        MockLogger.Object);
    
    // Act
    var result = await service.ExecuteComplexWorkflowAsync(CancellationToken.None);
    
    // Assert: Verify workflow execution
    Assert.IsTrue(result.Success);
    
    // Verify specific method call sequences
    mockVisualStudioService.Verify(x => x.IsConnectionHealthyAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    mockComHelper.Verify(x => x.SafeComOperation<bool>(
        It.IsAny<Func<bool>>(),
        It.IsAny<ILogger>(),
        It.Is<string>(s => s.Contains("reconnect"))), Times.Once);
    mockMemoryMonitor.Verify(x => x.GetCurrentMemoryUsageAsync(), Times.AtLeastOnce);
    
    // Verify logging workflow
    MockLogger.ShouldHaveLogged()
        .AtLevel(LogLevel.Warning)
        .WithMessage("Connection unhealthy, attempting reconnection")
        .Times(Times.Once())
        .Verify();
        
    MockLogger.ShouldHaveLogged()
        .AtLevel(LogLevel.Information)
        .WithMessage("Complex workflow completed successfully")
        .Times(Times.Once())
        .Verify();
}
```

## Conclusion

The test utilities framework provides comprehensive support for consistent, productive testing across the Visual Studio MCP Server project. Key benefits include:

- **Standardised Patterns**: Consistent exception testing, logging verification, and test organisation
- **Developer Productivity**: Reduced boilerplate code and clear, fluent APIs
- **Quality Assurance**: Robust utilities that ensure comprehensive test coverage
- **Maintainability**: Well-documented, extensible utility classes
- **Integration**: Seamless integration with MSTest and Moq frameworks

Regular use of these utilities ensures high-quality, maintainable tests that provide reliable validation of system behaviour and performance characteristics.