# Testing Strategy Guide

## Overview

This document establishes comprehensive testing standards for the Visual Studio MCP Server project. It serves as the foundation for all testing activities and ensures consistent quality across all development phases.

## Testing Philosophy

### Core Principles
- **Quality Gates**: All code must pass defined quality thresholds before integration
- **Categorised Testing**: Tests are organised by purpose and execution requirements
- **Security First**: Security testing is mandatory for all contributions
- **Performance Awareness**: Performance characteristics are validated continuously
- **Automation**: Testing is integrated into CI/CD pipelines for consistent execution

### Quality Standards
- **Minimum Code Coverage**: >90% for core services, >80% for supporting components
- **Security Testing**: Required for all changes that touch COM interop or external APIs
- **Performance Testing**: All operations must meet production-grade thresholds
- **Integration Testing**: Real-world scenarios validated with actual dependencies

## Test Categories System

Our testing framework uses 12 standardised categories for organisation and execution filtering:

### Core Categories

#### `Unit`
- **Purpose**: Test individual components in isolation with mocked dependencies
- **Requirements**: Fast execution (<100ms per test), no external resources
- **Examples**: Service method validation, data model testing, algorithm verification
- **Execution**: `dotnet test --filter TestCategory=Unit`

#### `Integration` 
- **Purpose**: Test interactions between components or with external systems
- **Requirements**: May require specific environment setup, longer execution acceptable
- **Examples**: Service composition, database interactions, file system operations
- **Execution**: `dotnet test --filter TestCategory=Integration`

#### `Performance`
- **Purpose**: Validate timing, memory usage, and scalability requirements
- **Requirements**: Specific timing thresholds, memory monitoring, stress testing
- **Examples**: Operation timing validation, memory leak detection, concurrency testing
- **Execution**: `dotnet test --filter TestCategory=Performance`

#### `Security`
- **Purpose**: Validate access controls, input validation, and security boundaries
- **Requirements**: Security vulnerability prevention, authorisation testing
- **Examples**: Input sanitisation, COM security boundaries, authentication flows
- **Execution**: `dotnet test --filter TestCategory=Security`

### Specialised Categories

#### `ComInterop`
- **Purpose**: Test COM object lifecycle management and interaction patterns
- **Requirements**: Proper COM disposal, exception handling, marshalling validation
- **Examples**: EnvDTE interaction, COM cleanup testing, marshalling validation
- **Execution**: `dotnet test --filter TestCategory=ComInterop`

#### `McpProtocol`
- **Purpose**: Validate Model Context Protocol compliance and message handling
- **Requirements**: Protocol specification adherence, serialisation validation
- **Examples**: Message format validation, tool response compliance, error handling
- **Execution**: `dotnet test --filter TestCategory=McpProtocol`

#### `VisualStudioAutomation`
- **Purpose**: Test interactions with Visual Studio through APIs
- **Requirements**: May require Visual Studio availability, handles VS crashes gracefully
- **Examples**: Solution building, debugging control, project navigation
- **Execution**: `dotnet test --filter TestCategory=VisualStudioAutomation`

#### `XamlDesigner`
- **Purpose**: Test XAML parsing, modification, and designer interaction
- **Requirements**: XAML manipulation validation, designer window detection
- **Examples**: XAML parsing, designer surface capture, markup modification
- **Execution**: `dotnet test --filter TestCategory=XamlDesigner`

#### `Imaging`
- **Purpose**: Validate screen capture and image processing capabilities
- **Requirements**: Graphics subsystem availability, window management
- **Examples**: Screenshot capture, image quality validation, window detection
- **Execution**: `dotnet test --filter TestCategory=Imaging`

#### `MemoryManagement`
- **Purpose**: Validate proper resource disposal and memory usage patterns
- **Requirements**: Memory monitoring, garbage collection testing, leak detection
- **Examples**: COM object disposal, memory growth monitoring, resource cleanup
- **Execution**: `dotnet test --filter TestCategory=MemoryManagement`

