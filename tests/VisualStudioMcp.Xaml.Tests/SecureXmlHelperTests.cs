using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml;

namespace VisualStudioMcp.Xaml.Tests;

[TestClass]
public class SecureXmlHelperTests
{
    private string _testXamlFilePath;
    private string _validXamlContent;
    private string _testDirectory;

    [TestInitialize]
    public void Setup()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SecureXmlTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        
        _testXamlFilePath = Path.Combine(_testDirectory, "test.xaml");
        _validXamlContent = """
            <Window x:Class="TestApp.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    Title="Test Window">
                <Grid>
                    <Button Content="Test Button" />
                </Grid>
            </Window>
            """;
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [TestMethod]
    public void ParseXamlSecurely_ValidXaml_ParsesSuccessfully()
    {
        // Act
        var result = SecureXmlHelper.ParseXamlSecurely(_validXamlContent);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Root);
        Assert.AreEqual("Window", result.Root.Name.LocalName);
        
        // Verify content structure
        var gridElement = result.Root.Element(result.Root.GetDefaultNamespace() + "Grid");
        Assert.IsNotNull(gridElement);
        
        var buttonElement = gridElement.Element(result.Root.GetDefaultNamespace() + "Button");
        Assert.IsNotNull(buttonElement);
        Assert.AreEqual("Test Button", buttonElement.Attribute("Content")?.Value);
    }

    [TestMethod]
    public void ParseXamlSecurely_NullContent_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            SecureXmlHelper.ParseXamlSecurely(null!);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public void ParseXamlSecurely_EmptyContent_ThrowsXmlException()
    {
        // Act & Assert
        try
        {
            SecureXmlHelper.ParseXamlSecurely("");
            Assert.Fail("Expected XmlException was not thrown");
        }
        catch (XmlException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public void ParseXamlSecurely_MalformedXml_ThrowsXmlException()
    {
        // Arrange
        var malformedXaml = "<Grid><Button></Grid>"; // Mismatched tags

        // Act & Assert
        try
        {
            SecureXmlHelper.ParseXamlSecurely(malformedXaml);
            Assert.Fail("Expected XmlException was not thrown");
        }
        catch (XmlException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public void ParseXamlSecurely_XxeAttackAttempt_ThrowsXmlException()
    {
        // Arrange - XML with external entity reference (XXE attack)
        var xxeXaml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE test [
                <!ENTITY xxe SYSTEM "file:///etc/passwd">
            ]>
            <Grid>&xxe;</Grid>
            """;

        // Act & Assert
        try
        {
            SecureXmlHelper.ParseXamlSecurely(xxeXaml);
            Assert.Fail("Expected XmlException was not thrown");
        }
        catch (XmlException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public void ParseXamlSecurely_ProcessingInstructions_IgnoresProcessingInstructions()
    {
        // Arrange
        var xamlWithProcessingInstructions = """
            <?xml version="1.0" encoding="UTF-8"?>
            <?xml-stylesheet type="text/xsl" href="style.xsl"?>
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Button Content="Test" />
            </Grid>
            """;

        // Act
        var result = SecureXmlHelper.ParseXamlSecurely(xamlWithProcessingInstructions);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Grid", result.Root.Name.LocalName);
        
        // Processing instructions should be ignored
        Assert.AreEqual(0, result.Nodes().OfType<System.Xml.Linq.XProcessingInstruction>().Count());
    }

    [TestMethod]
    public void ParseXamlSecurely_Comments_IgnoresComments()
    {
        // Arrange
        var xamlWithComments = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <!-- This is a comment -->
                <Button Content="Test" />
                <!-- Another comment -->
            </Grid>
            """;

        // Act
        var result = SecureXmlHelper.ParseXamlSecurely(xamlWithComments);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Grid", result.Root.Name.LocalName);
        
        // Comments should be ignored
        Assert.AreEqual(0, result.Nodes().OfType<System.Xml.Linq.XComment>().Count());
    }

    [TestMethod]
    public void LoadXamlFileSecurely_ValidFile_LoadsSuccessfully()
    {
        // Arrange
        File.WriteAllText(_testXamlFilePath, _validXamlContent);

        // Act
        var result = SecureXmlHelper.LoadXamlFileSecurely(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Root);
        Assert.AreEqual("Window", result.Root.Name.LocalName);
    }

    [TestMethod]
    public void LoadXamlFileSecurely_NullFilePath_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            SecureXmlHelper.LoadXamlFileSecurely(null!));
    }

    [TestMethod]
    public void LoadXamlFileSecurely_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.xaml");

        // Act & Assert
        Assert.ThrowsException<FileNotFoundException>(() => 
            SecureXmlHelper.LoadXamlFileSecurely(nonExistentPath));
    }

    [TestMethod]
    public void LoadXamlFileSecurely_MalformedFile_ThrowsXmlException()
    {
        // Arrange
        var malformedXaml = "<Grid><Button></Grid>";
        File.WriteAllText(_testXamlFilePath, malformedXaml);

        // Act & Assert
        Assert.ThrowsException<XmlException>(() => 
            SecureXmlHelper.LoadXamlFileSecurely(_testXamlFilePath));
    }

    [TestMethod]
    public void IsFilePathSafe_ValidPath_ReturnsTrue()
    {
        // Arrange
        var validPath = Path.Combine(_testDirectory, "validfile.xaml");

        // Act
        var result = SecureXmlHelper.IsFilePathSafe(validPath);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsFilePathSafe_PathTraversalAttempt_ReturnsFalse()
    {
        // Arrange
        var maliciousPath = "../../../etc/passwd";

        // Act
        var result = SecureXmlHelper.IsFilePathSafe(maliciousPath);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsFilePathSafe_PathWithDoubleDots_ReturnsFalse()
    {
        // Arrange
        var maliciousPath = Path.Combine(_testDirectory, "..", "..", "sensitive.txt");

        // Act
        var result = SecureXmlHelper.IsFilePathSafe(maliciousPath);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsFilePathSafe_PathWithTilde_ReturnsFalse()
    {
        // Arrange
        var pathWithTilde = "~/sensitive.txt";

        // Act
        var result = SecureXmlHelper.IsFilePathSafe(pathWithTilde);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void IsFilePathSafe_WindowsReservedNames_ReturnsFalse()
    {
        // Arrange
        var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "LPT1" };

        foreach (var reservedName in reservedNames)
        {
            var pathWithReservedName = Path.Combine(_testDirectory, $"{reservedName}.xaml");

            // Act
            var result = SecureXmlHelper.IsFilePathSafe(pathWithReservedName);

            // Assert
            Assert.IsFalse(result, $"Reserved name {reservedName} should be rejected");
        }
    }

    [TestMethod]
    public void IsFilePathSafe_WithAllowedDirectory_ValidatesCorrectly()
    {
        // Arrange
        var validPath = Path.Combine(_testDirectory, "validfile.xaml");
        var invalidPath = Path.Combine(Path.GetTempPath(), "outside.xaml");

        // Act
        var validResult = SecureXmlHelper.IsFilePathSafe(validPath, _testDirectory);
        var invalidResult = SecureXmlHelper.IsFilePathSafe(invalidPath, _testDirectory);

        // Assert
        Assert.IsTrue(validResult);
        Assert.IsFalse(invalidResult);
    }

    [TestMethod]
    public void IsFilePathSafe_NullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            SecureXmlHelper.IsFilePathSafe(null!));
    }

    [TestMethod]
    public void ValidateAndNormalizePath_ValidPath_ReturnsNormalizedPath()
    {
        // Arrange
        var validPath = Path.Combine(_testDirectory, "validfile.xaml");

        // Act
        var result = SecureXmlHelper.ValidateAndNormalizePath(validPath);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(Path.IsPathFullyQualified(result));
        Assert.IsTrue(result.EndsWith("validfile.xaml"));
    }

    [TestMethod]
    public void ValidateAndNormalizePath_UnsafePath_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var unsafePath = "../../../etc/passwd";

        // Act & Assert
        Assert.ThrowsException<UnauthorizedAccessException>(() => 
            SecureXmlHelper.ValidateAndNormalizePath(unsafePath));
    }

    [TestMethod]
    public void ValidateAndNormalizePath_NullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            SecureXmlHelper.ValidateAndNormalizePath(null!));
    }

    [TestMethod]
    public void ValidateAndNormalizePath_WithAllowedDirectory_ValidatesAndNormalizes()
    {
        // Arrange
        var validPath = Path.Combine(_testDirectory, "validfile.xaml");
        var relativePath = Path.Combine(".", "validfile.xaml");

        // Act
        var result = SecureXmlHelper.ValidateAndNormalizePath(relativePath, _testDirectory);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(Path.IsPathFullyQualified(result));
    }

    [TestMethod]
    public void ParseXamlSecurely_LargeDocument_HandlesWithinLimits()
    {
        // Arrange - Create a moderately large but valid XAML document
        var largeXaml = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            """;
        
        // Add many button elements to increase size
        for (int i = 0; i < 1000; i++)
        {
            largeXaml += $"    <Button Content=\"Button {i}\" />\n";
        }
        largeXaml += "</Grid>";

        // Act
        var result = SecureXmlHelper.ParseXamlSecurely(largeXaml);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Grid", result.Root.Name.LocalName);
        
        // Verify that all buttons were parsed
        var buttons = result.Root.Elements().Count();
        Assert.AreEqual(1000, buttons);
    }

    [TestMethod]
    public void ParseXamlSecurely_ExcessivelyLargeDocument_ThrowsException()
    {
        // Arrange - Create a document that exceeds the 1MB limit
        var baseXaml = "<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">";
        var largeString = new string('A', 1_100_000); // 1.1MB of A's
        var endXaml = "</Grid>";
        var excessivelyLargeXaml = baseXaml + largeString + endXaml;

        // Act & Assert
        Assert.ThrowsException<XmlException>(() => 
            SecureXmlHelper.ParseXamlSecurely(excessivelyLargeXaml));
    }

    [TestMethod]
    public void IsFilePathSafe_EdgeCases_HandlesCorrectly()
    {
        // Test various edge cases
        Assert.IsTrue(SecureXmlHelper.IsFilePathSafe("normalfile.xaml"));
        Assert.IsTrue(SecureXmlHelper.IsFilePathSafe(@"C:\valid\path\file.xaml"));
        Assert.IsTrue(SecureXmlHelper.IsFilePathSafe("/valid/unix/path/file.xaml"));
        
        Assert.IsFalse(SecureXmlHelper.IsFilePathSafe(""));
        Assert.IsFalse(SecureXmlHelper.IsFilePathSafe("file..txt"));
        Assert.IsFalse(SecureXmlHelper.IsFilePathSafe(".."));
        Assert.IsFalse(SecureXmlHelper.IsFilePathSafe("."));
    }

    [TestMethod]
    public void ParseXamlSecurely_NamespaceDeclarations_PreservesNamespaces()
    {
        // Arrange
        var xamlWithNamespaces = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:local="clr-namespace:MyApp"
                    x:Class="MyApp.MainWindow">
                <Grid>
                    <local:CustomControl x:Name="MyControl" />
                </Grid>
            </Window>
            """;

        // Act
        var result = SecureXmlHelper.ParseXamlSecurely(xamlWithNamespaces);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Window", result.Root.Name.LocalName);
        
        // Verify namespace attributes are preserved
        var classAttribute = result.Root.Attributes()
            .FirstOrDefault(a => a.Name.LocalName == "Class" && a.Name.Namespace.NamespaceName.Contains("xaml"));
        Assert.IsNotNull(classAttribute);
        Assert.AreEqual("MyApp.MainWindow", classAttribute.Value);
    }

    [TestMethod]
    public void ParseXamlSecurely_SpecialCharactersInContent_HandlesCorrectly()
    {
        // Arrange
        var xamlWithSpecialChars = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Button Content="Special chars: &amp; &lt; &gt; &quot; &apos;" />
                <TextBox Text="Unicode: ♠ ♥ ♦ ♣" />
            </Grid>
            """;

        // Act
        var result = SecureXmlHelper.ParseXamlSecurely(xamlWithSpecialChars);

        // Assert
        Assert.IsNotNull(result);
        
        var buttonElement = result.Root.Element(result.Root.GetDefaultNamespace() + "Button");
        Assert.IsNotNull(buttonElement);
        
        var content = buttonElement.Attribute("Content")?.Value;
        Assert.IsTrue(content?.Contains("& < > \" '"));
        
        var textBoxElement = result.Root.Element(result.Root.GetDefaultNamespace() + "TextBox");
        Assert.IsNotNull(textBoxElement);
        
        var text = textBoxElement.Attribute("Text")?.Value;
        Assert.IsTrue(text?.Contains("♠ ♥ ♦ ♣"));
    }
}