# Contributing Guidelines

## Overview

Welcome to the Visual Studio MCP Server project! This document provides comprehensive guidelines for contributing to the project, with particular emphasis on our testing standards, security practices, and quality requirements established in Phase 6.

## Getting Started

### Prerequisites
- Visual Studio 2022 (17.8 or later) with .NET desktop development workload
- .NET 8 SDK (8.0.100 or later) on Windows
- Windows 10/11 (required for Visual Studio COM dependencies)
- Git with proper Windows line endings configured

### Development Environment Setup
1. Clone the repository: `git clone https://github.com/your-org/MCP-VS-AUTOMATION.git`
2. Open the solution in Visual Studio: `VisualStudioMcp.sln`
3. Restore NuGet packages: `dotnet restore`
4. Build the solution: Use MCP tools from WSL or build in Visual Studio
5. Run tests: `dotnet test` to verify your environment setup

## Project Structure

### Core Components
```
src/VisualStudioMcp.Server/     # Console application entry point
src/VisualStudioMcp.Core/       # Core VS automation services
src/VisualStudioMcp.Xaml/       # XAML designer automation
src/VisualStudioMcp.Debug/      # Debugging automation services
src/VisualStudioMcp.Imaging/    # Screenshot and visual capture
src/VisualStudioMcp.Shared/     # Common models and interfaces
tests/                          # Unit and integration test projects
docs/                           # Project documentation
```

### Technology Stack
- **Runtime**: .NET 8.0 (`net8.0-windows` target framework)
- **Language**: C# 12.0 with nullable reference types enabled
- **MCP Protocol**: ModelContextProtocol 0.3.0-preview.3
- **Visual Studio APIs**: EnvDTE 17.0.32112.339, EnvDTE80 8.0.1
- **Testing**: MSTest with Moq framework
- **COM Interop**: Windows-only, requires Visual Studio 2022

## Development Workflow

### Git Workflow Requirements
All contributions must follow our strict Git workflow:

1. **Never work in the main branch** - Always create feature branches
2. **Create descriptive branch names**: `feature/security-enhancements`, `bugfix/com-memory-leak`
3. **Make small, logical commits** with descriptive messages
4. **Push feature branch to GitHub** when ready for review
5. **Create pull request** using GitHub CLI: `gh pr create --title "Title" --body "Description"`
6. **Merge only after approval** and passing all quality gates

### Branch Naming Conventions
- Feature branches: `feature/description-of-feature`
- Bug fixes: `bugfix/description-of-bug`
- Documentation: `docs/description-of-documentation`
- Performance improvements: `perf/description-of-improvement`
- Security fixes: `security/description-of-security-fix`

## Testing Standards (MANDATORY)

### Test Coverage Requirements

#### Minimum Coverage Thresholds
- **Core Services**: >90% line coverage, >85% branch coverage (MANDATORY)
- **Supporting Components**: >80% line coverage, >75% branch coverage (MANDATORY)
- **Test Projects**: 100% compilation success required for merge
- **Security Components**: >95% line coverage, >90% branch coverage (MANDATORY)

#### Coverage Validation
```bash
# Check coverage before creating PR
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
# Coverage report generated in TestResults/coverage.opencover.xml
```

### Mandatory Test Categories

#### Required Test Categories for All New Features
Every new feature MUST include tests in these categories:

1. **Unit Tests** (`[TestCategory(TestCategories.Unit)]`)
   - Test individual components in isolation
   - Must complete in <100ms per test
   - No external dependencies
   - Example:
   ```csharp
   [TestMethod]
   [TestCategory(TestCategories.Unit)]
   public void ServiceMethod_WithValidInput_ReturnsExpectedResult()
   {
       // Test implementation
   }
   ```

2. **Integration Tests** (`[TestCategory(TestCategories.Integration)]`)
   - Test component interactions
   - May require environment setup
   - Example:
   ```csharp
   [TestMethod]
   [TestCategory(TestCategories.Integration)]
   public async Task ServiceIntegration_EndToEndFlow_ExecutesSuccessfully()
   {
       // Integration test implementation
   }
   ```

3. **Security Tests** (`[TestCategory(TestCategories.Security)]`)
   - **MANDATORY for all changes** that touch:
     - COM interop operations
     - External API interactions
     - Input validation
     - Authentication/authorization flows
   - Example:
   ```csharp
   [TestMethod]
   [TestCategory(TestCategories.Security)]
   public void InputValidation_WithMaliciousInput_RejectsSecurely()
   {
       // Security test implementation
   }
   ```

