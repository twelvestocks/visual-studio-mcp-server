# Security Testing Best Practices

## Overview

This document establishes security testing standards to prevent regression of critical security vulnerabilities identified in the Visual Studio MCP Server project. It provides comprehensive guidance for secure testing patterns and vulnerability prevention.

## Security Testing Philosophy

### Core Security Principles
- **Defense in Depth**: Multiple layers of security validation in testing
- **Least Privilege**: Tests operate with minimal required permissions
- **Secure by Default**: Security measures enabled by default in all test scenarios
- **Vulnerability Prevention**: Proactive testing prevents known vulnerability patterns
- **Security Regression Prevention**: Automated detection of security regressions

### Security Testing Standards
- **Mandatory for All Changes**: Security tests required for all code modifications
- **Automated Validation**: Security tests integrated into CI/CD pipeline
- **Regular Updates**: Security test patterns updated based on threat landscape
- **Documentation**: All security measures documented with rationale

## Critical Security Fixes Documentation

### CRITICAL-001: Unsafe Reflection Access Elimination

#### Vulnerability Description
**Issue**: Tests used reflection to access private methods, bypassing intended access controls
**Risk Level**: HIGH - Reflection-based access control bypasses can introduce security vulnerabilities
**Attack Vector**: Malicious code could use similar patterns to bypass security boundaries

#### Security Fix Implementation
**Before (Vulnerable Pattern)**:
```csharp
// VULNERABLE: Using reflection to bypass access controls
var method = typeof(SecurityValidationService).GetMethod("IsVisualStudioWindow", 
    BindingFlags.NonPublic | BindingFlags.Instance);
var result = method.Invoke(service, new object[] { windowHandle });
```

**After (Secure Pattern)**:
```csharp
// SECURE: Using InternalsVisibleTo for controlled test access
// In AssemblyInfo.cs or .csproj:
[assembly: InternalsVisibleTo("VisualStudioMcp.Imaging.Tests")]

// Test method now accesses internal method directly:
internal bool IsVisualStudioWindow(IntPtr windowHandle) // Changed from private to internal
{
    // Implementation remains the same
}

// Test code:
var result = service.IsVisualStudioWindow(windowHandle); // Direct call, no reflection
```

#### Security Testing Pattern
```csharp
[TestMethod]
[TestCategory(TestCategories.Security)]
public void SecurityMethods_DoNotAllowReflectionAccess_PreventsBypass()
{
    // Arrange: Setup security service
    var service = new SecurityValidationService(Mock.Of<ILogger<SecurityValidationService>>());
    
    // Act & Assert: Verify reflection access is properly restricted
    var type = typeof(SecurityValidationService);
    var privateMethods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
        .Where(m => m.Name.Contains("Security") || m.Name.Contains("Validation"))
        .ToArray();
    
    // Verify no critical security methods are accessible via reflection
    foreach (var method in privateMethods)
    {
        if (method.IsPrivate && IsCriticalSecurityMethod(method.Name))
        {
            Assert.Fail($"Critical security method '{method.Name}' should not be private if accessed by tests. " +
                       "Use 'internal' visibility with InternalsVisibleTo instead.");
        }
    }
}

private bool IsCriticalSecurityMethod(string methodName)
{
    var criticalPatterns = new[] { "IsVisualStudioWindow", "ValidateAccess", "CheckPermissions", "Authorize" };
    return criticalPatterns.Any(pattern => methodName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
}
```

### CRITICAL-002: Cryptographically Secure Random Process IDs

#### Vulnerability Description
**Issue**: Test process IDs were generated using predictable patterns
**Risk Level**: MEDIUM - Predictable IDs could be exploited for process enumeration attacks
**Attack Vector**: Malicious code could predict test process IDs and interfere with testing

#### Security Fix Implementation
**Before (Vulnerable Pattern)**:
```csharp
// VULNERABLE: Predictable process ID generation
private static uint[] GenerateTestProcessIds(int count)
{
    var processIds = new uint[count];
    for (int i = 0; i < count; i++)
    {
        processIds[i] = (uint)(1000 + i * 100); // Predictable sequence
    }
    return processIds;
}
```

