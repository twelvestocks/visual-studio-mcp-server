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
    private readonly Dictionary<string, DocumentIndex> _indexCache;
    private readonly object _cacheLock = new();

    /// <summary>
    /// Initializes a new instance of the XamlParser class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public XamlParser(ILogger<XamlParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _documentCache = new Dictionary<string, XDocument>();
        _indexCache = new Dictionary<string, DocumentIndex>();
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

            // Try to use indexed lookup first
            var index = await GetDocumentIndexAsync(xamlFilePath).ConfigureAwait(false);
            if (index != null && index.ElementsByName.TryGetValue(elementName, out var indexedElements))
            {
                _logger.LogDebug("Using indexed lookup for elements with name '{ElementName}': found {Count} elements", 
                    elementName, indexedElements.Count);
                return indexedElements.ToArray();
            }

            // Fallback to document parsing if index is not available
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

            // Try to use indexed lookup first
            var index = await GetDocumentIndexAsync(xamlFilePath).ConfigureAwait(false);
            if (index != null && index.ElementsByType.TryGetValue(elementType, out var indexedElements))
            {
                _logger.LogDebug("Using indexed lookup for elements of type '{ElementType}': found {Count} elements", 
                    elementType, indexedElements.Count);
                return indexedElements.ToArray();
            }

            // Fallback to document parsing if index is not available
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
            var docRemoved = _documentCache.Remove(xamlFilePath);
            var indexRemoved = _indexCache.Remove(xamlFilePath);
            
            if (docRemoved || indexRemoved)
            {
                _logger.LogDebug("Invalidated cache for XAML file: {FilePath} (doc: {DocRemoved}, index: {IndexRemoved})", 
                    xamlFilePath, docRemoved, indexRemoved);
            }
        }
    }

    /// <summary>
    /// Clears all cached XAML documents and indexes.
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            var docCount = _documentCache.Count;
            var indexCount = _indexCache.Count;
            _documentCache.Clear();
            _indexCache.Clear();
            _logger.LogInformation("Cleared XAML document cache ({DocCount} documents, {IndexCount} indexes)", 
                docCount, indexCount);
        }
    }

    /// <summary>
    /// Gets statistics about all cached indexes.
    /// </summary>
    public IndexStatistics[] GetIndexStatistics()
    {
        lock (_cacheLock)
        {
            return _indexCache.Values.Select(index => index.GetStatistics()).ToArray();
        }
    }

    /// <summary>
    /// Gets index statistics for a specific file.
    /// </summary>
    public async Task<IndexStatistics?> GetIndexStatisticsAsync(string xamlFilePath)
    {
        var index = await GetDocumentIndexAsync(xamlFilePath).ConfigureAwait(false);
        return index?.GetStatistics();
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

    /// <summary>
    /// Gets or creates a document index for fast lookups.
    /// </summary>
    private async Task<DocumentIndex?> GetDocumentIndexAsync(string xamlFilePath)
    {
        // Check cache first
        lock (_cacheLock)
        {
            if (_indexCache.TryGetValue(xamlFilePath, out var cachedIndex))
            {
                _logger.LogDebug("Using cached index for XAML file: {FilePath}", xamlFilePath);
                return cachedIndex;
            }
        }

        try
        {
            // Load document if needed
            var document = await LoadXamlDocumentAsync(xamlFilePath).ConfigureAwait(false);
            if (document?.Root == null)
            {
                return null;
            }

            // Build index
            var index = BuildDocumentIndex(document, xamlFilePath);

            // Cache the index
            lock (_cacheLock)
            {
                _indexCache[xamlFilePath] = index;
                _logger.LogDebug("Built and cached index for XAML file: {FilePath} ({NameCount} names, {TypeCount} types)", 
                    xamlFilePath, index.ElementsByName.Count, index.ElementsByType.Count);
            }

            return index;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building document index for XAML file: {FilePath}", xamlFilePath);
            return null;
        }
    }

    /// <summary>
    /// Builds a document index for efficient element lookups.
    /// </summary>
    private DocumentIndex BuildDocumentIndex(XDocument document, string filePath)
    {
        var index = new DocumentIndex(filePath);
        
        if (document.Root != null)
        {
            BuildIndexRecursive(document.Root, index, null, 0);
        }

        return index;
    }

    /// <summary>
    /// Recursively builds the document index from the XAML tree.
    /// </summary>
    private void BuildIndexRecursive(XElement element, DocumentIndex index, XamlElement? parent, int depth)
    {
        try
        {
            var xamlElement = CreateXamlElementFromXElement(element, parent, depth);
            
            // Index by name if element has a name
            if (!string.IsNullOrEmpty(xamlElement.ElementName))
            {
                if (!index.ElementsByName.ContainsKey(xamlElement.ElementName))
                {
                    index.ElementsByName[xamlElement.ElementName] = new List<XamlElement>();
                }
                index.ElementsByName[xamlElement.ElementName].Add(xamlElement);
            }

            // Index by type
            var elementType = xamlElement.ElementType;
            if (!index.ElementsByType.ContainsKey(elementType))
            {
                index.ElementsByType[elementType] = new List<XamlElement>();
            }
            index.ElementsByType[elementType].Add(xamlElement);

            // Add to all elements collection
            index.AllElements.Add(xamlElement);

            // Continue with child elements
            foreach (var child in element.Elements())
            {
                BuildIndexRecursive(child, index, xamlElement, depth + 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error indexing element: {ElementName}", element.Name.LocalName);
        }
    }
}

/// <summary>
/// Index structure for fast element lookups in a XAML document.
/// </summary>
internal class DocumentIndex
{
    /// <summary>
    /// File path this index represents.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Elements indexed by their name (x:Name or Name attribute).
    /// </summary>
    public Dictionary<string, List<XamlElement>> ElementsByName { get; }

    /// <summary>
    /// Elements indexed by their type (local name).
    /// </summary>
    public Dictionary<string, List<XamlElement>> ElementsByType { get; }

    /// <summary>
    /// All elements in the document.
    /// </summary>
    public List<XamlElement> AllElements { get; }

    /// <summary>
    /// When this index was created.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Initializes a new DocumentIndex.
    /// </summary>
    public DocumentIndex(string filePath)
    {
        FilePath = filePath;
        ElementsByName = new Dictionary<string, List<XamlElement>>(StringComparer.OrdinalIgnoreCase);
        ElementsByType = new Dictionary<string, List<XamlElement>>(StringComparer.OrdinalIgnoreCase);
        AllElements = new List<XamlElement>();
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets statistics about this index.
    /// </summary>
    public IndexStatistics GetStatistics()
    {
        return new IndexStatistics
        {
            FilePath = FilePath,
            TotalElements = AllElements.Count,
            NamedElements = ElementsByName.Values.Sum(list => list.Count),
            UniqueElementTypes = ElementsByType.Count,
            UniqueElementNames = ElementsByName.Count,
            CreatedAt = CreatedAt
        };
    }
}

/// <summary>
/// Statistics about a document index.
/// </summary>
public class IndexStatistics
{
    /// <summary>
    /// File path this index represents.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Total number of elements in the document.
    /// </summary>
    public int TotalElements { get; set; }

    /// <summary>
    /// Number of elements that have names.
    /// </summary>
    public int NamedElements { get; set; }

    /// <summary>
    /// Number of unique element types.
    /// </summary>
    public int UniqueElementTypes { get; set; }

    /// <summary>
    /// Number of unique element names.
    /// </summary>
    public int UniqueElementNames { get; set; }

    /// <summary>
    /// When this index was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}