#### Performance Testing Requirements
All new features with performance implications MUST include:

1. **Performance Tests** (`[TestCategory(TestCategories.Performance)]`)
   - Validate timing thresholds:
     - `GetRunningInstancesAsync`: ≤1000ms
     - `IsConnectionHealthyAsync`: ≤500ms
     - Custom operations: Document and validate thresholds
   - Example:
   ```csharp
   [TestMethod]
   [TestCategory(TestCategories.Performance)]
   public async Task NewFeature_PerformanceRequirements_MeetsThresholds()
   {
       var stopwatch = Stopwatch.StartNew();
       await newFeature.ExecuteAsync();
       stopwatch.Stop();
       
       Assert.IsTrue(stopwatch.ElapsedMilliseconds <= 1000,
           $"Operation took {stopwatch.ElapsedMilliseconds}ms, exceeds 1000ms threshold");
   }
   ```

2. **Memory Management Tests** (`[TestCategory(TestCategories.MemoryManagement)]`)
   - Validate memory growth ≤20MB per operation cycle
   - Required for all COM interop changes
   - Example:
   ```csharp
   [TestMethod]
   [TestCategory(TestCategories.MemoryManagement)]
   public async Task NewComFeature_MemoryUsage_WithinThreshold()
   {
       var memoryBefore = GC.GetTotalMemory(true);
       
       // Execute operation 100 times
       for (int i = 0; i < 100; i++)
       {
           await newComFeature.ExecuteAsync();
       }
       
       var memoryAfter = GC.GetTotalMemory(true);
       var memoryGrowthMB = (memoryAfter - memoryBefore) / (1024.0 * 1024.0);
       
       Assert.IsTrue(memoryGrowthMB <= 20,
           $"Memory growth {memoryGrowthMB:F2}MB exceeds 20MB threshold");
   }
   ```

### Test Infrastructure Usage Requirements

#### Mandatory Use of Test Utilities

1. **ExceptionTestHelper** (REQUIRED for all exception testing)
   ```csharp
   // ✅ REQUIRED: Use ExceptionTestHelper for consistent patterns
   var exception = ExceptionTestHelper.AssertThrows<ArgumentException>(
       () => service.ProcessInvalidInput(null),
       "parameter cannot be null");
   
   // ❌ FORBIDDEN: Manual try-catch exception testing
   // try { ... } catch { ... } patterns are not allowed
   ```

2. **LoggerTestExtensions** (REQUIRED for all logging verification)
   ```csharp
   // ✅ REQUIRED: Use fluent API for log verification
   mockLogger.ShouldHaveLogged()
       .AtLevel(LogLevel.Error)
       .WithMessage("Operation failed")
       .WithException<ComInteropException>()
       .Times(Times.Once())
       .Verify();
   
   // ✅ REQUIRED: Use extension methods for simple verification
   mockLogger.VerifyLogMessage(LogLevel.Information, "Operation completed");
   ```

3. **TestCategories** (MANDATORY for all tests)
   ```csharp
   // ✅ REQUIRED: All tests must have appropriate categories
   [TestMethod]
   [TestCategory(TestCategories.Unit)]
   [TestCategory(TestCategories.Security)]
   public void SecurityValidation_UnitTest_PassesValidation()
   
   // ❌ FORBIDDEN: Tests without categories
   [TestMethod]
   public void SomeTest() // Missing test categories
   ```

4. **Shared Test Utilities** (RECOMMENDED for consistency)
   ```csharp
   // ✅ RECOMMENDED: Use standardized mock providers
   var mockLogger = MockProviders.CreateMockLogger<ServiceType>();
   var mockService = MockProviders.CreateMockVisualStudioService();
   
   // ✅ RECOMMENDED: Use test data generators
   var testInstance = TestDataGenerators.CreateTestVisualStudioInstance();
   var testResult = TestDataGenerators.CreateTestBuildResult(success: false, errorCount: 5);
   ```

### Test Execution Requirements

#### Pre-Commit Test Validation
Before creating any pull request, you MUST run and pass:

```bash
# 1. Unit tests (fast feedback)
dotnet test --filter TestCategory=Unit

# 2. Security tests (security validation)
dotnet test --filter TestCategory=Security

# 3. Performance tests (threshold validation)
dotnet test --filter TestCategory=Performance

# 4. Integration tests (full validation)
dotnet test --filter TestCategory=Integration
```