**After (Secure Pattern)**:
```csharp
// SECURE: Cryptographically secure random generation
private static uint[] GenerateSecureRandomProcessIds(int count, uint minValue, uint maxValue)
{
    using var rng = RandomNumberGenerator.Create();
    var processIds = new uint[count];
    var range = maxValue - minValue;
    
    for (int i = 0; i < count; i++)
    {
        var randomBytes = new byte[4];
        rng.GetBytes(randomBytes);
        var randomValue = BitConverter.ToUInt32(randomBytes, 0);
        processIds[i] = (randomValue % range) + minValue;
    }
    
    return processIds;
}
```

#### Security Testing Pattern
```csharp
[TestMethod]
[TestCategory(TestCategories.Security)]
[TestCategory(TestCategories.Performance)]
public void ProcessIdGeneration_UsesSecureRandom_PreventsPrediction()
{
    // Arrange: Generate multiple sets of process IDs
    const int setCount = 10;
    const int idsPerSet = 100;
    var allIds = new List<uint>();
    
    // Act: Generate multiple sets and collect all IDs
    for (int i = 0; i < setCount; i++)
    {
        var ids = ProcessMockProvider.GenerateSecureRandomProcessIds(idsPerSet, 1000, 65000);
        allIds.AddRange(ids);
    }
    
    // Assert: Verify randomness characteristics
    var uniqueIds = allIds.Distinct().Count();
    var totalIds = allIds.Count;
    
    // At least 95% of IDs should be unique (allowing for some collision in random generation)
    var uniquenessRatio = (double)uniqueIds / totalIds;
    Assert.IsTrue(uniquenessRatio > 0.95, 
        $"Uniqueness ratio {uniquenessRatio:P2} indicates insufficient randomness");
    
    // Verify no sequential patterns
    var sequentialPairs = 0;
    for (int i = 1; i < allIds.Count; i++)
    {
        if (allIds[i] == allIds[i - 1] + 1)
            sequentialPairs++;
    }
    
    var sequentialRatio = (double)sequentialPairs / (allIds.Count - 1);
    Assert.IsTrue(sequentialRatio < 0.01, 
        $"Sequential pattern ratio {sequentialRatio:P2} indicates predictable generation");
}
```

### CRITICAL-003: Secure COM GUID Generation

#### Vulnerability Description
**Issue**: Test COM interfaces used weak, predictable GUIDs
**Risk Level**: MEDIUM - Predictable GUIDs could allow COM interface hijacking
**Attack Vector**: Malicious code could register with predictable GUIDs and intercept COM calls

#### Security Fix Implementation
**Before (Vulnerable Pattern)**:
```csharp
// VULNERABLE: Weak, predictable GUID
[ComVisible(true)]
[Guid("12345678-1234-1234-1234-123456789012")] // Predictable pattern
public interface ITestComInterface
{
    void TestMethod();
}
```

**After (Secure Pattern)**:
```csharp
// SECURE: Cryptographically generated GUID
[ComVisible(true)]
[Guid("57d545f8-98a9-44ac-99ba-571f8b4babeb")] // Cryptographically secure GUID
public interface ITestComInterface
{
    void TestMethod();
}
```