#### `Concurrency`
- **Purpose**: Validate thread safety and concurrent operation handling
- **Requirements**: Race condition detection, synchronisation validation
- **Examples**: Concurrent API calls, thread safety testing, lock validation
- **Execution**: `dotnet test --filter TestCategory=Concurrency`

#### `DebuggingAutomation`
- **Purpose**: Test debugging session management and control
- **Requirements**: Visual Studio debugging API interaction
- **Examples**: Breakpoint management, debugging session control, variable inspection
- **Execution**: `dotnet test --filter TestCategory=DebuggingAutomation`

## Quality Gates

### Performance Thresholds

#### Production-Grade Timing Requirements
- **GetRunningInstancesAsync**: Maximum 1000ms execution time
  - *Rationale*: Enterprise environment with multiple VS instances
  - *Validation*: Automated performance testing in CI/CD pipeline
  - *Failure Action*: Performance regression investigation required

- **IsConnectionHealthyAsync**: Maximum 500ms execution time
  - *Rationale*: Frequent health checks require rapid response
  - *Validation*: Stress testing with concurrent health checks
  - *Failure Action*: Connection optimisation investigation required

#### Memory Management Requirements
- **Memory Growth Tolerance**: Maximum 20MB per operation cycle
  - *Rationale*: COM interop operations must not cause memory leaks
  - *Validation*: Memory monitoring over 100 operation cycles
  - *Failure Action*: Memory leak investigation and COM cleanup review

- **Concurrent Operations**: Support minimum 50 simultaneous operations
  - *Rationale*: Multi-user enterprise scenarios require high concurrency
  - *Validation*: Stress testing with 50+ parallel operations
  - *Failure Action*: Concurrency bottleneck analysis required

### Code Coverage Requirements

#### Core Services (>90% Coverage Required)
- `VisualStudioService`: >95% line coverage, >90% branch coverage
- `ComInteropHelper`: >95% line coverage, >95% branch coverage
- `MemoryMonitor`: >90% line coverage, >85% branch coverage
- `MCP Protocol Handlers`: >95% line coverage, >90% branch coverage

#### Supporting Components (>80% Coverage Required)
- `Imaging Services`: >85% line coverage, >80% branch coverage
- `XAML Services`: >85% line coverage, >80% branch coverage
- `Debug Services`: >80% line coverage, >75% branch coverage
- `Shared Models`: >90% line coverage, >85% branch coverage

### Security Requirements

#### Mandatory Security Testing
- **Input Validation**: All external inputs must have validation tests
- **COM Security Boundaries**: All COM operations must have security tests
- **Access Control**: Authentication and authorisation paths must be tested
- **Injection Prevention**: SQL injection and code injection prevention tests

#### Security Test Categories
- **Reflection Security**: No unsafe reflection-based access control bypasses
- **Cryptographic Security**: Secure random generation for all test scenarios
- **COM Security**: Secure GUID generation and interface boundaries
- **Process Security**: Secure process identification and interaction

## Test Infrastructure

### Shared Test Utilities

#### ExceptionTestHelper
Standardised exception testing patterns for consistent validation:

```csharp
// Synchronous exception testing
var exception = ExceptionTestHelper.AssertThrows<ComInteropException>(
    () => ComInteropHelper.SafeComOperation(faultyOperation, logger, "TestOp"),
    "TestOp");

// Asynchronous exception testing  
var exception = await ExceptionTestHelper.AssertThrowsAsync<InvalidOperationException>(
    async () => await service.PerformInvalidOperationAsync(),
    "Invalid operation");

// Exception validation with custom properties
ExceptionTestHelper.AssertThrowsWithValidation<ComInteropException>(
    () => ComInteropHelper.GetActiveVisualStudioInstance(),
    ex => {
        Assert.AreEqual("VS_E_NOTFOUND", ex.ErrorCode);
        Assert.IsTrue(ex.IsRetryable);
    });
```

#### LoggerTestExtensions  
Fluent API for comprehensive logger verification:

