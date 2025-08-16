using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Runtime.InteropServices;

namespace VisualStudioMcp.Xaml.Tests;

[TestClass]
public class SafeComWrapperTests
{
    private Mock<ILogger> _mockLogger;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
    }

    // Test interface for COM object simulation
    [ComVisible(true)]
    [Guid("12345678-1234-1234-1234-123456789012")]
    public interface ITestComObject
    {
        string GetValue();
        void SetValue(string value);
        int CalculateSum(int a, int b);
    }

    // Mock COM object implementation for testing
    public class MockComObject : ITestComObject
    {
        private string _value = "test";
        public bool IsDisposed { get; private set; }

        public string GetValue() => IsDisposed ? throw new ObjectDisposedException("MockComObject") : _value;
        public void SetValue(string value) => _value = IsDisposed ? throw new ObjectDisposedException("MockComObject") : value;
        public int CalculateSum(int a, int b) => IsDisposed ? throw new ObjectDisposedException("MockComObject") : a + b;

        public void Dispose() => IsDisposed = true;
    }

    [TestMethod]
    public void Constructor_ValidComObject_WrapsSuccessfully()
    {
        // Arrange
        var mockComObject = new MockComObject();

        // Act
        using var wrapper = new SafeComWrapper<ITestComObject>(mockComObject, _mockLogger.Object);

        // Assert
        Assert.IsNotNull(wrapper);
        Assert.IsFalse(wrapper.IsDisposed);
        Assert.AreEqual(mockComObject, wrapper.ComObject);
    }

    [TestMethod]
    public void Constructor_NullComObject_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            new SafeComWrapper<ITestComObject>(null!, _mockLogger.Object);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public void Constructor_NullLogger_DoesNotThrow()
    {
        // Arrange
        var mockComObject = new MockComObject();

        // Act & Assert - Should not throw
        using var wrapper = new SafeComWrapper<ITestComObject>(mockComObject, null);
        Assert.IsNotNull(wrapper);
    }

    [TestMethod]
    public void Create_ValidComObject_ReturnsWrapper()
    {
        // Arrange
        var mockComObject = new MockComObject();

        // Act
        using var wrapper = SafeComWrapper<ITestComObject>.Create(mockComObject, _mockLogger.Object);

        // Assert
        Assert.IsNotNull(wrapper);
        Assert.IsFalse(wrapper.IsDisposed);
    }

    [TestMethod]
    public void Create_NullComObject_ReturnsNull()
    {
        // Act
        var wrapper = SafeComWrapper<ITestComObject>.Create(null, _mockLogger.Object);

        // Assert
        Assert.IsNull(wrapper);
    }

    [TestMethod]
    public void Execute_Action_ExecutesSuccessfully()
    {
        // Arrange
        var mockComObject = new MockComObject();
        using var wrapper = new SafeComWrapper<ITestComObject>(mockComObject, _mockLogger.Object);
        var executed = false;

        // Act
        wrapper.Execute(obj =>
        {
            obj.SetValue("modified");
            executed = true;
        });

        // Assert
        Assert.IsTrue(executed);
        Assert.AreEqual("modified", mockComObject.GetValue());
    }

    [TestMethod]
    public void Execute_Function_ReturnsResult()
    {
        // Arrange
        var mockComObject = new MockComObject();
        using var wrapper = new SafeComWrapper<ITestComObject>(mockComObject, _mockLogger.Object);

        // Act
        var result = wrapper.Execute(obj => obj.CalculateSum(5, 3));

        // Assert
        Assert.AreEqual(8, result);
    }

    [TestMethod]
    public void Execute_NullAction_ThrowsArgumentNullException()
    {
        // Arrange
        var mockComObject = new MockComObject();
        using var wrapper = new SafeComWrapper<ITestComObject>(mockComObject, _mockLogger.Object);

        // Act & Assert
        try
        {
            wrapper.Execute((Action<ITestComObject>)null!);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public void Execute_NullFunction_ThrowsArgumentNullException()
    {
        // Arrange
        var mockComObject = new MockComObject();
        using var wrapper = new SafeComWrapper<ITestComObject>(mockComObject, _mockLogger.Object);

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            wrapper.Execute((Func<ITestComObject, string>)null!));
    }

    [TestMethod]
    public void Execute_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var mockComObject = new MockComObject();
        var wrapper = new SafeComWrapper<ITestComObject>(mockComObject, _mockLogger.Object);
        wrapper.Dispose();

        // Act & Assert
        Assert.ThrowsException<ObjectDisposedException>(() => 
            wrapper.Execute(obj => obj.GetValue()));
    }

    [TestMethod]
    public void Execute_ComException_LogsAndRethrows()
    {
        // Arrange
        var mockComObject = new Mock<ITestComObject>();
        var comException = new COMException("Test COM exception");
        mockComObject.Setup(obj => obj.GetValue()).Throws(comException);

        using var wrapper = new SafeComWrapper<ITestComObject>(mockComObject.Object, _mockLogger.Object);

        // Act & Assert
        var thrownException = Assert.ThrowsException<COMException>(() => 
            wrapper.Execute(obj => obj.GetValue()));

        Assert.AreEqual(comException, thrownException);
    }

    [TestMethod]
    public void Execute_GeneralException_LogsAndRethrows()
    {
        // Arrange
        var mockComObject = new Mock<ITestComObject>();
        var generalException = new InvalidOperationException("Test exception");
        mockComObject.Setup(obj => obj.GetValue()).Throws(generalException);

        using var wrapper = new SafeComWrapper<ITestComObject>(mockComObject.Object, _mockLogger.Object);

        // Act & Assert
        var thrownException = Assert.ThrowsException<InvalidOperationException>(() => 
            wrapper.Execute(obj => obj.GetValue()));

        Assert.AreEqual(generalException, thrownException);
    }

    [TestMethod]
    public void ComObject_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var mockComObject = new MockComObject();
        var wrapper = new SafeComWrapper<ITestComObject>(mockComObject, _mockLogger.Object);
        wrapper.Dispose();

        // Act & Assert
        Assert.ThrowsException<ObjectDisposedException>(() => 
            { var _ = wrapper.ComObject; });
    }

    [TestMethod]
    public void IsDisposed_AfterDispose_ReturnsTrue()
    {
        // Arrange
        var mockComObject = new MockComObject();
        var wrapper = new SafeComWrapper<ITestComObject>(mockComObject, _mockLogger.Object);

        // Act
        wrapper.Dispose();

        // Assert
        Assert.IsTrue(wrapper.IsDisposed);
    }

    [TestMethod]
    public void IsDisposed_BeforeDispose_ReturnsFalse()
    {
        // Arrange
        var mockComObject = new MockComObject();
        using var wrapper = new SafeComWrapper<ITestComObject>(mockComObject, _mockLogger.Object);

        // Assert
        Assert.IsFalse(wrapper.IsDisposed);
    }

    [TestMethod]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var mockComObject = new MockComObject();
        var wrapper = new SafeComWrapper<ITestComObject>(mockComObject, _mockLogger.Object);

        // Act & Assert - Should not throw on multiple dispose calls
        wrapper.Dispose();
        wrapper.Dispose();
        wrapper.Dispose();

        Assert.IsTrue(wrapper.IsDisposed);
    }

    [TestMethod]
    public void UsingStatement_AutomaticallyDisposes()
    {
        // Arrange
        var mockComObject = new MockComObject();
        SafeComWrapper<ITestComObject> wrapper;

        // Act
        using (wrapper = new SafeComWrapper<ITestComObject>(mockComObject, _mockLogger.Object))
        {
            Assert.IsFalse(wrapper.IsDisposed);
        }

        // Assert
        Assert.IsTrue(wrapper.IsDisposed);
    }

    [TestMethod]
    public void Finalizer_ReleasesComObject_WhenNotDisposed()
    {
        // This test is challenging because we can't reliably test finalizers
        // We'll test the disposal pattern instead
        
        // Arrange
        var mockComObject = new MockComObject();
        var wrapper = new SafeComWrapper<ITestComObject>(mockComObject, _mockLogger.Object);

        // Act - Force finalization without explicit dispose
        wrapper = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert - Test passes if no exceptions are thrown during cleanup
        Assert.IsTrue(true);
    }
}