#### Security Testing Pattern
```csharp
[TestMethod]
[TestCategory(TestCategories.Security)]
[TestCategory(TestCategories.ComInterop)]
public void ComInterfaces_UseSecureGuids_PreventHijacking()
{
    // Arrange: Get all COM-visible interfaces in test assemblies
    var testAssemblies = AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => a.GetName().Name?.Contains("Test") == true);
    
    var weakGuids = new[]
    {
        "00000000-0000-0000-0000-000000000000", // Null GUID
        "12345678-1234-1234-1234-123456789012", // Sequential pattern
        "11111111-1111-1111-1111-111111111111", // Repeated pattern
        "AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"  // Repeated hex pattern
    };
    
    foreach (var assembly in testAssemblies)
    {
        var comInterfaces = assembly.GetTypes()
            .Where(t => t.IsInterface && t.IsComVisible())
            .ToArray();
            
        foreach (var comInterface in comInterfaces)
        {
            var guidAttribute = comInterface.GetCustomAttribute<GuidAttribute>();
            if (guidAttribute != null)
            {
                // Assert: Verify GUID is not weak/predictable
                Assert.IsFalse(weakGuids.Contains(guidAttribute.Value.ToUpperInvariant()),
                    $"COM interface '{comInterface.Name}' uses weak GUID '{guidAttribute.Value}'. " +
                    "Use cryptographically generated GUIDs for COM interfaces.");
                
                // Verify GUID is properly formatted
                Assert.IsTrue(Guid.TryParse(guidAttribute.Value, out _),
                    $"COM interface '{comInterface.Name}' has invalid GUID format '{guidAttribute.Value}'");
            }
        }
    }
}
```

## Secure Testing Patterns

### Pattern 1: Controlled Test Access with InternalsVisibleTo

#### When to Use
- Testing internal methods that should not be publicly accessible
- Validating internal security boundaries
- Testing security-critical internal logic

#### Implementation
```csharp
// In production assembly (.csproj):
<PropertyGroup>
  <AssemblyMetadata Include="InternalsVisibleTo" Content="$(AssemblyName).Tests" />
</PropertyGroup>

// Or in AssemblyInfo.cs:
[assembly: InternalsVisibleTo("VisualStudioMcp.Core.Tests")]
[assembly: InternalsVisibleTo("VisualStudioMcp.Security.Tests")]
```

#### Security Considerations
- **Limit Scope**: Only specify required test assemblies
- **Review Access**: Regular review of InternalsVisibleTo declarations
- **Documentation**: Document why internal access is required
- **Alternative Check**: Consider if public API testing is sufficient

### Pattern 2: Cryptographic Randomness in Test Scenarios

#### When to Use
- Generating test data that simulates real-world unpredictability
- Testing security boundaries with random inputs
- Validating random number usage in production code

#### Implementation
```csharp
public static class SecureTestDataGenerator
{
    private static readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();
    
    public static string GenerateSecureTestString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var bytes = new byte[length];
        _rng.GetBytes(bytes);
        
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }
    
    public static uint GenerateSecureTestId(uint minValue = 1000, uint maxValue = uint.MaxValue)
    {
        var range = maxValue - minValue;
        var bytes = new byte[4];
        _rng.GetBytes(bytes);
        var value = BitConverter.ToUInt32(bytes, 0);
        return (value % range) + minValue;
    }
    
    public static Guid GenerateSecureTestGuid()
    {
        var bytes = new byte[16];
        _rng.GetBytes(bytes);
        return new Guid(bytes);
    }
}
```

### Pattern 3: Input Validation Security Testing

#### When to Use
- Testing all external input validation
- Validating security boundaries
- Testing injection prevention

#### Implementation
```csharp
[TestMethod]
[TestCategory(TestCategories.Security)]
[TestCategory(TestCategories.Unit)]
public void ValidateInput_WithMaliciousInput_RejectsSecurely()
{
    // Arrange: Prepare malicious input patterns
    var maliciousInputs = new[]
    {
        // SQL Injection patterns
        "'; DROP TABLE Users; --",
        "1' OR '1'='1",
        
        // Script injection patterns  
        "<script>alert('xss')</script>",
        "javascript:alert('xss')",
        
        // Path traversal patterns
        "../../../etc/passwd",
        "..\\..\\..\\windows\\system32\\config\\sam",
        
        // Command injection patterns
        "; rm -rf /",
        "| net user hacker password /add",
        
        // Null/empty patterns
        null,
        string.Empty,
        "\0\0\0",
        
        // Unicode/encoding attacks
        "\u0000",
        "%00",
        "%2e%2e%2f",
        
        // Buffer overflow patterns
        new string('A', 10000),
        new string('X', 100000)
    };
    
    var service = new InputValidationService(Mock.Of<ILogger<InputValidationService>>());
    
    // Act & Assert: Verify all malicious inputs are rejected
    foreach (var maliciousInput in maliciousInputs)
    {
        var exception = ExceptionTestHelper.AssertThrows<SecurityException>(
            () => service.ValidateUserInput(maliciousInput),
            $"Malicious input was not rejected: '{maliciousInput}'");
            
        // Verify exception contains security context
        Assert.IsTrue(exception.Message.Contains("security", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(exception.Message.Contains("validation", StringComparison.OrdinalIgnoreCase));
    }
}
```