```csharp
// Simple log message verification
mockLogger.VerifyLogMessage(LogLevel.Error, "Connection failed", Times.Once());

// Exception logging verification
mockLogger.VerifyLogMessageWithException(LogLevel.Error, "COM operation failed", 
    typeof(ComInteropException), Times.AtLeastOnce());

// Fluent assertion patterns
mockLogger.ShouldHaveLogged()
    .AtLevel(LogLevel.Warning)
    .WithMessage("Memory pressure detected")
    .WithException<MemoryPressureException>()
    .Times(Times.Exactly(3))
    .Verify();

// Negative verification
mockLogger.VerifyNoLogsAtOrAboveLevel(LogLevel.Error);
```

#### TestCategories System
Comprehensive categorisation for organised test execution:

```csharp
[TestMethod]
[TestCategory(TestCategories.Unit)]
[TestCategory(TestCategories.ComInterop)]
[TestCategory(TestCategories.Security)]
public void SafeComOperation_WithSecurityValidation_PreventsBypass()
{
    // Test implementation ensuring COM security boundaries
}
```

### Test Execution Patterns

#### Category-Based Execution
```bash
# Run all unit tests (fast feedback)
dotnet test --filter TestCategory=Unit

# Run security tests (security validation)
dotnet test --filter TestCategory=Security

# Run performance tests (threshold validation)
dotnet test --filter TestCategory=Performance

# Run integration tests (full validation)
dotnet test --filter TestCategory=Integration

# Combined category execution
dotnet test --filter "TestCategory=Unit|TestCategory=Security"

# Exclude long-running tests
dotnet test --filter "TestCategory!=Performance&TestCategory!=Integration"
```

#### CI/CD Pipeline Integration
```yaml
# Example pipeline stages
stages:
  - name: "Fast Feedback"
    command: "dotnet test --filter TestCategory=Unit"
    timeout: "5 minutes"
    
  - name: "Security Validation"  
    command: "dotnet test --filter TestCategory=Security"
    timeout: "10 minutes"
    
  - name: "Performance Validation"
    command: "dotnet test --filter TestCategory=Performance"
    timeout: "15 minutes"
    
  - name: "Full Integration"
    command: "dotnet test --filter TestCategory=Integration"
    timeout: "30 minutes"
```

## Testing Standards by Component Type

### COM Interop Testing Standards

#### Required Test Categories
- `[TestCategory(TestCategories.ComInterop)]`
- `[TestCategory(TestCategories.Security)]` (for security boundary testing)
- `[TestCategory(TestCategories.MemoryManagement)]` (for disposal testing)

#### Mandatory Test Scenarios
- **COM Object Lifecycle**: Creation, usage, proper disposal with `Marshal.ReleaseComObject()`
- **Exception Handling**: COM exceptions properly wrapped and contextualised
- **Timeout Handling**: Operations respect timeout parameters and cancel appropriately
- **Security Boundaries**: COM security interfaces properly validated
- **Memory Management**: No COM object leaks, proper cleanup in exception scenarios

#### Example Test Structure
```csharp
[TestMethod]
[TestCategory(TestCategories.ComInterop)]
[TestCategory(TestCategories.Security)]  
[TestCategory(TestCategories.MemoryManagement)]
public void SafeComOperation_WithProperCleanup_DisposesCorrectly()
{
    // Arrange: Setup COM operation and monitoring
    var memoryBefore = GC.GetTotalMemory(true);
    var mockLogger = new Mock<ILogger<ComInteropHelper>>();
    
    // Act: Execute COM operation
    var result = ComInteropHelper.SafeComOperation(() => {
        var comObject = GetTestComObject();
        return ProcessComObject(comObject);
    }, mockLogger.Object, "TestOperation");
    
    // Assert: Verify results and cleanup
    Assert.IsNotNull(result);
    
    // Force garbage collection and verify memory cleanup
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    
    var memoryAfter = GC.GetTotalMemory(true);
    var memoryGrowth = memoryAfter - memoryBefore;
    
    Assert.IsTrue(memoryGrowth < 1024 * 1024, // 1MB tolerance
        $"Memory growth {memoryGrowth} bytes exceeds tolerance");
        
    // Verify proper logging
    mockLogger.VerifyLogMessage(LogLevel.Debug, "TestOperation completed successfully");
}
```