#### Test Quality Standards
- **Deterministic**: Tests must produce consistent, repeatable results
- **Isolated**: Tests must not depend on external state or other tests
- **Fast**: Unit tests must complete quickly (<100ms each)
- **Descriptive**: Test names must clearly describe scenario and expected outcome
- **One Assert**: Each test should validate a single behavior

## Security Requirements (MANDATORY)

### Security Testing Standards

#### Required Security Tests for All Changes
Every pull request touching security-sensitive areas MUST include:

1. **Input Validation Tests**
   ```csharp
   [TestMethod]
   [TestCategory(TestCategories.Security)]
   public void ValidateInput_WithMaliciousPayloads_RejectsSecurely()
   {
       var maliciousInputs = new[]
       {
           "'; DROP TABLE Users; --",          // SQL injection
           "<script>alert('xss')</script>",    // XSS
           "../../../etc/passwd",              // Path traversal
           "; rm -rf /",                       // Command injection
           new string('A', 10000)              // Buffer overflow
       };
       
       foreach (var input in maliciousInputs)
       {
           ExceptionTestHelper.AssertThrows<SecurityException>(
               () => service.ValidateInput(input),
               "security validation failed");
       }
   }
   ```

2. **COM Security Boundary Tests**
   ```csharp
   [TestMethod]
   [TestCategory(TestCategories.Security)]
   [TestCategory(TestCategories.ComInterop)]
   public void ComInterface_WithInvalidCredentials_DeniesAccess()
   {
       ExceptionTestHelper.AssertThrows<UnauthorizedAccessException>(
           () => comService.AccessProtectedMethod("invalid_user", "bad_token"));
   }
   ```

3. **Access Control Tests**
   ```csharp
   [TestMethod]
   [TestCategory(TestCategories.Security)]
   public void AuthorizeOperation_WithInsufficientPermissions_DeniesAccess()
   {
       ExceptionTestHelper.AssertThrows<UnauthorizedAccessException>(
           () => securityService.AuthorizeOperation("user", "admin_operation"));
   }
   ```

#### Security Code Patterns (MANDATORY)

1. **NO Reflection-Based Access Control Bypasses**
   ```csharp
   // ❌ FORBIDDEN: Reflection to access private security methods
   var method = typeof(SecurityService).GetMethod("ValidateAccess", 
       BindingFlags.NonPublic | BindingFlags.Instance);
   
   // ✅ REQUIRED: Use InternalsVisibleTo for test access
   [assembly: InternalsVisibleTo("ProjectName.Tests")]
   internal bool ValidateAccess(...) // Changed from private to internal
   ```

2. **Cryptographically Secure Random Generation**
   ```csharp
   // ❌ FORBIDDEN: Predictable random generation
   var random = new Random();
   var processId = random.Next(1000, 9999);
   
   // ✅ REQUIRED: Cryptographically secure generation
   using var rng = RandomNumberGenerator.Create();
   var bytes = new byte[4];
   rng.GetBytes(bytes);
   var processId = BitConverter.ToUInt32(bytes, 0);
   ```

3. **Secure COM GUIDs**
   ```csharp
   // ❌ FORBIDDEN: Predictable or weak GUIDs
   [Guid("12345678-1234-1234-1234-123456789012")]
   
   // ✅ REQUIRED: Cryptographically generated GUIDs
   [Guid("57d545f8-98a9-44ac-99ba-571f8b4babeb")]
   ```

### Security Review Checklist

Before submitting any pull request, verify:

- [ ] **No unsafe reflection usage** - All reflection-based access control bypasses removed
- [ ] **Secure random generation** - All random data generation uses cryptographically secure methods
- [ ] **Input validation** - All external inputs have comprehensive validation tests
- [ ] **COM security** - All COM interfaces use secure GUIDs and proper security boundaries
- [ ] **Exception security** - No sensitive information leaked in exception messages
- [ ] **Logging security** - No sensitive data logged at any level
- [ ] **Test data security** - No real passwords, keys, or sensitive data in test code

## Code Quality Standards

### C# Coding Standards

#### General Principles
- **Nullable Reference Types**: `#nullable enable` in all files
- **Code Analysis**: All analyzer warnings must be addressed
- **XML Documentation**: All public APIs must have XML documentation
- **Consistency**: Follow existing codebase patterns and conventions