### Pattern 4: COM Security Boundary Testing

#### When to Use
- Testing COM object security interfaces
- Validating COM marshalling security
- Testing COM authentication and authorisation

#### Implementation
```csharp
[TestMethod]
[TestCategory(TestCategories.Security)]
[TestCategory(TestCategories.ComInterop)]
public void ComInterface_WithoutValidCredentials_DeniesAccess()
{
    // Arrange: Setup COM object without valid credentials
    var mockLogger = new Mock<ILogger<ComSecurityService>>();
    var service = new ComSecurityService(mockLogger.Object);
    
    // Act & Assert: Verify access denied without proper authentication
    var exception = ExceptionTestHelper.AssertThrows<UnauthorizedAccessException>(
        () => service.AccessProtectedComMethod("unauthorized_user", "invalid_token"));
    
    // Verify security logging
    mockLogger.VerifyLogMessageWithException(LogLevel.Warning, 
        "Unauthorized COM access attempt", typeof(UnauthorizedAccessException));
    
    // Verify no sensitive information leaked in exception
    Assert.IsFalse(exception.Message.Contains("password", StringComparison.OrdinalIgnoreCase));
    Assert.IsFalse(exception.Message.Contains("secret", StringComparison.OrdinalIgnoreCase));
    Assert.IsFalse(exception.Message.Contains("token", StringComparison.OrdinalIgnoreCase));
}
```

## Security Regression Prevention

### Automated Security Validation in CI/CD

#### Security Test Gate Configuration
```yaml
# Security test stage (must pass before merge)
security_tests:
  stage: security_validation
  script:
    - dotnet test --filter TestCategory=Security --logger "trx;LogFileName=security-results.trx"
  artifacts:
    reports:
      junit: "**/security-results.trx"
  rules:
    - if: '$CI_PIPELINE_SOURCE == "merge_request_event"'
  allow_failure: false  # Block merge on security test failures
```

#### Security Test Categories for CI/CD
```bash
# Run all security tests
dotnet test --filter TestCategory=Security

# Run specific security test types
dotnet test --filter "TestCategory=Security&TestCategory=ComInterop"     # COM security
dotnet test --filter "TestCategory=Security&TestCategory=McpProtocol"    # Protocol security  
dotnet test --filter "TestCategory=Security&TestCategory=Unit"           # Unit security tests
```

### Security Code Review Checklist

#### Pre-Merge Security Validation
- [ ] **No Reflection Access**: Verify no new reflection-based access control bypasses
- [ ] **Secure Random Usage**: Confirm cryptographically secure random generation where required
- [ ] **Input Validation**: All external inputs have security validation tests
- [ ] **COM Security**: COM interfaces use secure GUIDs and proper security boundaries
- [ ] **Exception Security**: No sensitive information leaked in exception messages
- [ ] **Logging Security**: No sensitive data logged at any level
- [ ] **Test Data Security**: No real passwords, keys, or sensitive data in test code

#### Security Test Coverage Requirements
- [ ] **Input Validation Coverage**: 100% of public API methods have input validation tests
- [ ] **Security Boundary Coverage**: All security boundaries have negative testing
- [ ] **COM Security Coverage**: All COM interfaces have security validation tests
- [ ] **Authentication Coverage**: All authentication paths have security tests
- [ ] **Authorization Coverage**: All authorization checks have comprehensive tests

### Vulnerability Prevention Guide

#### Common Security Anti-Patterns in Testing