### MCP Protocol Testing Standards

#### Required Test Categories
- `[TestCategory(TestCategories.McpProtocol)]`
- `[TestCategory(TestCategories.Unit)]` (for message parsing)
- `[TestCategory(TestCategories.Integration)]` (for end-to-end protocol testing)

#### Mandatory Test Scenarios
- **Message Serialisation**: All models serialise/deserialise correctly
- **Protocol Compliance**: Response formats match MCP specification
- **Error Handling**: Protocol errors properly formatted and transmitted
- **Schema Validation**: All protocol messages validate against schema
- **Tool Registration**: Tools properly register and respond to requests

### Performance Testing Standards

#### Required Test Categories
- `[TestCategory(TestCategories.Performance)]`
- `[TestCategory(TestCategories.Concurrency)]` (for multi-threaded scenarios)

#### Mandatory Performance Scenarios
- **Timing Validation**: Operations complete within specified thresholds
- **Memory Monitoring**: Memory usage stays within defined limits
- **Concurrency Testing**: System handles specified concurrent load
- **Stress Testing**: Degradation patterns under extreme load
- **Resource Cleanup**: Performance doesn't degrade over time

#### Example Performance Test
```csharp
[TestMethod]
[TestCategory(TestCategories.Performance)]
[TestCategory(TestCategories.Concurrency)]
public async Task GetRunningInstances_Under50ConcurrentCalls_MeetsPerformanceThresholds()
{
    // Arrange: Setup performance monitoring
    var service = new VisualStudioService(Mock.Of<ILogger<VisualStudioService>>());
    var stopwatch = Stopwatch.StartNew();
    var memoryBefore = GC.GetTotalMemory(true);
    
    // Act: Execute 50 concurrent operations
    var tasks = Enumerable.Range(0, 50)
        .Select(_ => service.GetRunningInstancesAsync(CancellationToken.None))
        .ToArray();
        
    var results = await Task.WhenAll(tasks);
    stopwatch.Stop();
    
    // Assert: Verify performance thresholds
    Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000,
        $"Operation took {stopwatch.ElapsedMilliseconds}ms, exceeds 1000ms threshold");
        
    var memoryAfter = GC.GetTotalMemory(true);
    var memoryGrowth = memoryAfter - memoryBefore;
    
    Assert.IsTrue(memoryGrowth < 20 * 1024 * 1024,
        $"Memory growth {memoryGrowth / 1024 / 1024}MB exceeds 20MB threshold");
        
    // Verify all operations succeeded
    Assert.AreEqual(50, results.Length);
    Assert.IsTrue(results.All(r => r != null));
}
```

## Continuous Integration Integration

### Pipeline Stages

#### Stage 1: Fast Feedback (2-5 minutes)
```bash
# Unit tests only - rapid developer feedback
dotnet test --filter TestCategory=Unit --logger "console;verbosity=minimal"
```

#### Stage 2: Security Validation (5-10 minutes)  
```bash
# Security tests - prevent vulnerability regression
dotnet test --filter TestCategory=Security --logger "console;verbosity=normal"
```

#### Stage 3: Component Integration (10-15 minutes)
```bash
# Integration tests without external dependencies
dotnet test --filter "TestCategory=Integration&TestCategory!=VisualStudioAutomation" --logger "console;verbosity=normal"
```

#### Stage 4: Performance Validation (15-20 minutes)
```bash
# Performance and concurrency tests
dotnet test --filter "TestCategory=Performance|TestCategory=Concurrency" --logger "console;verbosity=detailed"
```

#### Stage 5: Full Integration (20-30 minutes)
```bash
# Complete test suite including Visual Studio automation
dotnet test --logger "console;verbosity=detailed"
```

### Quality Gates Configuration

#### Mandatory Gates
- **Unit Test Success**: 100% pass rate required for merge
- **Security Test Success**: 100% pass rate required for merge  
- **Code Coverage**: Minimum thresholds enforced automatically
- **Performance Thresholds**: Automated performance regression detection
- **Static Analysis**: Security and quality analysis passes

