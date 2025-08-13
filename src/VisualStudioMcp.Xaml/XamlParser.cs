using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace VisualStudioMcp.Xaml;

/// <summary>
/// Handles XAML file parsing and visual tree analysis.
/// Responsible for extracting elements, properties, and structure from XAML files.
/// </summary>
public class XamlParser
{
    private readonly ILogger<XamlParser> _logger;
    private readonly Dictionary<string, XDocument> _documentCache;
    private readonly object _cacheLock = new();

    /// <summary>
    /// Initializes a new instance of the XamlParser class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public XamlParser(ILogger<XamlParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _documentCache = new Dictionary<string, XDocument>();
    }

    /// <summary>
    /// Parses a XAML file and extracts the visual tree elements.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Array of visual tree elements.</returns>
    public async Task<XamlElement[]> ParseVisualTreeAsync(string xamlFilePath)
    {
        _logger.LogInformation("Parsing visual tree from XAML file: {FilePath}", xamlFilePath);

        try
        {
            if (!SecureXmlHelper.IsFilePathSafe(xamlFilePath))
            {
                _logger.LogWarning("Unsafe file path rejected: {FilePath}", xamlFilePath);
                return Array.Empty<XamlElement>();
            }

            var document = await LoadXamlDocumentAsync(xamlFilePath).ConfigureAwait(false);
            if (document?.Root == null)
            {
                _logger.LogWarning("Failed to load XAML document or document has no root: {FilePath}", xamlFilePath);
                return Array.Empty<XamlElement>();
            }

            var elements = ExtractElementsFromDocument(document);
            _logger.LogInformation("Extracted {Count} elements from XAML file: {FilePath}", elements.Length, xamlFilePath);
            
            return elements;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing visual tree from XAML file: {FilePath}", xamlFilePath);
            return Array.Empty<XamlElement>();
        }
    }

    /// <summary>
    /// Gets the root element from a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Root element information, or null if not found.</returns>
    public async Task<XamlElement?> GetRootElementAsync(string xamlFilePath)
    {
        _logger.LogInformation("Getting root element from XAML file: {FilePath}", xamlFilePath);

        try
        {
            if (!SecureXmlHelper.IsFilePathSafe(xamlFilePath))
            {
                _logger.LogWarning("Unsafe file path rejected: {FilePath}", xamlFilePath);
                return null;
            }

            var document = await LoadXamlDocumentAsync(xamlFilePath).ConfigureAwait(false);
            if (document?.Root == null)
            {
                return null;
            }

            return CreateXamlElementFromXElement(document.Root, null, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting root element from XAML file: {FilePath}", xamlFilePath);
            return null;
        }
    }

    /// <summary>
    /// Searches for elements by name within a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementName">Name to search for (x:Name or Name attribute).</param>
    /// <returns>Array of matching elements.</returns>
    public async Task<XamlElement[]> FindElementsByNameAsync(string xamlFilePath, string elementName)
    {
        _logger.LogInformation("Finding elements by name '{ElementName}' in XAML file: {FilePath}", elementName, xamlFilePath);

        try
        {
            if (!SecureXmlHelper.IsFilePathSafe(xamlFilePath))
            {
                _logger.LogWarning("Unsafe file path rejected: {FilePath}", xamlFilePath);
                return Array.Empty<XamlElement>();
            }

            if (string.IsNullOrWhiteSpace(elementName))
            {
                return Array.Empty<XamlElement>();
            }

            var document = await LoadXamlDocumentAsync(xamlFilePath).ConfigureAwait(false);
            if (document?.Root == null)
            {
                return Array.Empty<XamlElement>();
            }

            var matchingElements = new List<XamlElement>();
            FindElementsByNameRecursive(document.Root, elementName, matchingElements, null, 0);

            _logger.LogInformation("Found {Count} elements with name '{ElementName}' in XAML file: {FilePath}", 
                matchingElements.Count, elementName, xamlFilePath);

            return matchingElements.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding elements by name '{ElementName}' in XAML file: {FilePath}", elementName, xamlFilePath);
            return Array.Empty<XamlElement>();
        }
    }

    /// <summary>
    /// Searches for elements by type within a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementType">Type to search for (local name without namespace).</param>
    /// <returns>Array of matching elements.</returns>
    public async Task<XamlElement[]> FindElementsByTypeAsync(string xamlFilePath, string elementType)
    {
        _logger.LogInformation("Finding elements by type '{ElementType}' in XAML file: {FilePath}", elementType, xamlFilePath);

        try
        {
            if (!SecureXmlHelper.IsFilePathSafe(xamlFilePath))
            {
                _logger.LogWarning("Unsafe file path rejected: {FilePath}", xamlFilePath);
                return Array.Empty<XamlElement>();
            }

            if (string.IsNullOrWhiteSpace(elementType))
            {
                return Array.Empty<XamlElement>();
            }

            var document = await LoadXamlDocumentAsync(xamlFilePath).ConfigureAwait(false);
            if (document?.Root == null)
            {
                return Array.Empty<XamlElement>();
            }

            var matchingElements = new List<XamlElement>();
            FindElementsByTypeRecursive(document.Root, elementType, matchingElements, null, 0);

            _logger.LogInformation("Found {Count} elements of type '{ElementType}' in XAML file: {FilePath}", 
                matchingElements.Count, elementType, xamlFilePath);

            return matchingElements.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding elements by type '{ElementType}' in XAML file: {FilePath}", elementType, xamlFilePath);
            return Array.Empty<XamlElement>();
        }
    }

    /// <summary>
    /// Invalidates the cache for a specific XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file to invalidate.</param>
    public void InvalidateCache(string xamlFilePath)
    {
        lock (_cacheLock)
        {
            if (_documentCache.Remove(xamlFilePath))
            {
                _logger.LogDebug("Invalidated cache for XAML file: {FilePath}", xamlFilePath);
            }
        }
    }

    /// <summary>
    /// Clears all cached XAML documents.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            var count = _documentCache.Count;
            _documentCache.Clear();
            _logger.LogInformation("Cleared XAML document cache ({Count} documents)", count);
        }
    }

    /// <summary>
    /// Loads a XAML document with caching and secure parsing.
    /// </summary>
    private async Task<XDocument?> LoadXamlDocumentAsync(string xamlFilePath)
    {
        // Check cache first
        lock (_cacheLock)
        {
            if (_documentCache.TryGetValue(xamlFilePath, out var cachedDocument))
            {
                _logger.LogDebug("Using cached XAML document: {FilePath}", xamlFilePath);
                return cachedDocument;
            }
        }

        try
        {
            if (!File.Exists(xamlFilePath))
            {
                _logger.LogWarning("XAML file not found: {FilePath}", xamlFilePath);
                return null;
            }

            var xamlContent = await File.ReadAllTextAsync(xamlFilePath).ConfigureAwait(false);
            var document = SecureXmlHelper.ParseXamlSecurely(xamlContent);

            // Cache the document
            lock (_cacheLock)
            {
                _documentCache[xamlFilePath] = document;
                _logger.LogDebug("Cached XAML document: {FilePath}", xamlFilePath);
            }

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading XAML document: {FilePath}", xamlFilePath);
            return null;
        }
    }

    /// <summary>
    /// Extracts all elements from a XAML document.
    /// </summary>
    private XamlElement[] ExtractElementsFromDocument(XDocument document)
    {
        var elements = new List<XamlElement>();
        
        if (document.Root != null)
        {
            ExtractElementsRecursive(document.Root, elements, null, 0);
        }

        return elements.ToArray();
    }

    /// <summary>
    /// Recursively extracts elements from the XAML tree.
    /// </summary>
    private void ExtractElementsRecursive(XElement element, List<XamlElement> elements, XamlElement? parent, int depth)
    {
        try
        {
            var xamlElement = CreateXamlElementFromXElement(element, parent, depth);
            elements.Add(xamlElement);

            foreach (var child in element.Elements())
            {
                ExtractElementsRecursive(child, elements, xamlElement, depth + 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting element: {ElementName}", element.Name.LocalName);
        }
    }

    /// <summary>
    /// Creates a XamlElement from an XElement.
    /// </summary>
    private XamlElement CreateXamlElementFromXElement(XElement element, XamlElement? parent, int depth)
    {
        var xamlElement = new XamlElement
        {
            Name = element.Name.LocalName,
            Namespace = element.Name.Namespace.NamespaceName,
            ElementType = element.Name.LocalName,
            Parent = parent,
            Depth = depth,
            Properties = new Dictionary<string, string>()
        };

        // Extract attributes as properties
        foreach (var attribute in element.Attributes())
        {
            // Skip namespace declarations
            if (attribute.IsNamespaceDeclaration)
                continue;

            var propertyName = attribute.Name.LocalName;
            if (!string.IsNullOrEmpty(attribute.Name.Namespace.NamespaceName))
            {
                propertyName = $"{attribute.Name.Namespace.NamespaceName}:{propertyName}";
            }

            xamlElement.Properties[propertyName] = attribute.Value;
        }

        // Set special properties
        if (xamlElement.Properties.TryGetValue("Name", out var nameValue) || 
            xamlElement.Properties.TryGetValue("x:Name", out nameValue))
        {
            xamlElement.ElementName = nameValue;
        }

        // Extract text content if present
        if (!element.HasElements && !string.IsNullOrWhiteSpace(element.Value))
        {
            xamlElement.Properties["Content"] = element.Value.Trim();
        }

        return xamlElement;
    }

    /// <summary>
    /// Recursively finds elements by name.
    /// </summary>
    private void FindElementsByNameRecursive(XElement element, string targetName, List<XamlElement> results, XamlElement? parent, int depth)
    {
        try
        {
            // Check Name and x:Name attributes
            var nameAttr = element.Attribute("Name")?.Value ?? element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value;
            
            if (!string.IsNullOrEmpty(nameAttr) && nameAttr.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            {
                var xamlElement = CreateXamlElementFromXElement(element, parent, depth);
                results.Add(xamlElement);
            }

            // Continue searching in child elements
            foreach (var child in element.Elements())
            {
                var xamlParent = nameAttr == targetName ? CreateXamlElementFromXElement(element, parent, depth) : parent;
                FindElementsByNameRecursive(child, targetName, results, xamlParent, depth + 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching element by name: {ElementName}", element.Name.LocalName);
        }
    }

    /// <summary>
    /// Recursively finds elements by type.
    /// </summary>
    private void FindElementsByTypeRecursive(XElement element, string targetType, List<XamlElement> results, XamlElement? parent, int depth)
    {
        try
        {
            if (element.Name.LocalName.Equals(targetType, StringComparison.OrdinalIgnoreCase))
            {
                var xamlElement = CreateXamlElementFromXElement(element, parent, depth);
                results.Add(xamlElement);
            }

            // Continue searching in child elements
            foreach (var child in element.Elements())
            {
                var xamlParent = element.Name.LocalName.Equals(targetType, StringComparison.OrdinalIgnoreCase) ? 
                    CreateXamlElementFromXElement(element, parent, depth) : parent;
                FindElementsByTypeRecursive(child, targetType, results, xamlParent, depth + 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching element by type: {ElementName}", element.Name.LocalName);
        }
    }
}