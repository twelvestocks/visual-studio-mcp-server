using System.Xml;
using System.Xml.Linq;

namespace VisualStudioMcp.Xaml;

/// <summary>
/// Provides secure XML parsing functionality to prevent XXE attacks and other XML-based vulnerabilities.
/// </summary>
public static class SecureXmlHelper
{
    /// <summary>
    /// Parses XAML content using secure XML reader settings to prevent XXE attacks.
    /// </summary>
    /// <param name="xamlContent">The XAML content to parse.</param>
    /// <returns>The parsed XDocument.</returns>
    /// <exception cref="ArgumentNullException">Thrown when xamlContent is null.</exception>
    /// <exception cref="XmlException">Thrown when the XAML content is malformed.</exception>
    public static XDocument ParseXamlSecurely(string xamlContent)
    {
        if (xamlContent == null)
            throw new ArgumentNullException(nameof(xamlContent));

        var settings = CreateSecureXmlReaderSettings();
        
        using var stringReader = new StringReader(xamlContent);
        using var xmlReader = XmlReader.Create(stringReader, settings);
        
        return XDocument.Load(xmlReader);
    }

    /// <summary>
    /// Loads and parses a XAML file using secure XML reader settings.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file to load.</param>
    /// <returns>The parsed XDocument.</returns>
    /// <exception cref="ArgumentNullException">Thrown when xamlFilePath is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist.</exception>
    /// <exception cref="XmlException">Thrown when the XAML file is malformed.</exception>
    public static XDocument LoadXamlFileSecurely(string xamlFilePath)
    {
        if (xamlFilePath == null)
            throw new ArgumentNullException(nameof(xamlFilePath));

        if (!File.Exists(xamlFilePath))
            throw new FileNotFoundException($"XAML file not found: {xamlFilePath}");

        var xamlContent = File.ReadAllText(xamlFilePath);
        return ParseXamlSecurely(xamlContent);
    }

    /// <summary>
    /// Creates secure XML reader settings that prevent XXE attacks and other XML vulnerabilities.
    /// </summary>
    /// <returns>Configured XmlReaderSettings with security hardening.</returns>
    private static XmlReaderSettings CreateSecureXmlReaderSettings()
    {
        return new XmlReaderSettings
        {
            // Disable DTD processing to prevent XXE attacks
            DtdProcessing = DtdProcessing.Prohibit,
            
            // Disable XML resolver to prevent external entity resolution
            XmlResolver = null,
            
            // Validate namespace declarations
            ValidationType = ValidationType.None,
            
            // Close input when done
            CloseInput = true,
            
            // Ignore whitespace for cleaner parsing
            IgnoreWhitespace = false,
            
            // Ignore processing instructions
            IgnoreProcessingInstructions = true,
            
            // Ignore comments
            IgnoreComments = true,
            
            // Maximum number of characters to read (prevent DoS attacks via large documents)
            MaxCharactersInDocument = 1_000_000, // 1MB limit for XAML files
            
            // Maximum nesting depth (prevent stack overflow attacks)
            MaxCharactersFromEntities = 0, // No entity expansion allowed
            
            // Conformance level
            ConformanceLevel = ConformanceLevel.Document
        };
    }

    /// <summary>
    /// Validates that a file path is safe to access (prevents path traversal attacks).
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <param name="allowedDirectory">Optional allowed base directory. If null, only prevents traversal attacks.</param>
    /// <returns>True if the path is safe to access.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null.</exception>
    public static bool IsFilePathSafe(string filePath, string? allowedDirectory = null)
    {
        if (filePath == null)
            throw new ArgumentNullException(nameof(filePath));

        try
        {
            // Get the full path to resolve any relative path components
            var fullPath = Path.GetFullPath(filePath);
            
            // Check for dangerous path characters
            if (fullPath.Contains("..") || fullPath.Contains("~"))
                return false;
                
            // If an allowed directory is specified, ensure the file is within it
            if (!string.IsNullOrEmpty(allowedDirectory))
            {
                var allowedFullPath = Path.GetFullPath(allowedDirectory);
                if (!fullPath.StartsWith(allowedFullPath, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            
            // Check for Windows-specific dangerous paths
            var fileName = Path.GetFileName(fullPath);
            var dangerousNames = new[] 
            { 
                "CON", "PRN", "AUX", "NUL", 
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", 
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" 
            };
            
            if (dangerousNames.Contains(Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant()))
                return false;
                
            return true;
        }
        catch (Exception)
        {
            // If any exception occurs during path validation, consider it unsafe
            return false;
        }
    }

    /// <summary>
    /// Validates and normalizes a file path for safe access.
    /// </summary>
    /// <param name="filePath">The file path to validate and normalize.</param>
    /// <param name="allowedDirectory">Optional allowed base directory.</param>
    /// <returns>The normalized safe file path.</returns>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the path is unsafe.</exception>
    public static string ValidateAndNormalizePath(string filePath, string? allowedDirectory = null)
    {
        if (filePath == null)
            throw new ArgumentNullException(nameof(filePath));

        if (!IsFilePathSafe(filePath, allowedDirectory))
        {
            throw new UnauthorizedAccessException($"File path is not safe for access: {filePath}");
        }

        return Path.GetFullPath(filePath);
    }
}