#### Failure Actions
- **Test Failures**: Block merge, require investigation and fixes
- **Coverage Regression**: Block merge, require additional test coverage
- **Performance Regression**: Block merge, require performance investigation
- **Security Failures**: Immediate escalation, security team notification

## Testing Tools and Frameworks

### Primary Framework: MSTest
- **Rationale**: Native .NET integration, excellent Visual Studio support
- **Test Discovery**: Automatic discovery and execution
- **Parallel Execution**: Category-based parallel test execution
- **Assertion Library**: Rich assertion capabilities with clear failure messages

### Mocking Framework: Moq
- **Interface Mocking**: Comprehensive dependency injection mocking
- **Verification**: Method call verification and parameter validation
- **Setup/Returns**: Flexible mock behaviour configuration
- **Integration**: Seamless integration with MSTest and test utilities

### Coverage Analysis
- **Tool**: Built-in .NET coverage analysis
- **Reporting**: HTML and Cobertura format reports
- **Thresholds**: Automated threshold enforcement in CI/CD
- **Exclusions**: Generated code and platform-specific code excluded

### Performance Testing
- **Timing**: `System.Diagnostics.Stopwatch` for precise timing
- **Memory**: `GC.GetTotalMemory()` for memory usage monitoring
- **Concurrency**: `Task.WhenAll()` for concurrent operation testing
- **Stress Testing**: Configurable load generation and monitoring

## Best Practices

### Test Organisation
- **One Assert Per Test**: Each test validates a single behaviour
- **Descriptive Names**: Test names clearly describe the scenario and expected outcome
- **AAA Pattern**: Arrange, Act, Assert structure for clarity
- **Category Assignment**: All tests must have appropriate category assignments

### Data Management
- **Test Data**: Use realistic but non-sensitive test data
- **Cleanup**: Proper cleanup of test resources and temporary files
- **Isolation**: Tests must not depend on external state or other tests
- **Deterministic**: Tests must produce consistent, repeatable results

### Error Handling
- **Exception Testing**: Use ExceptionTestHelper for consistent exception validation
- **Error Messages**: Validate error message content and context
- **Recovery Testing**: Test error recovery and retry mechanisms
- **Logging Validation**: Verify appropriate logging during error scenarios

### Performance Testing
- **Realistic Thresholds**: Performance thresholds based on production requirements
- **Environment Consistency**: Performance tests run in consistent environment
- **Baseline Tracking**: Performance baseline tracking and regression detection
- **Resource Monitoring**: Comprehensive resource usage monitoring

## Troubleshooting Common Issues

### Test Execution Problems
- **Category Filtering**: Verify test category assignments and filter syntax
- **Dependency Issues**: Check test project references and package versions
- **Parallel Execution**: Investigate test isolation issues in parallel scenarios
- **Environment Setup**: Validate test environment configuration and prerequisites

### Performance Test Issues
- **Timing Variability**: Account for system load and timing variations
- **Memory Measurement**: Ensure proper garbage collection before memory measurements
- **Concurrency Problems**: Investigate thread safety and synchronisation issues
- **Resource Cleanup**: Verify proper resource disposal after performance tests

### Integration Test Issues
- **External Dependencies**: Handle external dependency availability and configuration
- **Visual Studio Availability**: Graceful handling when Visual Studio is not available
- **COM Registration**: Verify COM object registration and availability
- **Environment Differences**: Account for differences between development and CI environments

## Conclusion

This testing strategy provides comprehensive coverage for all aspects of the Visual Studio MCP Server project. By following these standards, we ensure:

- **Quality Assurance**: Consistent quality across all components
- **Security Protection**: Proactive security vulnerability prevention
- **Performance Reliability**: Production-grade performance characteristics
- **Maintainability**: Well-organised, maintainable test suites
- **Developer Productivity**: Clear patterns and utilities for efficient test development

Regular review and updates of this strategy ensure continuous improvement and adaptation to project needs.