##### Anti-Pattern 1: Hardcoded Credentials
```csharp
// AVOID: Hardcoded credentials in tests
[TestMethod]
public void Login_WithValidCredentials_Succeeds()
{
    var result = authService.Login("admin", "password123"); // SECURITY RISK
}

// SECURE: Use secure test credential generation
[TestMethod]
public void Login_WithValidCredentials_Succeeds()
{
    var testCredentials = SecureTestDataGenerator.GenerateTestCredentials();
    var result = authService.Login(testCredentials.Username, testCredentials.Password);
}
```

##### Anti-Pattern 2: Sensitive Data in Test Output
```csharp
// AVOID: Sensitive data in assertions or logs
[TestMethod]
public void ProcessApiKey_WithValidKey_ReturnsSuccess()
{
    var apiKey = "sk-1234567890abcdef"; // Real API key pattern
    var result = apiService.ProcessApiKey(apiKey);
    
    // SECURITY RISK: API key may appear in test output
    Assert.AreEqual($"Processed key: {apiKey}", result.Message);
}

// SECURE: Mask sensitive data in test validation
[TestMethod]  
public void ProcessApiKey_WithValidKey_ReturnsSuccess()
{
    var apiKey = SecureTestDataGenerator.GenerateTestApiKey();
    var result = apiService.ProcessApiKey(apiKey);
    
    // Validate success without exposing sensitive data
    Assert.IsTrue(result.Success);
    Assert.IsTrue(result.Message.Contains("Processed key: ***"));
}
```

##### Anti-Pattern 3: Insufficient Security Boundary Testing
```csharp
// AVOID: Only testing positive security scenarios
[TestMethod]
public void AuthorizeUser_WithValidPermissions_Succeeds()
{
    var result = authService.AuthorizeUser("validUser", "read");
    Assert.IsTrue(result.Success);
}

// SECURE: Test both positive and negative security scenarios
[TestMethod]
[TestCategory(TestCategories.Security)]
public void AuthorizeUser_Security_BoundaryTesting()
{
    // Test positive scenario
    var validResult = authService.AuthorizeUser("validUser", "read");
    Assert.IsTrue(validResult.Success);
    
    // Test negative scenarios
    ExceptionTestHelper.AssertThrows<UnauthorizedAccessException>(
        () => authService.AuthorizeUser("invalidUser", "read"));
        
    ExceptionTestHelper.AssertThrows<UnauthorizedAccessException>(
        () => authService.AuthorizeUser("validUser", "admin"));
        
    ExceptionTestHelper.AssertThrows<ArgumentException>(
        () => authService.AuthorizeUser(null, "read"));
        
    ExceptionTestHelper.AssertThrows<ArgumentException>(
        () => authService.AuthorizeUser("validUser", null));
}
```

## Security Exception Testing Patterns

### Pattern 1: Comprehensive Security Exception Validation
```csharp
[TestMethod]
[TestCategory(TestCategories.Security)]
public void SecurityMethod_WithInvalidInput_ThrowsProperSecurityException()
{
    // Arrange
    var service = new SecurityService(Mock.Of<ILogger<SecurityService>>());
    var maliciousInput = "<script>alert('xss')</script>";
    
    // Act & Assert: Use ExceptionTestHelper for consistent validation
    var exception = ExceptionTestHelper.AssertThrowsWithValidation<SecurityException>(
        () => service.ProcessUserInput(maliciousInput),
        ex => {
            // Validate exception properties
            Assert.AreEqual("SECURITY_VALIDATION_FAILED", ex.ErrorCode);
            Assert.IsTrue(ex.Message.Contains("Input validation failed"));
            Assert.IsFalse(ex.Message.Contains(maliciousInput)); // No sensitive data leaked
            
            // Validate security context
            Assert.IsNotNull(ex.Data["SecurityContext"]);
            Assert.AreEqual("InputValidation", ex.Data["SecurityContext"]);
        });
}
```

