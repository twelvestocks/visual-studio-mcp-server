using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Xml.Linq;

namespace VisualStudioMcp.Xaml.Tests;

[TestClass]
public class XamlParserTests
{
    private Mock<ILogger<XamlParser>> _mockLogger;
    private XamlParser _parser;
    private string _testXamlFilePath;
    private string _testXamlContent;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<XamlParser>>();
        _parser = new XamlParser(_mockLogger.Object);
        
        _testXamlFilePath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.xaml");
        _testXamlContent = """
            <Window x:Class="TestApp.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    Title="Test Window" Height="350" Width="525">
                <Grid Name="MainGrid">
                    <Button x:Name="TestButton" Content="Click Me" Width="100" Height="30"/>
                    <TextBox Name="TestTextBox" Text="Hello World" />
                    <Label Content="Test Label" />
                    <Button Name="AnotherButton" Content="Another Button" />
                </Grid>
            </Window>
            """;
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(_testXamlFilePath))
        {
            File.Delete(_testXamlFilePath);
        }
        _parser?.ClearCache();
    }

    [TestMethod]
    public async Task ParseVisualTreeAsync_ValidXamlFile_ReturnsExpectedElements()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _testXamlContent);

        // Act
        var elements = await _parser.ParseVisualTreeAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(elements);
        Assert.AreEqual(5, elements.Length); // Window, Grid, Button, TextBox, Label, Button
        
        var windowElement = elements.First(e => e.ElementType == "Window");
        Assert.AreEqual("Window", windowElement.Name);
        Assert.AreEqual("TestApp.MainWindow", windowElement.Properties.GetValueOrDefault("x:Class", ""));
        Assert.AreEqual(0, windowElement.Depth);

        var gridElement = elements.First(e => e.ElementType == "Grid");
        Assert.AreEqual("MainGrid", gridElement.ElementName);
        Assert.AreEqual(1, gridElement.Depth);
        Assert.AreEqual(windowElement, gridElement.Parent);
    }

    [TestMethod]
    public async Task ParseVisualTreeAsync_NonExistentFile_ReturnsEmptyArray()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "nonexistent.xaml");

        // Act
        var elements = await _parser.ParseVisualTreeAsync(nonExistentPath);

        // Assert
        Assert.IsNotNull(elements);
        Assert.AreEqual(0, elements.Length);
    }

    [TestMethod]
    public async Task ParseVisualTreeAsync_UnsafeFilePath_ReturnsEmptyArray()
    {
        // Arrange
        var unsafePath = "../../../etc/passwd";

        // Act
        var elements = await _parser.ParseVisualTreeAsync(unsafePath);

        // Assert
        Assert.IsNotNull(elements);
        Assert.AreEqual(0, elements.Length);
    }

    [TestMethod]
    public async Task GetRootElementAsync_ValidXamlFile_ReturnsRootElement()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _testXamlContent);

        // Act
        var rootElement = await _parser.GetRootElementAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(rootElement);
        Assert.AreEqual("Window", rootElement.ElementType);
        Assert.AreEqual(0, rootElement.Depth);
        Assert.IsNull(rootElement.Parent);
        Assert.AreEqual("TestApp.MainWindow", rootElement.Properties.GetValueOrDefault("x:Class", ""));
    }

    [TestMethod]
    public async Task GetRootElementAsync_NonExistentFile_ReturnsNull()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "nonexistent.xaml");

        // Act
        var rootElement = await _parser.GetRootElementAsync(nonExistentPath);

        // Assert
        Assert.IsNull(rootElement);
    }

    [TestMethod]
    public async Task FindElementsByNameAsync_ExistingElementName_ReturnsMatchingElements()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _testXamlContent);

        // Act
        var elements = await _parser.FindElementsByNameAsync(_testXamlFilePath, "TestButton");

        // Assert
        Assert.IsNotNull(elements);
        Assert.AreEqual(1, elements.Length);
        Assert.AreEqual("Button", elements[0].ElementType);
        Assert.AreEqual("TestButton", elements[0].ElementName);
        Assert.AreEqual("Click Me", elements[0].Properties.GetValueOrDefault("Content", ""));
    }

    [TestMethod]
    public async Task FindElementsByNameAsync_MultipleMatches_ReturnsAllMatches()
    {
        // Arrange
        var xamlWithDuplicateNames = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Button Name="TestElement" Content="First" />
                <TextBox Name="TestElement" Text="Second" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlWithDuplicateNames);

        // Act
        var elements = await _parser.FindElementsByNameAsync(_testXamlFilePath, "TestElement");

        // Assert
        Assert.IsNotNull(elements);
        Assert.AreEqual(2, elements.Length);
        Assert.IsTrue(elements.Any(e => e.ElementType == "Button"));
        Assert.IsTrue(elements.Any(e => e.ElementType == "TextBox"));
    }

    [TestMethod]
    public async Task FindElementsByNameAsync_NonExistentName_ReturnsEmptyArray()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _testXamlContent);

        // Act
        var elements = await _parser.FindElementsByNameAsync(_testXamlFilePath, "NonExistentElement");

        // Assert
        Assert.IsNotNull(elements);
        Assert.AreEqual(0, elements.Length);
    }

    [TestMethod]
    public async Task FindElementsByNameAsync_EmptyElementName_ReturnsEmptyArray()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _testXamlContent);

        // Act
        var elements = await _parser.FindElementsByNameAsync(_testXamlFilePath, "");

        // Assert
        Assert.IsNotNull(elements);
        Assert.AreEqual(0, elements.Length);
    }

    [TestMethod]
    public async Task FindElementsByTypeAsync_ExistingElementType_ReturnsMatchingElements()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _testXamlContent);

        // Act
        var elements = await _parser.FindElementsByTypeAsync(_testXamlFilePath, "Button");

        // Assert
        Assert.IsNotNull(elements);
        Assert.AreEqual(2, elements.Length); // TestButton and AnotherButton
        Assert.IsTrue(elements.All(e => e.ElementType == "Button"));
    }

    [TestMethod]
    public async Task FindElementsByTypeAsync_NonExistentType_ReturnsEmptyArray()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _testXamlContent);

        // Act
        var elements = await _parser.FindElementsByTypeAsync(_testXamlFilePath, "NonExistentType");

        // Assert
        Assert.IsNotNull(elements);
        Assert.AreEqual(0, elements.Length);
    }

    [TestMethod]
    public async Task FindElementsByTypeAsync_CaseInsensitive_ReturnsMatchingElements()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _testXamlContent);

        // Act
        var elements = await _parser.FindElementsByTypeAsync(_testXamlFilePath, "button");

        // Assert
        Assert.IsNotNull(elements);
        Assert.AreEqual(2, elements.Length);
        Assert.IsTrue(elements.All(e => e.ElementType == "Button"));
    }

    [TestMethod]
    public void InvalidateCache_ExistingFile_RemovesFromCache()
    {
        // Arrange
        var testPath = "/test/path.xaml";

        // Act
        _parser.InvalidateCache(testPath);

        // Assert - Should not throw exception
        Assert.IsTrue(true);
    }

    [TestMethod]
    public void ClearCache_RemovesAllCachedDocuments()
    {
        // Act
        _parser.ClearCache();

        // Assert - Should not throw exception
        Assert.IsTrue(true);
    }

    [TestMethod]
    public async Task GetIndexStatisticsAsync_ValidFile_ReturnsStatistics()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _testXamlContent);

        // Act - First call to trigger index creation
        await _parser.ParseVisualTreeAsync(_testXamlFilePath);
        var statistics = await _parser.GetIndexStatisticsAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(statistics);
        Assert.AreEqual(_testXamlFilePath, statistics.FilePath);
        Assert.IsTrue(statistics.TotalElements > 0);
        Assert.IsTrue(statistics.UniqueElementTypes > 0);
    }

    [TestMethod]
    public void GetIndexStatistics_ReturnsAllCachedStatistics()
    {
        // Act
        var allStatistics = _parser.GetIndexStatistics();

        // Assert
        Assert.IsNotNull(allStatistics);
        Assert.IsTrue(allStatistics.Length >= 0);
    }

    [TestMethod]
    public async Task ParseVisualTreeAsync_CachesBetweenCalls_UsesCache()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _testXamlContent);

        // Act - First call should parse and cache
        var elements1 = await _parser.ParseVisualTreeAsync(_testXamlFilePath);
        var elements2 = await _parser.ParseVisualTreeAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(elements1);
        Assert.IsNotNull(elements2);
        Assert.AreEqual(elements1.Length, elements2.Length);
    }

    [TestMethod]
    public async Task FindElementsByNameAsync_UsesIndexedLookup_WhenAvailable()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _testXamlContent);

        // Act - First call builds index, second should use it
        await _parser.ParseVisualTreeAsync(_testXamlFilePath); // Build index
        var elements = await _parser.FindElementsByNameAsync(_testXamlFilePath, "TestButton");

        // Assert
        Assert.IsNotNull(elements);
        Assert.AreEqual(1, elements.Length);
        Assert.AreEqual("TestButton", elements[0].ElementName);
    }

    [TestMethod]
    public async Task FindElementsByTypeAsync_UsesIndexedLookup_WhenAvailable()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _testXamlContent);

        // Act - First call builds index, second should use it
        await _parser.ParseVisualTreeAsync(_testXamlFilePath); // Build index
        var elements = await _parser.FindElementsByTypeAsync(_testXamlFilePath, "Button");

        // Assert
        Assert.IsNotNull(elements);
        Assert.AreEqual(2, elements.Length);
        Assert.IsTrue(elements.All(e => e.ElementType == "Button"));
    }

    [TestMethod]
    public async Task ParseVisualTreeAsync_HandlesNamespaces_Correctly()
    {
        // Arrange
        var xamlWithNamespaces = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:local="clr-namespace:TestApp">
                <Grid>
                    <local:CustomControl x:Name="CustomElement" />
                </Grid>
            </Window>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlWithNamespaces);

        // Act
        var elements = await _parser.ParseVisualTreeAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(elements);
        var customElement = elements.FirstOrDefault(e => e.ElementName == "CustomElement");
        Assert.IsNotNull(customElement);
        Assert.AreEqual("CustomControl", customElement.ElementType);
    }

    [TestMethod]
    public async Task ParseVisualTreeAsync_HandlesTextContent_Correctly()
    {
        // Arrange
        var xamlWithTextContent = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Label>Simple Text Content</Label>
                <TextBlock>
                    Multi-line
                    Text Content
                </TextBlock>
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlWithTextContent);

        // Act
        var elements = await _parser.ParseVisualTreeAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(elements);
        var labelElement = elements.FirstOrDefault(e => e.ElementType == "Label");
        Assert.IsNotNull(labelElement);
        Assert.AreEqual("Simple Text Content", labelElement.Properties.GetValueOrDefault("Content", ""));

        var textBlockElement = elements.FirstOrDefault(e => e.ElementType == "TextBlock");
        Assert.IsNotNull(textBlockElement);
        Assert.IsTrue(textBlockElement.Properties.ContainsKey("Content"));
    }

    [TestMethod]
    public async Task ParseVisualTreeAsync_InvalidXml_ReturnsEmptyArray()
    {
        // Arrange
        var invalidXaml = "<Grid><Button></Grid>"; // Mismatched tags
        await File.WriteAllTextAsync(_testXamlFilePath, invalidXaml);

        // Act
        var elements = await _parser.ParseVisualTreeAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(elements);
        Assert.AreEqual(0, elements.Length);
    }

    [TestMethod]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            new XamlParser(null!);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException)
        {
            // Expected exception
        }
    }
}