using Microsoft.Extensions.Logging;
using VisualStudioMcp.Core;

namespace VisualStudioMcp.Xaml;

/// <summary>
/// Orchestrates XAML designer automation services using focused, single-responsibility classes.
/// This service delegates operations to specialized components following the SRP pattern.
/// </summary>
public class XamlDesignerService : IXamlDesignerService
{
    private readonly ILogger<XamlDesignerService> _logger;
    private readonly IVisualStudioService _vsService;
    private readonly XamlWindowManager _windowManager;
    private readonly XamlParser _parser;
    private readonly XamlBindingAnalyser _bindingAnalyser;
    private readonly XamlPropertyModifier _propertyModifier;

    /// <summary>
    /// Initializes a new instance of the XamlDesignerService class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic and debugging information.</param>
    /// <param name="vsService">Visual Studio service for COM automation.</param>
    /// <param name="windowManager">Window management service.</param>
    /// <param name="parser">XAML parsing service.</param>
    /// <param name="bindingAnalyser">Binding analysis service.</param>
    /// <param name="propertyModifier">Property modification service.</param>
    public XamlDesignerService(
        ILogger<XamlDesignerService> logger,
        IVisualStudioService vsService,
        XamlWindowManager windowManager,
        XamlParser parser,
        XamlBindingAnalyser bindingAnalyser,
        XamlPropertyModifier propertyModifier)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _vsService = vsService ?? throw new ArgumentNullException(nameof(vsService));
        _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _bindingAnalyser = bindingAnalyser ?? throw new ArgumentNullException(nameof(bindingAnalyser));
        _propertyModifier = propertyModifier ?? throw new ArgumentNullException(nameof(propertyModifier));
    }

    /// <summary>
    /// Finds XAML designer windows in the Visual Studio process.
    /// </summary>
    /// <param name="vsProcessId">Visual Studio process ID.</param>
    /// <returns>List of designer window information.</returns>
    public async Task<XamlDesignerWindow[]> FindXamlDesignerWindowsAsync(int vsProcessId)
    {
        _logger.LogDebug("Delegating FindXamlDesignerWindowsAsync to XamlWindowManager");
        return await _windowManager.FindXamlDesignerWindowsAsync(vsProcessId).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the active XAML designer window for the currently active document.
    /// </summary>
    /// <returns>Designer window information if found, null otherwise.</returns>
    public async Task<XamlDesignerWindow?> GetActiveDesignerWindowAsync()
    {
        _logger.LogDebug("Delegating GetActiveDesignerWindowAsync to XamlWindowManager");
        return await _windowManager.GetActiveDesignerWindowAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Activates a specific XAML designer window.
    /// </summary>
    /// <param name="designerWindow">The designer window to activate.</param>
    /// <returns>True if successfully activated, false otherwise.</returns>
    public async Task<bool> ActivateDesignerWindowAsync(XamlDesignerWindow designerWindow)
    {
        _logger.LogDebug("Delegating ActivateDesignerWindowAsync to XamlWindowManager");
        return await _windowManager.ActivateDesignerWindowAsync(designerWindow).ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts the visual tree elements from a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Array of visual tree elements.</returns>
    public async Task<XamlElement[]> ExtractVisualTreeAsync(string xamlFilePath)
    {
        _logger.LogDebug("Delegating ExtractVisualTreeAsync to XamlParser");
        return await _parser.ParseVisualTreeAsync(xamlFilePath).ConfigureAwait(false);
    }

    /// <summary>
    /// Modifies a property on elements with the specified name in a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementName">Name of the element to modify.</param>
    /// <param name="propertyName">Name of the property to modify.</param>
    /// <param name="propertyValue">New value for the property.</param>
    /// <returns>True if successfully modified, false otherwise.</returns>
    public async Task<bool> ModifyElementPropertyAsync(string xamlFilePath, string elementName, string propertyName, string propertyValue)
    {
        _logger.LogDebug("Delegating ModifyElementPropertyAsync to XamlPropertyModifier");
        return await _propertyModifier.ModifyElementPropertyAsync(xamlFilePath, elementName, propertyName, propertyValue).ConfigureAwait(false);
    }

    /// <summary>
    /// Analyses data bindings in a XAML file and extracts binding information.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Array of binding information.</returns>
    public async Task<XamlBindingInfo[]> AnalyseDataBindingsAsync(string xamlFilePath)
    {
        _logger.LogDebug("Delegating AnalyseDataBindingsAsync to XamlBindingAnalyser");
        return await _bindingAnalyser.AnalyseDataBindingsAsync(xamlFilePath).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates data bindings in a XAML file and returns validation results.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Array of validation results.</returns>
    public async Task<BindingValidationResult[]> ValidateDataBindingsAsync(string xamlFilePath)
    {
        _logger.LogDebug("Delegating ValidateDataBindingsAsync to XamlBindingAnalyser");
        return await _bindingAnalyser.ValidateDataBindingsAsync(xamlFilePath).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets comprehensive statistics about binding usage in a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Binding usage statistics.</returns>
    public async Task<BindingStatistics> GetBindingStatisticsAsync(string xamlFilePath)
    {
        _logger.LogDebug("Delegating GetBindingStatisticsAsync to XamlBindingAnalyser");
        return await _bindingAnalyser.GetBindingStatisticsAsync(xamlFilePath).ConfigureAwait(false);
    }

    /// <summary>
    /// Searches for elements by name within a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementName">Name to search for.</param>
    /// <returns>Array of matching elements.</returns>
    public async Task<XamlElement[]> FindElementsByNameAsync(string xamlFilePath, string elementName)
    {
        _logger.LogDebug("Delegating FindElementsByNameAsync to XamlParser");
        return await _parser.FindElementsByNameAsync(xamlFilePath, elementName).ConfigureAwait(false);
    }

    /// <summary>
    /// Searches for elements by type within a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementType">Type to search for.</param>
    /// <returns>Array of matching elements.</returns>
    public async Task<XamlElement[]> FindElementsByTypeAsync(string xamlFilePath, string elementType)
    {
        _logger.LogDebug("Delegating FindElementsByTypeAsync to XamlParser");
        return await _parser.FindElementsByTypeAsync(xamlFilePath, elementType).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a new property to elements with the specified name.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementName">Name of the element to modify.</param>
    /// <param name="propertyName">Name of the property to add.</param>
    /// <param name="propertyValue">Value for the new property.</param>
    /// <returns>True if the property was added successfully, false otherwise.</returns>
    public async Task<bool> AddElementPropertyAsync(string xamlFilePath, string elementName, string propertyName, string propertyValue)
    {
        _logger.LogDebug("Delegating AddElementPropertyAsync to XamlPropertyModifier");
        return await _propertyModifier.AddElementPropertyAsync(xamlFilePath, elementName, propertyName, propertyValue).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a property from elements with the specified name.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementName">Name of the element to modify.</param>
    /// <param name="propertyName">Name of the property to remove.</param>
    /// <returns>True if the property was removed successfully, false otherwise.</returns>
    public async Task<bool> RemoveElementPropertyAsync(string xamlFilePath, string elementName, string propertyName)
    {
        _logger.LogDebug("Delegating RemoveElementPropertyAsync to XamlPropertyModifier");
        return await _propertyModifier.RemoveElementPropertyAsync(xamlFilePath, elementName, propertyName).ConfigureAwait(false);
    }

    /// <summary>
    /// Modifies properties for all elements of a specific type.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementType">Type of elements to modify.</param>
    /// <param name="propertyName">Name of the property to modify.</param>
    /// <param name="propertyValue">New value for the property.</param>
    /// <returns>True if the modification was successful, false otherwise.</returns>
    public async Task<bool> ModifyElementsByTypeAsync(string xamlFilePath, string elementType, string propertyName, string propertyValue)
    {
        _logger.LogDebug("Delegating ModifyElementsByTypeAsync to XamlPropertyModifier");
        return await _propertyModifier.ModifyElementsByTypeAsync(xamlFilePath, elementType, propertyName, propertyValue).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds all elements in a XAML file that have data binding expressions.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Array of elements with bindings.</returns>
    public async Task<XamlElement[]> FindElementsWithBindingsAsync(string xamlFilePath)
    {
        _logger.LogDebug("Delegating FindElementsWithBindingsAsync to XamlBindingAnalyser");
        return await _bindingAnalyser.FindElementsWithBindingsAsync(xamlFilePath).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the root element from a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Root element information, or null if not found.</returns>
    public async Task<XamlElement?> GetRootElementAsync(string xamlFilePath)
    {
        _logger.LogDebug("Delegating GetRootElementAsync to XamlParser");
        return await _parser.GetRootElementAsync(xamlFilePath).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a backup of a XAML file before modification.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Path to the backup file, or null if backup failed.</returns>
    public async Task<string?> CreateBackupAsync(string xamlFilePath)
    {
        _logger.LogDebug("Delegating CreateBackupAsync to XamlPropertyModifier");
        return await _propertyModifier.CreateBackupAsync(xamlFilePath).ConfigureAwait(false);
    }

    /// <summary>
    /// Invalidates the parser cache for a specific XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file to invalidate.</param>
    public void InvalidateParserCache(string xamlFilePath)
    {
        _logger.LogDebug("Delegating InvalidateParserCache to XamlParser");
        _parser.InvalidateCache(xamlFilePath);
    }

    /// <summary>
    /// Clears all cached XAML documents in the parser.
    /// </summary>
    public void ClearParserCache()
    {
        _logger.LogDebug("Delegating ClearParserCache to XamlParser");
        _parser.ClearCache();
    }
}