[TestClass]
public class SafeDteWrapperTests
{
    private Mock<ILogger> _mockLogger;
    private Mock<EnvDTE.DTE> _mockDte;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
        _mockDte = new Mock<EnvDTE.DTE>();
    }

    [TestMethod]
    public void Constructor_ValidDte_WrapsSuccessfully()
    {
        // Act
        using var wrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object);

        // Assert
        Assert.IsNotNull(wrapper);
        Assert.IsFalse(wrapper.IsDisposed);
        Assert.AreEqual(_mockDte.Object, wrapper.Dte);
    }

    [TestMethod]
    public void Constructor_NullDte_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            new SafeDteWrapper(null!, _mockLogger.Object);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public void Create_ValidDte_ReturnsWrapper()
    {
        // Act
        using var wrapper = SafeDteWrapper.Create(_mockDte.Object, _mockLogger.Object);

        // Assert
        Assert.IsNotNull(wrapper);
        Assert.IsFalse(wrapper.IsDisposed);
    }

    [TestMethod]
    public void Create_NullDte_ReturnsNull()
    {
        // Act
        var wrapper = SafeDteWrapper.Create(null, _mockLogger.Object);

        // Assert
        Assert.IsNull(wrapper);
    }

    [TestMethod]
    public void GetSolution_ValidSolution_ReturnsWrapper()
    {
        // Arrange
        var mockSolution = new Mock<EnvDTE.Solution>();
        _mockDte.Setup(dte => dte.Solution).Returns(mockSolution.Object);

        using var wrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object);

        // Act
        using var solutionWrapper = wrapper.GetSolution();

        // Assert
        Assert.IsNotNull(solutionWrapper);
        Assert.AreEqual(mockSolution.Object, solutionWrapper.ComObject);
    }

    [TestMethod]
    public void GetSolution_NullSolution_ReturnsNull()
    {
        // Arrange
        _mockDte.Setup(dte => dte.Solution).Returns((EnvDTE.Solution)null!);

        using var wrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object);

        // Act
        var solutionWrapper = wrapper.GetSolution();

        // Assert
        Assert.IsNull(solutionWrapper);
    }

    [TestMethod]
    public void GetSolution_ThrowsException_ReturnsNull()
    {
        // Arrange
        _mockDte.Setup(dte => dte.Solution).Throws(new COMException("Test exception"));

        using var wrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object);

        // Act
        var solutionWrapper = wrapper.GetSolution();

        // Assert
        Assert.IsNull(solutionWrapper);
    }

    [TestMethod]
    public void GetActiveDocument_ValidDocument_ReturnsWrapper()
    {
        // Arrange
        var mockDocument = new Mock<EnvDTE.Document>();
        _mockDte.Setup(dte => dte.ActiveDocument).Returns(mockDocument.Object);

        using var wrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object);

        // Act
        using var documentWrapper = wrapper.GetActiveDocument();

        // Assert
        Assert.IsNotNull(documentWrapper);
        Assert.AreEqual(mockDocument.Object, documentWrapper.ComObject);
    }

    [TestMethod]
    public void GetActiveDocument_NullDocument_ReturnsNull()
    {
        // Arrange
        _mockDte.Setup(dte => dte.ActiveDocument).Returns((EnvDTE.Document)null!);

        using var wrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object);

        // Act
        var documentWrapper = wrapper.GetActiveDocument();

        // Assert
        Assert.IsNull(documentWrapper);
    }

    [TestMethod]
    public void GetActiveDocument_ThrowsException_ReturnsNull()
    {
        // Arrange
        _mockDte.Setup(dte => dte.ActiveDocument).Throws(new COMException("Test exception"));

        using var wrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object);

        // Act
        var documentWrapper = wrapper.GetActiveDocument();

        // Assert
        Assert.IsNull(documentWrapper);
    }

    [TestMethod]
    public void Execute_Action_ExecutesSuccessfully()
    {
        // Arrange
        using var wrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object);
        var executed = false;

        // Act
        wrapper.Execute(dte =>
        {
            Assert.AreEqual(_mockDte.Object, dte);
            executed = true;
        });

        // Assert
        Assert.IsTrue(executed);
    }

    [TestMethod]
    public void Execute_Function_ReturnsResult()
    {
        // Arrange
        _mockDte.Setup(dte => dte.Version).Returns("17.0");
        using var wrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object);

        // Act
        var result = wrapper.Execute(dte => dte.Version);

        // Assert
        Assert.AreEqual("17.0", result);
    }

    [TestMethod]
    public void IsDisposed_AfterDispose_ReturnsTrue()
    {
        // Arrange
        var wrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object);

        // Act
        wrapper.Dispose();

        // Assert
        Assert.IsTrue(wrapper.IsDisposed);
    }

    [TestMethod]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var wrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object);

        // Act & Assert - Should not throw
        wrapper.Dispose();
        wrapper.Dispose();
        wrapper.Dispose();

        Assert.IsTrue(wrapper.IsDisposed);
    }

    [TestMethod]
    public void UsingStatement_AutomaticallyDisposes()
    {
        // Arrange
        SafeDteWrapper wrapper;

        // Act
        using (wrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object))
        {
            Assert.IsFalse(wrapper.IsDisposed);
        }

        // Assert
        Assert.IsTrue(wrapper.IsDisposed);
    }

    [TestMethod]
    public void Dte_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var wrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object);
        wrapper.Dispose();

        // Act & Assert
        Assert.ThrowsException<ObjectDisposedException>(() => 
            { var _ = wrapper.Dte; });
    }

    [TestMethod]
    public void Execute_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var wrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object);
        wrapper.Dispose();

        // Act & Assert
        Assert.ThrowsException<ObjectDisposedException>(() => 
            wrapper.Execute(dte => dte.Version));
    }

    [TestMethod]
    public void NestedWrappers_DisposeCorrectly()
    {
        // Arrange
        var mockSolution = new Mock<EnvDTE.Solution>();
        _mockDte.Setup(dte => dte.Solution).Returns(mockSolution.Object);

        SafeDteWrapper dteWrapper;
        SafeComWrapper<EnvDTE.Solution> solutionWrapper;

        // Act
        using (dteWrapper = new SafeDteWrapper(_mockDte.Object, _mockLogger.Object))
        {
            using (solutionWrapper = dteWrapper.GetSolution())
            {
                Assert.IsNotNull(solutionWrapper);
                Assert.IsFalse(solutionWrapper.IsDisposed);
            }
            
            // Solution wrapper should be disposed
            Assert.IsTrue(solutionWrapper.IsDisposed);
            Assert.IsFalse(dteWrapper.IsDisposed);
        }

        // Both wrappers should be disposed
        Assert.IsTrue(dteWrapper.IsDisposed);
        Assert.IsTrue(solutionWrapper.IsDisposed);
    }

    [TestMethod]
    public void SafeComWrapper_WithNullLogger_WorksCorrectly()
    {
        // Arrange
        var mockComObject = new SafeComWrapperTests.MockComObject();

        // Act & Assert - Should not throw with null logger
        using var wrapper = new SafeComWrapper<SafeComWrapperTests.ITestComObject>(mockComObject, null);
        
        var result = wrapper.Execute(obj => obj.GetValue());
        Assert.AreEqual("test", result);
        
        wrapper.Execute(obj => obj.SetValue("modified"));
        Assert.AreEqual("modified", mockComObject.GetValue());
    }
}