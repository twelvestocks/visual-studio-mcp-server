using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using VisualStudioMcp.Core;

namespace VisualStudioMcp.Core.Tests;

[TestClass]
public class ComInteropHelperTests
{
    private Mock<ILogger> _mockLogger = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
    }

    [TestMethod]
    public void SafeComOperation_WithSuccessfulOperation_ReturnsResult()
    {
        // Arrange
        const string expectedResult = "Success";
        var operation = () => expectedResult;

        // Act
        var result = ComInteropHelper.SafeComOperation(operation, _mockLogger.Object, "TestOperation");

        // Assert
        Assert.AreEqual(expectedResult, result);
        VerifyLogCall("Starting COM operation: TestOperation", LogLevel.Debug);
        VerifyLogCall("Completed COM operation: TestOperation", LogLevel.Debug);
    }

    [TestMethod]
    public void SafeComOperation_WithCOMException_ThrowsComInteropException()
    {
        // Arrange
        var comException = new COMException("Test COM error", unchecked((int)0x80004005));
        Func<string> operation = () => throw comException;

        // Act & Assert
        try
        {
            ComInteropHelper.SafeComOperation(operation, _mockLogger.Object, "TestOperation");
            Assert.Fail("Expected ComInteropException was not thrown");
        }
        catch (ComInteropException exception)
        {
            Assert.IsTrue(exception.Message.Contains("TestOperation"));
            Assert.AreEqual(comException, exception.InnerException);
            VerifyLogCall("COM exception in operation TestOperation", LogLevel.Error);
        }
    }

    [TestMethod]
    public void SafeComOperation_WithGeneralException_RethrowsException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test error");
        Func<string> operation = () => throw expectedException;

        // Act & Assert
        try
        {
            ComInteropHelper.SafeComOperation(operation, _mockLogger.Object, "TestOperation");
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException exception)
        {
            Assert.AreEqual(expectedException, exception);
            VerifyLogCall("Unexpected exception in COM operation TestOperation", LogLevel.Error);
        }
    }

    [TestMethod]
    public void SafeComOperationVoid_WithSuccessfulOperation_CompletesSuccessfully()
    {
        // Arrange
        var executed = false;
        Action operation = () => executed = true;

        // Act
        ComInteropHelper.SafeComOperation(operation, _mockLogger.Object, "TestOperation");

        // Assert
        Assert.IsTrue(executed);
        VerifyLogCall("Starting COM operation: TestOperation", LogLevel.Debug);
        VerifyLogCall("Completed COM operation: TestOperation", LogLevel.Debug);
    }

    [TestMethod]
    public void SafeReleaseComObject_WithValidComObject_ReleasesSuccessfully()
    {
        // Arrange
        var comObject = Marshal.GetObjectForIUnknown(Marshal.GetIUnknownForObject(new object()));

        // Act
        ComInteropHelper.SafeReleaseComObject(comObject, _mockLogger.Object, "TestObject");

        // Assert
        VerifyLogCall("Released COM object TestObject", LogLevel.Debug);
    }

    [TestMethod]
    public void SafeReleaseComObject_WithNullObject_HandlesGracefully()
    {
        // Act
        ComInteropHelper.SafeReleaseComObject(null, _mockLogger.Object, "TestObject");

        // Assert - Should not throw and not log anything
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [TestMethod]
    public void SafeReleaseComObject_WithNonComObject_HandlesGracefully()
    {
        // Arrange
        var nonComObject = "Not a COM object";

        // Act
        ComInteropHelper.SafeReleaseComObject(nonComObject, _mockLogger.Object, "TestObject");

        // Assert
        VerifyLogCall("Object TestObject is not a COM object", LogLevel.Debug);
    }

    [TestMethod]
    public void WithComObject_WithSuccessfulOperation_ReturnsResultAndCleanup()
    {
        // Arrange
        var testObject = new TestComObject();
        const string expectedResult = "Success";
        Func<TestComObject> getObject = () => testObject;
        Func<TestComObject, string> operation = obj => expectedResult;

        // Act
        var result = ComInteropHelper.WithComObject(getObject, operation, _mockLogger.Object, "TestOperation");

        // Assert
        Assert.AreEqual(expectedResult, result);
    }

    [TestMethod]
    public void WithComObjectVoid_WithSuccessfulOperation_CompletesAndCleanup()
    {
        // Arrange
        var testObject = new TestComObject();
        var executed = false;
        Func<TestComObject> getObject = () => testObject;
        Action<TestComObject> operation = obj => executed = true;

        // Act
        ComInteropHelper.WithComObject(getObject, operation, _mockLogger.Object, "TestOperation");

        // Assert
        Assert.IsTrue(executed);
    }

    [TestMethod]
    public void IsRetryableComError_WithRetryableErrors_ReturnsTrue()
    {
        // Arrange & Act & Assert
        Assert.IsTrue(ComInteropHelper.IsRetryableComError(unchecked((int)0x800401E3))); // MK_E_UNAVAILABLE
        Assert.IsTrue(ComInteropHelper.IsRetryableComError(unchecked((int)0x80010001))); // RPC_E_CALL_REJECTED
        Assert.IsTrue(ComInteropHelper.IsRetryableComError(unchecked((int)0x80010105))); // RPC_E_SERVERCALL_RETRYLATER
        Assert.IsTrue(ComInteropHelper.IsRetryableComError(unchecked((int)0x8001010A))); // RPC_E_SERVERCALL_REJECTED
        Assert.IsTrue(ComInteropHelper.IsRetryableComError(unchecked((int)0x80070005))); // E_ACCESSDENIED
        Assert.IsTrue(ComInteropHelper.IsRetryableComError(unchecked((int)0x80004005))); // E_FAIL
    }

    [TestMethod]
    public void IsRetryableComError_WithNonRetryableError_ReturnsFalse()
    {
        // Arrange & Act & Assert
        Assert.IsFalse(ComInteropHelper.IsRetryableComError(unchecked((int)0x80040154))); // CLASS_E_CLASSNOTREGISTERED
        Assert.IsFalse(ComInteropHelper.IsRetryableComError(0)); // S_OK
        Assert.IsFalse(ComInteropHelper.IsRetryableComError(-1)); // Random error
    }

    [TestMethod]
    public void GetComErrorDescription_WithKnownErrors_ReturnsDescriptiveMessage()
    {
        // Arrange & Act & Assert
        Assert.AreEqual("Visual Studio instance is temporarily unavailable", 
            ComInteropHelper.GetComErrorDescription(unchecked((int)0x800401E3)));
        Assert.AreEqual("Visual Studio rejected the call (may be busy)", 
            ComInteropHelper.GetComErrorDescription(unchecked((int)0x80010001)));
        Assert.AreEqual("Visual Studio COM class not registered", 
            ComInteropHelper.GetComErrorDescription(unchecked((int)0x80040154)));
    }

    [TestMethod]
    public void GetComErrorDescription_WithUnknownError_ReturnsGenericMessage()
    {
        // Arrange & Act
        var result = ComInteropHelper.GetComErrorDescription(0x12345678);

        // Assert
        Assert.AreEqual("COM error with HRESULT 0x12345678", result);
    }

    [TestMethod]
    public async Task RetryComOperationAsync_WithSuccessfulOperation_ReturnsResult()
    {
        // Arrange
        const string expectedResult = "Success";
        var operation = () => expectedResult;

        // Act
        var result = await ComInteropHelper.RetryComOperationAsync(operation, _mockLogger.Object, "TestOperation");

        // Assert
        Assert.AreEqual(expectedResult, result);
    }

    [TestMethod]
    public async Task RetryComOperationAsync_WithRetryableErrorThenSuccess_RetriesAndSucceeds()
    {
        // Arrange
        var attemptCount = 0;
        const string expectedResult = "Success";
        Func<string> operation = () =>
        {
            attemptCount++;
            if (attemptCount == 1)
            {
                throw new ComInteropException("Retry needed", new COMException("Test", unchecked((int)0x80010001)));
            }
            return expectedResult;
        };

        // Act
        var result = await ComInteropHelper.RetryComOperationAsync(operation, _mockLogger.Object, "TestOperation", maxRetries: 2, retryDelay: 10);

        // Assert
        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(2, attemptCount);
        VerifyLogCall("COM operation TestOperation failed with retryable error", LogLevel.Warning);
    }

    [TestMethod]
    public async Task RetryComOperationAsync_WithNonRetryableError_ThrowsImmediately()
    {
        // Arrange
        var comException = new ComInteropException("Non-retryable", new COMException("Test", unchecked((int)0x80040154)));
        Func<string> operation = () => throw comException;

        // Act & Assert
        try
        {
            await ComInteropHelper.RetryComOperationAsync(operation, _mockLogger.Object, "TestOperation");
            Assert.Fail("Expected ComInteropException was not thrown");
        }
        catch (ComInteropException exception)
        {
            Assert.AreEqual(comException, exception);
        }
    }

    [TestMethod]
    public async Task SafeComOperationWithTimeoutAsync_WithSuccessfulOperation_ReturnsResult()
    {
        // Arrange
        const string expectedResult = "Success";
        var operation = () => expectedResult;

        // Act
        var result = await ComInteropHelper.SafeComOperationWithTimeoutAsync(operation, _mockLogger.Object, "TestOperation", 5000);

        // Assert
        Assert.AreEqual(expectedResult, result);
    }

    [TestMethod]
    public async Task SafeComOperationWithTimeoutAsync_WithSlowOperation_ThrowsTimeoutException()
    {
        // Arrange
        Func<string> operation = () =>
        {
            Thread.Sleep(1000);
            return "Never reached";
        };

        // Act & Assert
        try
        {
            await ComInteropHelper.SafeComOperationWithTimeoutAsync(operation, _mockLogger.Object, "TestOperation", 100);
            Assert.Fail("Expected ComInteropException was not thrown");
        }
        catch (ComInteropException exception)
        {
            Assert.IsTrue(exception.Message.Contains("timed out"));
            VerifyLogCall("COM operation TestOperation timed out after 100ms", LogLevel.Error);
        }
    }

    [TestMethod]
    public async Task WithComObjectTimeoutAsync_WithSuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var testObject = new TestComObject();
        const string expectedResult = "Success";
        Func<TestComObject> getObject = () => testObject;
        Func<TestComObject, string> operation = obj => expectedResult;

        // Act
        var result = await ComInteropHelper.WithComObjectTimeoutAsync(getObject, operation, _mockLogger.Object, "TestOperation", 5000);

        // Assert
        Assert.AreEqual(expectedResult, result);
    }

    private void VerifyLogCall(string expectedMessage, LogLevel expectedLevel)
    {
        _mockLogger.Verify(
            x => x.Log(
                expectedLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private class TestComObject
    {
        // Simple test class to use in COM operations
    }
}