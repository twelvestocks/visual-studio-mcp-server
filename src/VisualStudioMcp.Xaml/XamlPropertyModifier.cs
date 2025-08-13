using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace VisualStudioMcp.Xaml;

/// <summary>
/// Handles XAML property modification operations.
/// Responsible for safely modifying element properties in XAML files.
/// </summary>
public class XamlPropertyModifier
{
    private readonly ILogger<XamlPropertyModifier> _logger;
    private readonly XamlParser _xamlParser;

    /// <summary>
    /// Initializes a new instance of the XamlPropertyModifier class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="xamlParser">XAML parser for document access.</param>
    public XamlPropertyModifier(ILogger<XamlPropertyModifier> logger, XamlParser xamlParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _xamlParser = xamlParser ?? throw new ArgumentNullException(nameof(xamlParser));
    }

    /// <summary>
    /// Modifies a property value for all elements with the specified name.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementName">Name of the element to modify (x:Name or Name attribute).</param>
    /// <param name="propertyName">Name of the property to modify.</param>
    /// <param name="propertyValue">New value for the property.</param>
    /// <returns>True if the modification was successful, false otherwise.</returns>
    public async Task<bool> ModifyElementPropertyAsync(string xamlFilePath, string elementName, string propertyName, string propertyValue)
    {
        _logger.LogInformation("Modifying property '{PropertyName}' to '{PropertyValue}' for element '{ElementName}' in file: {FilePath}",
            propertyName, propertyValue, elementName, xamlFilePath);

        try
        {
            if (!SecureXmlHelper.IsFilePathSafe(xamlFilePath))
            {
                _logger.LogWarning("Unsafe file path rejected: {FilePath}", xamlFilePath);
                return false;
            }

            if (string.IsNullOrWhiteSpace(elementName) || string.IsNullOrWhiteSpace(propertyName))
            {
                _logger.LogWarning("Element name and property name cannot be empty");
                return false;
            }

            if (!File.Exists(xamlFilePath))
            {
                _logger.LogWarning("XAML file not found: {FilePath}", xamlFilePath);
                return false;
            }

            // Load and parse the XAML document
            var xamlContent = await File.ReadAllTextAsync(xamlFilePath).ConfigureAwait(false);
            var document = SecureXmlHelper.ParseXamlSecurely(xamlContent);

            if (document?.Root == null)
            {
                _logger.LogWarning("Failed to parse XAML document: {FilePath}", xamlFilePath);
                return false;
            }

            // Find elements with matching name and modify properties
            var modifiedCount = ModifyElementsInDocument(document, elementName, propertyName, propertyValue);

            if (modifiedCount > 0)
            {
                // Save the modified document back to file
                await SaveDocumentSafelyAsync(document, xamlFilePath).ConfigureAwait(false);
                
                // Invalidate parser cache for this file
                _xamlParser.InvalidateCache(xamlFilePath);

                _logger.LogInformation("Successfully modified {Count} elements in file: {FilePath}", modifiedCount, xamlFilePath);
                return true;
            }
            else
            {
                _logger.LogWarning("No elements found with name '{ElementName}' in file: {FilePath}", elementName, xamlFilePath);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error modifying element property in file: {FilePath}", xamlFilePath);
            return false;
        }
    }

    /// <summary>
    /// Modifies properties for all elements of a specific type.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementType">Type of elements to modify (local name).</param>
    /// <param name="propertyName">Name of the property to modify.</param>
    /// <param name="propertyValue">New value for the property.</param>
    /// <returns>True if the modification was successful, false otherwise.</returns>
    public async Task<bool> ModifyElementsByTypeAsync(string xamlFilePath, string elementType, string propertyName, string propertyValue)
    {
        _logger.LogInformation("Modifying property '{PropertyName}' to '{PropertyValue}' for all '{ElementType}' elements in file: {FilePath}",
            propertyName, propertyValue, elementType, xamlFilePath);

        try
        {
            if (!SecureXmlHelper.IsFilePathSafe(xamlFilePath))
            {
                _logger.LogWarning("Unsafe file path rejected: {FilePath}", xamlFilePath);
                return false;
            }

            if (string.IsNullOrWhiteSpace(elementType) || string.IsNullOrWhiteSpace(propertyName))
            {
                _logger.LogWarning("Element type and property name cannot be empty");
                return false;
            }

            if (!File.Exists(xamlFilePath))
            {
                _logger.LogWarning("XAML file not found: {FilePath}", xamlFilePath);
                return false;
            }

            // Load and parse the XAML document
            var xamlContent = await File.ReadAllTextAsync(xamlFilePath).ConfigureAwait(false);
            var document = SecureXmlHelper.ParseXamlSecurely(xamlContent);

            if (document?.Root == null)
            {
                _logger.LogWarning("Failed to parse XAML document: {FilePath}", xamlFilePath);
                return false;
            }

            // Find elements of matching type and modify properties
            var modifiedCount = ModifyElementsByTypeInDocument(document, elementType, propertyName, propertyValue);

            if (modifiedCount > 0)
            {
                // Save the modified document back to file
                await SaveDocumentSafelyAsync(document, xamlFilePath).ConfigureAwait(false);
                
                // Invalidate parser cache for this file
                _xamlParser.InvalidateCache(xamlFilePath);

                _logger.LogInformation("Successfully modified {Count} '{ElementType}' elements in file: {FilePath}", 
                    modifiedCount, elementType, xamlFilePath);
                return true;
            }
            else
            {
                _logger.LogWarning("No elements of type '{ElementType}' found in file: {FilePath}", elementType, xamlFilePath);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error modifying elements by type in file: {FilePath}", xamlFilePath);
            return false;
        }
    }

    /// <summary>
    /// Adds a new property to all elements with the specified name.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementName">Name of the element to modify.</param>
    /// <param name="propertyName">Name of the property to add.</param>
    /// <param name="propertyValue">Value for the new property.</param>
    /// <returns>True if the property was added successfully, false otherwise.</returns>
    public async Task<bool> AddElementPropertyAsync(string xamlFilePath, string elementName, string propertyName, string propertyValue)
    {
        _logger.LogInformation("Adding property '{PropertyName}' with value '{PropertyValue}' to element '{ElementName}' in file: {FilePath}",
            propertyName, propertyValue, elementName, xamlFilePath);

        try
        {
            if (!SecureXmlHelper.IsFilePathSafe(xamlFilePath))
            {
                _logger.LogWarning("Unsafe file path rejected: {FilePath}", xamlFilePath);
                return false;
            }

            if (string.IsNullOrWhiteSpace(elementName) || string.IsNullOrWhiteSpace(propertyName))
            {
                _logger.LogWarning("Element name and property name cannot be empty");
                return false;
            }

            if (!File.Exists(xamlFilePath))
            {
                _logger.LogWarning("XAML file not found: {FilePath}", xamlFilePath);
                return false;
            }

            // Load and parse the XAML document
            var xamlContent = await File.ReadAllTextAsync(xamlFilePath).ConfigureAwait(false);
            var document = SecureXmlHelper.ParseXamlSecurely(xamlContent);

            if (document?.Root == null)
            {
                _logger.LogWarning("Failed to parse XAML document: {FilePath}", xamlFilePath);
                return false;
            }

            // Find elements with matching name and add properties
            var modifiedCount = AddPropertyToElementsInDocument(document, elementName, propertyName, propertyValue);

            if (modifiedCount > 0)
            {
                // Save the modified document back to file
                await SaveDocumentSafelyAsync(document, xamlFilePath).ConfigureAwait(false);
                
                // Invalidate parser cache for this file
                _xamlParser.InvalidateCache(xamlFilePath);

                _logger.LogInformation("Successfully added property to {Count} elements in file: {FilePath}", modifiedCount, xamlFilePath);
                return true;
            }
            else
            {
                _logger.LogWarning("No elements found with name '{ElementName}' in file: {FilePath}", elementName, xamlFilePath);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding element property in file: {FilePath}", xamlFilePath);
            return false;
        }
    }

    /// <summary>
    /// Removes a property from all elements with the specified name.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementName">Name of the element to modify.</param>
    /// <param name="propertyName">Name of the property to remove.</param>
    /// <returns>True if the property was removed successfully, false otherwise.</returns>
    public async Task<bool> RemoveElementPropertyAsync(string xamlFilePath, string elementName, string propertyName)
    {
        _logger.LogInformation("Removing property '{PropertyName}' from element '{ElementName}' in file: {FilePath}",
            propertyName, elementName, xamlFilePath);

        try
        {
            if (!SecureXmlHelper.IsFilePathSafe(xamlFilePath))
            {
                _logger.LogWarning("Unsafe file path rejected: {FilePath}", xamlFilePath);
                return false;
            }

            if (string.IsNullOrWhiteSpace(elementName) || string.IsNullOrWhiteSpace(propertyName))
            {
                _logger.LogWarning("Element name and property name cannot be empty");
                return false;
            }

            if (!File.Exists(xamlFilePath))
            {
                _logger.LogWarning("XAML file not found: {FilePath}", xamlFilePath);
                return false;
            }

            // Load and parse the XAML document
            var xamlContent = await File.ReadAllTextAsync(xamlFilePath).ConfigureAwait(false);
            var document = SecureXmlHelper.ParseXamlSecurely(xamlContent);

            if (document?.Root == null)
            {
                _logger.LogWarning("Failed to parse XAML document: {FilePath}", xamlFilePath);
                return false;
            }

            // Find elements with matching name and remove properties
            var modifiedCount = RemovePropertyFromElementsInDocument(document, elementName, propertyName);

            if (modifiedCount > 0)
            {
                // Save the modified document back to file
                await SaveDocumentSafelyAsync(document, xamlFilePath).ConfigureAwait(false);
                
                // Invalidate parser cache for this file
                _xamlParser.InvalidateCache(xamlFilePath);

                _logger.LogInformation("Successfully removed property from {Count} elements in file: {FilePath}", modifiedCount, xamlFilePath);
                return true;
            }
            else
            {
                _logger.LogWarning("No elements found with name '{ElementName}' or property '{PropertyName}' in file: {FilePath}", 
                    elementName, propertyName, xamlFilePath);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing element property in file: {FilePath}", xamlFilePath);
            return false;
        }
    }

    /// <summary>
    /// Creates a backup of the XAML file before modification.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Path to the backup file, or null if backup failed.</returns>
    public async Task<string?> CreateBackupAsync(string xamlFilePath)
    {
        try
        {
            if (!File.Exists(xamlFilePath))
            {
                _logger.LogWarning("Cannot create backup - file not found: {FilePath}", xamlFilePath);
                return null;
            }

            var backupPath = $"{xamlFilePath}.backup.{DateTime.Now:yyyyMMddHHmmss}";
            File.Copy(xamlFilePath, backupPath);
            
            _logger.LogInformation("Created backup: {BackupPath}", backupPath);
            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup for file: {FilePath}", xamlFilePath);
            return null;
        }
    }

    /// <summary>
    /// Modifies elements in the document by name.
    /// </summary>
    private int ModifyElementsInDocument(XDocument document, string elementName, string propertyName, string propertyValue)
    {
        var modifiedCount = 0;

        try
        {
            var elements = FindElementsByName(document.Root!, elementName);

            foreach (var element in elements)
            {
                // Handle namespace-qualified properties
                var attributeName = GetAttributeName(propertyName);
                element.SetAttributeValue(attributeName, propertyValue);
                modifiedCount++;
                
                _logger.LogDebug("Modified {ElementType} element: {PropertyName} = {PropertyValue}", 
                    element.Name.LocalName, propertyName, propertyValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error modifying elements by name: {ElementName}", elementName);
        }

        return modifiedCount;
    }

    /// <summary>
    /// Modifies elements in the document by type.
    /// </summary>
    private int ModifyElementsByTypeInDocument(XDocument document, string elementType, string propertyName, string propertyValue)
    {
        var modifiedCount = 0;

        try
        {
            var elements = FindElementsByType(document.Root!, elementType);

            foreach (var element in elements)
            {
                var attributeName = GetAttributeName(propertyName);
                element.SetAttributeValue(attributeName, propertyValue);
                modifiedCount++;
                
                _logger.LogDebug("Modified {ElementType} element: {PropertyName} = {PropertyValue}", 
                    element.Name.LocalName, propertyName, propertyValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error modifying elements by type: {ElementType}", elementType);
        }

        return modifiedCount;
    }

    /// <summary>
    /// Adds a property to elements in the document.
    /// </summary>
    private int AddPropertyToElementsInDocument(XDocument document, string elementName, string propertyName, string propertyValue)
    {
        var modifiedCount = 0;

        try
        {
            var elements = FindElementsByName(document.Root!, elementName);

            foreach (var element in elements)
            {
                var attributeName = GetAttributeName(propertyName);
                
                // Only add if property doesn't already exist
                if (element.Attribute(attributeName) == null)
                {
                    element.SetAttributeValue(attributeName, propertyValue);
                    modifiedCount++;
                    
                    _logger.LogDebug("Added property to {ElementType} element: {PropertyName} = {PropertyValue}", 
                        element.Name.LocalName, propertyName, propertyValue);
                }
                else
                {
                    _logger.LogDebug("Property {PropertyName} already exists on {ElementType} element, skipping", 
                        propertyName, element.Name.LocalName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error adding properties to elements: {ElementName}", elementName);
        }

        return modifiedCount;
    }

    /// <summary>
    /// Removes a property from elements in the document.
    /// </summary>
    private int RemovePropertyFromElementsInDocument(XDocument document, string elementName, string propertyName)
    {
        var modifiedCount = 0;

        try
        {
            var elements = FindElementsByName(document.Root!, elementName);

            foreach (var element in elements)
            {
                var attributeName = GetAttributeName(propertyName);
                var attribute = element.Attribute(attributeName);
                
                if (attribute != null)
                {
                    attribute.Remove();
                    modifiedCount++;
                    
                    _logger.LogDebug("Removed property from {ElementType} element: {PropertyName}", 
                        element.Name.LocalName, propertyName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error removing properties from elements: {ElementName}", elementName);
        }

        return modifiedCount;
    }

    /// <summary>
    /// Finds elements in the document by name.
    /// </summary>
    private IEnumerable<XElement> FindElementsByName(XElement root, string elementName)
    {
        var elements = new List<XElement>();
        FindElementsByNameRecursive(root, elementName, elements);
        return elements;
    }

    /// <summary>
    /// Recursively finds elements by name.
    /// </summary>
    private void FindElementsByNameRecursive(XElement element, string targetName, List<XElement> results)
    {
        // Check Name and x:Name attributes
        var nameAttr = element.Attribute("Name")?.Value ?? 
                      element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value;

        if (!string.IsNullOrEmpty(nameAttr) && nameAttr.Equals(targetName, StringComparison.OrdinalIgnoreCase))
        {
            results.Add(element);
        }

        // Continue searching in child elements
        foreach (var child in element.Elements())
        {
            FindElementsByNameRecursive(child, targetName, results);
        }
    }

    /// <summary>
    /// Finds elements in the document by type.
    /// </summary>
    private IEnumerable<XElement> FindElementsByType(XElement root, string elementType)
    {
        var elements = new List<XElement>();
        FindElementsByTypeRecursive(root, elementType, elements);
        return elements;
    }

    /// <summary>
    /// Recursively finds elements by type.
    /// </summary>
    private void FindElementsByTypeRecursive(XElement element, string targetType, List<XElement> results)
    {
        if (element.Name.LocalName.Equals(targetType, StringComparison.OrdinalIgnoreCase))
        {
            results.Add(element);
        }

        // Continue searching in child elements
        foreach (var child in element.Elements())
        {
            FindElementsByTypeRecursive(child, targetType, results);
        }
    }

    /// <summary>
    /// Gets the appropriate XName for an attribute, handling namespaces.
    /// </summary>
    private XName GetAttributeName(string propertyName)
    {
        // Handle common namespace prefixes
        if (propertyName.StartsWith("x:"))
        {
            var localName = propertyName.Substring(2);
            return XName.Get(localName, "http://schemas.microsoft.com/winfx/2006/xaml");
        }

        // Handle other namespace prefixes if needed in the future
        return XName.Get(propertyName);
    }

    /// <summary>
    /// Safely saves an XDocument to a file with proper formatting and security.
    /// </summary>
    private async Task SaveDocumentSafelyAsync(XDocument document, string filePath)
    {
        try
        {
            // Create backup before saving
            var backupPath = await CreateBackupAsync(filePath).ConfigureAwait(false);
            
            // Configure save options for clean formatting
            var saveOptions = SaveOptions.None; // Preserve formatting

            // Save to a temporary file first, then move to target
            var tempPath = $"{filePath}.tmp.{Guid.NewGuid():N}";
            
            try
            {
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var streamWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8);
                
                document.Save(streamWriter, saveOptions);
                await streamWriter.FlushAsync().ConfigureAwait(false);
                
                // Atomically replace the original file
                File.Move(tempPath, filePath, overwrite: true);
                
                _logger.LogDebug("Successfully saved XAML document: {FilePath}", filePath);
            }
            catch (Exception)
            {
                // Clean up temporary file if something went wrong
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* Ignore cleanup errors */ }
                }
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving XAML document: {FilePath}", filePath);
            throw;
        }
    }
}