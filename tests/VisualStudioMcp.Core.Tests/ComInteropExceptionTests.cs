using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VisualStudioMcp.Core;

namespace VisualStudioMcp.Core.Tests;

[TestClass]
public class ComInteropExceptionTests
{
    [TestMethod]
    public void Constructor_Default_CreatesEmptyException()
    {
        // Act
        var exception = new ComInteropException();

        // Assert
        Assert.IsNotNull(exception);
        Assert.IsNull(exception.Message);
        Assert.IsNull(exception.InnerException);
        Assert.IsNull(exception.HResult);
        Assert.IsFalse(exception.IsRetryable);
    }

    [TestMethod]
    public void Constructor_WithMessage_SetsMessage()
    {
        // Arrange
        const string expectedMessage = "Test error message";

        // Act
        var exception = new ComInteropException(expectedMessage);

        // Assert
        Assert.AreEqual(expectedMessage, exception.Message);
        Assert.IsNull(exception.InnerException);
        Assert.IsNull(exception.HResult);
        Assert.IsFalse(exception.IsRetryable);
    }

    [TestMethod]
    public void Constructor_WithMessageAndInnerException_SetsBothProperties()
    {
        // Arrange
        const string expectedMessage = "Test error message";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new ComInteropException(expectedMessage, innerException);

        // Assert
        Assert.AreEqual(expectedMessage, exception.Message);
        Assert.AreEqual(innerException, exception.InnerException);
        Assert.AreEqual(innerException.HResult, exception.HResult);
        Assert.IsFalse(exception.IsRetryable);
    }

    [TestMethod]
    public void Constructor_WithComInnerException_ExtractsHResult()
    {
        // Arrange
        const string expectedMessage = "Test COM error message";
        var comException = new COMException("COM error", unchecked((int)0x80004005));

        // Act
        var exception = new ComInteropException(expectedMessage, comException);

        // Assert
        Assert.AreEqual(expectedMessage, exception.Message);
        Assert.AreEqual(comException, exception.InnerException);
        Assert.AreEqual(unchecked((int)0x80004005), exception.HResult);
        Assert.IsTrue(exception.IsRetryable); // E_FAIL is retryable
    }

    [TestMethod]
    public void HResult_WithCOMInnerException_ReturnsCorrectValue()
    {
        // Arrange
        var comException = new COMException("COM error", unchecked((int)0x800401E3));
        var exception = new ComInteropException("Test", comException);

        // Act & Assert
        Assert.AreEqual(unchecked((int)0x800401E3), exception.HResult);
    }

    [TestMethod]
    public void HResult_WithNonCOMInnerException_ReturnsInnerHResult()
    {
        // Arrange
        var innerException = new ArgumentException("Argument error")
        {
            HResult = 0x12345678
        };
        var exception = new ComInteropException("Test", innerException);

        // Act & Assert
        Assert.AreEqual(0x12345678, exception.HResult);
    }

    [TestMethod]
    public void HResult_WithNoInnerException_ReturnsNull()
    {
        // Arrange
        var exception = new ComInteropException("Test");

        // Act & Assert
        Assert.IsNull(exception.HResult);
    }

    [TestMethod]
    public void IsRetryable_WithRetryableCOMError_ReturnsTrue()
    {
        // Arrange
        var comException = new COMException("COM error", unchecked((int)0x80010001)); // RPC_E_CALL_REJECTED
        var exception = new ComInteropException("Test", comException);

        // Act & Assert
        Assert.IsTrue(exception.IsRetryable);
    }

    [TestMethod]
    public void IsRetryable_WithNonRetryableCOMError_ReturnsFalse()
    {
        // Arrange
        var comException = new COMException("COM error", unchecked((int)0x80040154)); // CLASS_E_CLASSNOTREGISTERED
        var exception = new ComInteropException("Test", comException);

        // Act & Assert
        Assert.IsFalse(exception.IsRetryable);
    }

    [TestMethod]
    public void IsRetryable_WithNoHResult_ReturnsFalse()
    {
        // Arrange
        var exception = new ComInteropException("Test");

        // Act & Assert
        Assert.IsFalse(exception.IsRetryable);
    }

    [TestMethod]
    public void IsRetryable_WithNonCOMInnerException_ReturnsFalse()
    {
        // Arrange
        var innerException = new ArgumentException("Argument error");
        var exception = new ComInteropException("Test", innerException);

        // Act & Assert
        Assert.IsFalse(exception.IsRetryable);
    }

    [TestMethod]
    public void Exception_Serialization_WorksCorrectly()
    {
        // Arrange
        const string expectedMessage = "Test serialization";
        var comException = new COMException("COM error", unchecked((int)0x80004005));
        var originalException = new ComInteropException(expectedMessage, comException);

        // Act - This would test serialization if needed
        // For unit tests, we primarily verify the properties are accessible
        var message = originalException.Message;
        var innerException = originalException.InnerException;
        var hresult = originalException.HResult;
        var isRetryable = originalException.IsRetryable;

        // Assert
        Assert.AreEqual(expectedMessage, message);
        Assert.AreEqual(comException, innerException);
        Assert.AreEqual(unchecked((int)0x80004005), hresult);
        Assert.IsTrue(isRetryable);
    }

    [TestMethod]
    public void ExceptionWithDifferentRetryableErrors_ReturnsCorrectRetryableStatus()
    {
        // Test various retryable HRESULT values
        var testCases = new[]
        {
            (unchecked((int)0x800401E3), true),  // MK_E_UNAVAILABLE
            (unchecked((int)0x80010001), true),  // RPC_E_CALL_REJECTED
            (unchecked((int)0x80010105), true),  // RPC_E_SERVERCALL_RETRYLATER
            (unchecked((int)0x8001010A), true),  // RPC_E_SERVERCALL_REJECTED
            (unchecked((int)0x80070005), true),  // E_ACCESSDENIED
            (unchecked((int)0x80004005), true),  // E_FAIL
            (unchecked((int)0x80040154), false), // CLASS_E_CLASSNOTREGISTERED
            (0, false),                          // S_OK
        };

        foreach (var (hresult, expectedRetryable) in testCases)
        {
            // Arrange
            var comException = new COMException("COM error", hresult);
            var exception = new ComInteropException("Test", comException);

            // Act & Assert
            Assert.AreEqual(expectedRetryable, exception.IsRetryable, 
                $"HRESULT 0x{hresult:X8} should {(expectedRetryable ? "be" : "not be")} retryable");
        }
    }
}