#### COM Interop Patterns (MANDATORY)

1. **COM Object Lifecycle Management**
   ```csharp
   // ✅ REQUIRED: Proper COM cleanup pattern
   DTE dte = null;
   try
   {
       dte = GetActiveVisualStudioInstance();
       return ProcessVsInstance(dte);
   }
   finally
   {
       if (dte != null)
           Marshal.ReleaseComObject(dte);
   }
   ```

2. **Async COM Operations**
   ```csharp
   // ✅ REQUIRED: COM operations on background thread
   public async Task<BuildResult> BuildSolutionAsync()
   {
       return await Task.Run(() =>
       {
           // COM operations here - EnvDTE is not thread-safe
           return ExecuteComBuildOperation();
       });
   }
   ```

3. **Exception Handling**
   ```csharp
   // ✅ REQUIRED: Wrap COM exceptions with context
   public void SafeComOperation<T>(Func<T> operation, ILogger logger, string operationName)
   {
       try
       {
           return operation();
       }
       catch (COMException comEx)
       {
           logger.LogError(comEx, "COM operation {OperationName} failed", operationName);
           throw new ComInteropException($"COM operation '{operationName}' failed", comEx)
           {
               OperationName = operationName,
               HResult = comEx.HResult
           };
       }
   }
   ```

### Performance Standards

#### Required Performance Characteristics
- **GetRunningInstancesAsync**: Must complete within 1000ms
- **IsConnectionHealthyAsync**: Must complete within 500ms
- **Memory Growth**: Must not exceed 20MB per operation cycle
- **Concurrent Operations**: Must support minimum 50 simultaneous operations

#### Performance Testing in CI/CD
Performance tests are automatically executed in CI/CD pipeline:
- Performance regression blocks merge
- Memory leak detection blocks merge
- Concurrency failures block merge

## Code Review Process

### Pre-Review Checklist

Before requesting code review, ensure:

#### Code Quality
- [ ] All tests pass locally: `dotnet test`
- [ ] Code compiles without warnings: Use MCP tools to verify compilation
- [ ] Performance tests meet thresholds
- [ ] Security tests pass validation
- [ ] Code coverage meets minimum requirements

#### Documentation
- [ ] Public APIs have XML documentation
- [ ] Complex algorithms have explanatory comments
- [ ] Security considerations documented where applicable
- [ ] Performance characteristics documented for new operations

### Code Review Requirements

#### Mandatory Review Elements

1. **Security Review** (REQUIRED for all changes)
   - [ ] No security vulnerabilities introduced
   - [ ] Input validation comprehensive and secure
   - [ ] COM security boundaries properly maintained
   - [ ] No sensitive data exposure in logs or exceptions

2. **Performance Review** (REQUIRED for performance-sensitive changes)
   - [ ] Performance tests validate timing requirements
   - [ ] Memory usage patterns reviewed and approved
   - [ ] Concurrency implications assessed
   - [ ] Resource cleanup patterns verified

3. **Testing Review** (REQUIRED for all changes)
   - [ ] Test coverage meets minimum requirements
   - [ ] Test categories properly assigned
   - [ ] Exception testing uses ExceptionTestHelper
   - [ ] Logger testing uses LoggerTestExtensions
   - [ ] Test data generation uses secure patterns

#### Review Criteria

**Approval Requirements**:
- At least one approving review from code owner
- All automated checks passing (tests, security, performance)
- No unresolved review comments
- Documentation updated where applicable

**Merge Blocking Conditions**:
- Any test failures (unit, integration, security, performance)
- Security vulnerabilities identified
- Performance regression detected
- Code coverage below minimum thresholds
- Unaddressed review feedback

### Quality Gates Integration

#### Automated Quality Gates

1. **Fast Feedback Stage** (0-5 minutes)
   - Unit tests: `dotnet test --filter TestCategory=Unit`
   - Quick syntax and compilation checks

2. **Security Validation Stage** (5-10 minutes)
   - Security tests: `dotnet test --filter TestCategory=Security`
   - Static security analysis

3. **Performance Validation Stage** (10-20 minutes)
   - Performance tests: `dotnet test --filter TestCategory=Performance`
   - Memory leak detection

4. **Full Integration Stage** (20-30 minutes)
   - Complete test suite execution
   - Code coverage analysis
   - Final quality validation