### Pattern 2: Security Logging Validation
```csharp
[TestMethod]
[TestCategory(TestCategories.Security)]
public void SecurityViolation_LogsAppropriately_WithoutSensitiveData()
{
    // Arrange
    var mockLogger = new Mock<ILogger<SecurityService>>();
    var service = new SecurityService(mockLogger.Object);
    var sensitiveInput = "user:password@server/database";
    
    // Act: Trigger security violation
    try
    {
        service.ProcessDatabaseConnection(sensitiveInput);
    }
    catch (SecurityException)
    {
        // Expected security exception
    }
    
    // Assert: Verify appropriate logging without sensitive data
    mockLogger.ShouldHaveLogged()
        .AtLevel(LogLevel.Warning)
        .WithMessage("Security violation detected")
        .WithException<SecurityException>()
        .Verify();
    
    // Verify no sensitive data in logs
    mockLogger.Verify(x => x.Log(
        It.IsAny<LogLevel>(),
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => !v.ToString()!.Contains("password")),
        It.IsAny<Exception?>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()), 
        Times.AtLeastOnce());
}
```

## Security Performance Testing

### Pattern 1: Security Operation Performance
```csharp
[TestMethod]
[TestCategory(TestCategories.Security)]
[TestCategory(TestCategories.Performance)]
public void SecurityValidation_PerformanceRequirements_MeetThresholds()
{
    // Arrange: Setup security validation with realistic load
    var service = new SecurityValidationService(Mock.Of<ILogger<SecurityValidationService>>());
    var testInputs = Enumerable.Range(0, 1000)
        .Select(_ => SecureTestDataGenerator.GenerateSecureTestString(100))
        .ToArray();
    
    // Act: Measure security validation performance
    var stopwatch = Stopwatch.StartNew();
    
    foreach (var input in testInputs)
    {
        var result = service.ValidateInput(input);
        Assert.IsTrue(result.IsValid);
    }
    
    stopwatch.Stop();
    
    // Assert: Verify performance thresholds
    var averageTimePerValidation = stopwatch.ElapsedMilliseconds / (double)testInputs.Length;
    
    Assert.IsTrue(averageTimePerValidation < 10, // 10ms per validation maximum
        $"Security validation too slow: {averageTimePerValidation:F2}ms average");
    
    Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, // 5 second total maximum
        $"Total security validation time exceeded: {stopwatch.ElapsedMilliseconds}ms");
}
```

### Pattern 2: Concurrent Security Testing
```csharp
[TestMethod]
[TestCategory(TestCategories.Security)]
[TestCategory(TestCategories.Concurrency)]
public async Task SecurityService_ConcurrentAccess_MaintainsSecurityBoundaries()
{
    // Arrange: Setup concurrent security operations
    var service = new SecurityService(Mock.Of<ILogger<SecurityService>>());
    var authorizedUser = "authorizedUser";
    var unauthorizedUser = "unauthorizedUser";
    
    // Act: Execute concurrent operations with mixed authorization
    var tasks = new List<Task>();
    
    // Authorized operations (should succeed)
    for (int i = 0; i < 25; i++)
    {
        tasks.Add(Task.Run(() => {
            var result = service.AuthorizeOperation(authorizedUser, $"operation_{i}");
            Assert.IsTrue(result.Success);
        }));
    }
    
    // Unauthorized operations (should fail)
    for (int i = 0; i < 25; i++)
    {
        tasks.Add(Task.Run(() => {
            ExceptionTestHelper.AssertThrows<UnauthorizedAccessException>(
                () => service.AuthorizeOperation(unauthorizedUser, $"operation_{i}"));
        }));
    }
    
    // Assert: All operations complete with correct security behavior
    await Task.WhenAll(tasks);
    
    // Verify no security boundary violations occurred during concurrent access
    // (This would be detected by the individual assertions in each task)
}
```

## Conclusion

These security testing best practices provide comprehensive protection against security vulnerabilities and regressions. Key principles include:

- **Proactive Security Testing**: Security validation integrated throughout development
- **Vulnerability Prevention**: Specific patterns to prevent known vulnerability types
- **Automated Security Gates**: CI/CD integration ensures consistent security validation
- **Comprehensive Coverage**: Security testing covers all attack vectors and boundaries
- **Continuous Improvement**: Regular updates based on evolving security threats

Regular application of these practices ensures the Visual Studio MCP Server maintains robust security throughout its development lifecycle.