#### Quality Gate Failure Actions
- **Test Failures**: Block merge, require investigation and fixes
- **Security Failures**: Immediate escalation, security team notification
- **Performance Regression**: Block merge, require performance investigation
- **Coverage Regression**: Block merge, require additional test coverage

## Documentation Requirements

### Code Documentation

#### XML Documentation (MANDATORY for Public APIs)
```csharp
/// <summary>
/// Safely executes a COM operation with proper exception handling and resource cleanup.
/// </summary>
/// <typeparam name="T">The return type of the COM operation.</typeparam>
/// <param name="operation">The COM operation to execute.</param>
/// <param name="logger">Logger for operation tracking and error reporting.</param>
/// <param name="operationName">Descriptive name for the operation (used in logging and exceptions).</param>
/// <returns>The result of the COM operation.</returns>
/// <exception cref="ArgumentNullException">Thrown when operation, logger, or operationName is null.</exception>
/// <exception cref="ComInteropException">Thrown when the COM operation fails.</exception>
/// <remarks>
/// This method ensures proper COM object cleanup and provides consistent exception handling
/// for all COM operations. It should be used for all interactions with Visual Studio COM APIs.
/// </remarks>
public static T SafeComOperation<T>(Func<T> operation, ILogger logger, string operationName)
```

#### Security Documentation (REQUIRED for Security-Sensitive Code)
```csharp
/// <summary>
/// Validates user input to prevent injection attacks and ensure data integrity.
/// </summary>
/// <param name="input">User-provided input to validate.</param>
/// <returns>Validated and sanitized input.</returns>
/// <exception cref="SecurityException">Thrown when input contains malicious content.</exception>
/// <security>
/// This method performs comprehensive input validation including:
/// - SQL injection pattern detection
/// - Cross-site scripting (XSS) prevention
/// - Path traversal attack prevention
/// - Command injection prevention
/// - Buffer overflow protection
/// </security>
public string ValidateUserInput(string input)
```

### Pull Request Documentation

#### Required PR Description Elements
```markdown
## Summary
Brief description of changes and their purpose.

## Changes Made
- Detailed list of modifications
- New features or functionality added
- Bug fixes implemented
- Performance improvements made

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Security tests added/updated (if applicable)
- [ ] Performance tests added/updated (if applicable)
- [ ] All tests pass locally

## Security Considerations
- Security implications of changes
- Input validation updates
- Authentication/authorization changes
- COM security boundary modifications

## Performance Impact
- Performance testing results
- Memory usage impact
- Concurrency implications
- Benchmark comparisons (if applicable)

## Documentation Updates
- [ ] XML documentation updated
- [ ] README updated (if applicable)
- [ ] API documentation updated (if applicable)
- [ ] Architecture documentation updated (if applicable)

## Breaking Changes
- List any breaking changes
- Migration guidance for consumers
```

## Release and Deployment

### Version Management
- Follow semantic versioning (MAJOR.MINOR.PATCH)
- Version increments managed through GitHub releases
- Automated CI/CD builds for releases

### Deployment Process
1. All quality gates must pass
2. Full test suite execution required
3. Security validation completed
4. Performance benchmarks validated
5. Documentation updates included

## Getting Help

### Resources
- **Documentation**: `/docs` directory contains comprehensive guides
- **Testing Guide**: `/docs/testing/testing-strategy-guide.md`
- **Security Guide**: `/docs/security/testing-security-best-practices.md`
- **Performance Guide**: `/docs/testing/performance-testing-guide.md`
- **Test Utilities**: `/docs/development/test-utilities-guide.md`

### Contact and Support
- **General Questions**: Create GitHub issue with `question` label
- **Security Concerns**: Create GitHub issue with `security` label
- **Performance Issues**: Create GitHub issue with `performance` label
- **Documentation Issues**: Create GitHub issue with `documentation` label

### Issue Reporting
When reporting issues, include:
- Detailed problem description
- Steps to reproduce
- Expected vs actual behavior
- Environment information (VS version, .NET version, Windows version)
- Relevant log files or error messages

## Conclusion

These contributing guidelines ensure high-quality, secure, and performant code contributions to the Visual Studio MCP Server project. By following these standards, we maintain:

- **Code Quality**: Consistent, maintainable, well-tested code
- **Security**: Proactive security vulnerability prevention
- **Performance**: Production-grade performance characteristics
- **Documentation**: Comprehensive documentation for all features
- **Team Productivity**: Clear processes and shared tooling

Thank you for contributing to the Visual Studio MCP Server project!