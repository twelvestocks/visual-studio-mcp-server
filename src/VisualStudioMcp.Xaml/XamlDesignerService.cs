using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.Extensions.Logging;
using VisualStudioMcp.Core;

namespace VisualStudioMcp.Xaml;

/// <summary>
/// Implementation of XAML designer automation service.
/// </summary>
public class XamlDesignerService : IXamlDesignerService
{
    private readonly ILogger<XamlDesignerService> _logger;
    private readonly IVisualStudioService _vsService;

    // Windows API for window enumeration
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parentHwnd, IntPtr childAfterHwnd, string className, string windowText);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public XamlDesignerService(ILogger<XamlDesignerService> logger, IVisualStudioService vsService)
    {
        _logger = logger;
        _vsService = vsService;
    }

    /// <summary>
    /// Finds XAML designer windows in the Visual Studio process.
    /// </summary>
    /// <param name="vsProcessId">Visual Studio process ID.</param>
    /// <returns>List of designer window information.</returns>
    public async Task<XamlDesignerWindow[]> FindXamlDesignerWindowsAsync(int vsProcessId)
    {
        _logger.LogInformation("Finding XAML designer windows for VS process: {ProcessId}", vsProcessId);

        return await Task.Run(() =>
        {
            var designerWindows = new List<XamlDesignerWindow>();

            try
            {
                // Get running VS instances to find the correct DTE object
                var instances = _vsService.GetRunningInstancesAsync().Result;
                var targetInstance = instances.FirstOrDefault(i => i.ProcessId == vsProcessId);

                if (targetInstance == null)
                {
                    _logger.LogWarning("Visual Studio instance with PID {ProcessId} not found", vsProcessId);
                    return designerWindows.ToArray();
                }

                // Connect to the VS instance to get document information
                var vsInstance = _vsService.ConnectToInstanceAsync(vsProcessId).Result;
                
                // Find active XAML documents and their designer windows
                designerWindows.AddRange(FindDesignerWindowsForInstance(vsProcessId));

                _logger.LogInformation("Found {Count} XAML designer windows", designerWindows.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding XAML designer windows for process {ProcessId}", vsProcessId);
            }

            return designerWindows.ToArray();
        });
    }

    /// <summary>
    /// Gets the active XAML designer window for the currently active document.
    /// </summary>
    /// <returns>Designer window information if found, null otherwise.</returns>
    public async Task<XamlDesignerWindow?> GetActiveDesignerWindowAsync()
    {
        _logger.LogInformation("Getting active XAML designer window");

        try
        {
            var instances = await _vsService.GetRunningInstancesAsync();
            
            foreach (var instance in instances)
            {
                var designerWindows = await FindXamlDesignerWindowsAsync(instance.ProcessId);
                var activeDesigner = designerWindows.FirstOrDefault(w => w.IsActive);
                
                if (activeDesigner != null)
                {
                    _logger.LogInformation("Found active designer for file: {FilePath}", activeDesigner.XamlFilePath);
                    return activeDesigner;
                }
            }

            _logger.LogInformation("No active XAML designer window found");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active XAML designer window");
            return null;
        }
    }

    /// <summary>
    /// Activates a specific XAML designer window.
    /// </summary>
    /// <param name="designerWindow">The designer window to activate.</param>
    /// <returns>True if successfully activated, false otherwise.</returns>
    public async Task<bool> ActivateDesignerWindowAsync(XamlDesignerWindow designerWindow)
    {
        _logger.LogInformation("Activating XAML designer for: {FilePath}", designerWindow.XamlFilePath);

        return await Task.Run(() =>
        {
            try
            {
                // Use Visual Studio automation to activate the designer
                return ActivateDesignerViaAutomation(designerWindow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating XAML designer for {FilePath}", designerWindow.XamlFilePath);
                return false;
            }
        });
    }

    /// <summary>
    /// Finds designer windows for a specific Visual Studio instance.
    /// </summary>
    private List<XamlDesignerWindow> FindDesignerWindowsForInstance(int processId)
    {
        var designerWindows = new List<XamlDesignerWindow>();

        try
        {
            // Look for XAML designer window class names
            var designerClassNames = new[]
            {
                "Microsoft.XamlDesignerHost.DesignerPane",
                "XamlDesignerPane",
                "DesignerView",
                "Microsoft.VisualStudio.DesignTools.Xaml.DesignerPane"
            };

            foreach (var className in designerClassNames)
            {
                var hwnd = FindWindowEx(IntPtr.Zero, IntPtr.Zero, className, null);
                while (hwnd != IntPtr.Zero)
                {
                    if (IsWindowVisible(hwnd))
                    {
                        GetWindowThreadProcessId(hwnd, out var windowProcessId);
                        
                        if (windowProcessId == processId)
                        {
                            var windowText = GetWindowText(hwnd);
                            var designerWindow = CreateDesignerWindowInfo(hwnd, windowText, processId);
                            
                            if (designerWindow != null)
                            {
                                designerWindows.Add(designerWindow);
                                _logger.LogDebug("Found XAML designer window: {Title}", designerWindow.Title);
                            }
                        }
                    }

                    hwnd = FindWindowEx(IntPtr.Zero, hwnd, className, null);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enumerating designer windows for process {ProcessId}", processId);
        }

        return designerWindows;
    }

    /// <summary>
    /// Gets the text content of a window.
    /// </summary>
    private string GetWindowText(IntPtr hwnd)
    {
        var text = new System.Text.StringBuilder(256);
        GetWindowText(hwnd, text, text.Capacity);
        return text.ToString();
    }

    /// <summary>
    /// Creates designer window information from a window handle.
    /// </summary>
    private XamlDesignerWindow? CreateDesignerWindowInfo(IntPtr hwnd, string title, int processId)
    {
        try
        {
            // Try to determine the XAML file path from the window title or context
            var xamlFilePath = ExtractXamlFilePathFromContext(title, processId);
            
            return new XamlDesignerWindow
            {
                Handle = hwnd,
                Title = title,
                ProcessId = processId,
                XamlFilePath = xamlFilePath ?? "Unknown",
                IsActive = IsActiveWindow(hwnd),
                IsVisible = IsWindowVisible(hwnd)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error creating designer window info for handle {Handle}", hwnd);
            return null;
        }
    }

    /// <summary>
    /// Extracts XAML file path from window context using Visual Studio automation.
    /// </summary>
    private string? ExtractXamlFilePathFromContext(string windowTitle, int processId)
    {
        try
        {
            // This would need to use EnvDTE to get the active document
            // For now, return null and implement in subsequent tasks
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting XAML file path from context");
            return null;
        }
    }

    /// <summary>
    /// Checks if a window is currently active/focused.
    /// </summary>
    private bool IsActiveWindow(IntPtr hwnd)
    {
        // Simple implementation - could be enhanced with GetForegroundWindow
        return IsWindowVisible(hwnd);
    }

    /// <summary>
    /// Activates designer using Visual Studio automation.
    /// </summary>
    private bool ActivateDesignerViaAutomation(XamlDesignerWindow designerWindow)
    {
        try
        {
            // This would use EnvDTE to activate the designer
            // Implementation will be added when EnvDTE integration is complete
            _logger.LogInformation("Designer activation via automation not yet implemented");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in designer activation via automation");
            return false;
        }
    }

    /// <summary>
    /// Gets the DTE instance for the specified XAML file.
    /// </summary>
    private DTE? GetDteForXamlFile(string xamlFilePath)
    {
        try
        {
            var instances = _vsService.GetRunningInstancesAsync().Result;
            
            foreach (var instance in instances)
            {
                // Connect to instance and check if it has the XAML file open
                var vsInstance = _vsService.ConnectToInstanceAsync(instance.ProcessId).Result;
                
                // For now, just return the first available DTE instance
                // In a full implementation, we'd check which instance has the file open
                return GetDteFromInstance(instance.ProcessId);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting DTE for XAML file {FilePath}", xamlFilePath);
            return null;
        }
    }

    /// <summary>
    /// Gets DTE object from a Visual Studio instance.
    /// </summary>
    private DTE? GetDteFromInstance(int processId)
    {
        // This is a simplified version - would need proper COM ROT enumeration
        // For now, just indicate the pattern
        _logger.LogDebug("Getting DTE for process {ProcessId}", processId);
        return null; // Placeholder - would return actual DTE object
    }

    /// <summary>
    /// Extracts visual tree elements from XAML using DTE automation.
    /// </summary>
    private XamlElement[] ExtractVisualTreeFromXaml(DTE? dte, string xamlFilePath)
    {
        var elements = new List<XamlElement>();

        try
        {
            // Method 1: Parse XAML file directly
            if (File.Exists(xamlFilePath))
            {
                elements.AddRange(ParseXamlFileDirectly(xamlFilePath));
            }

            // Method 2: Use Visual Studio designer APIs (when available)
            elements.AddRange(ExtractFromDesignerApi(dte, xamlFilePath));

            _logger.LogInformation("Extracted {Count} XAML elements from {FilePath}", elements.Count, xamlFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting visual tree from {FilePath}", xamlFilePath);
        }

        return elements.ToArray();
    }

    /// <summary>
    /// Parses XAML file directly to extract element information.
    /// </summary>
    private List<XamlElement> ParseXamlFileDirectly(string xamlFilePath)
    {
        var elements = new List<XamlElement>();

        try
        {
            var xamlContent = File.ReadAllText(xamlFilePath);
            var doc = XDocument.Parse(xamlContent);

            if (doc.Root != null)
            {
                ParseXamlElementRecursively(doc.Root, "", elements);
            }

            _logger.LogDebug("Parsed {Count} elements from XAML file directly", elements.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing XAML file directly: {FilePath}", xamlFilePath);
        }

        return elements;
    }

    /// <summary>
    /// Recursively parses XAML elements and their hierarchy.
    /// </summary>
    private void ParseXamlElementRecursively(XElement xmlElement, string parentPath, List<XamlElement> elements)
    {
        try
        {
            var elementName = xmlElement.Attribute("Name")?.Value ?? xmlElement.Attribute("x:Name")?.Value;
            var elementType = xmlElement.Name.LocalName;
            var currentPath = string.IsNullOrEmpty(parentPath) 
                ? elementType 
                : $"{parentPath}.{elementType}";

            if (!string.IsNullOrEmpty(elementName))
            {
                currentPath = string.IsNullOrEmpty(parentPath) 
                    ? elementName 
                    : $"{parentPath}.{elementName}";
            }

            var xamlElement = new XamlElement
            {
                Name = elementName ?? $"[{elementType}]",
                Type = elementType,
                Path = currentPath
            };

            elements.Add(xamlElement);

            // Process child elements
            foreach (var child in xmlElement.Elements())
            {
                ParseXamlElementRecursively(child, currentPath, elements);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing XML element: {ElementName}", xmlElement.Name);
        }
    }

    /// <summary>
    /// Extracts elements from Visual Studio designer APIs (placeholder).
    /// </summary>
    private List<XamlElement> ExtractFromDesignerApi(DTE? dte, string xamlFilePath)
    {
        var elements = new List<XamlElement>();

        try
        {
            if (dte == null)
            {
                _logger.LogDebug("DTE is null, skipping designer API extraction");
                return elements;
            }

            // This would use Visual Studio's XAML designer APIs
            // For now, this is a placeholder since the exact APIs depend on VS version
            _logger.LogDebug("Designer API extraction not yet implemented for {FilePath}", xamlFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting from designer API: {FilePath}", xamlFilePath);
        }

        return elements;
    }

    public async Task<XamlDesignerCapture> CaptureDesignerAsync(string xamlFilePath)
    {
        _logger.LogInformation("Capturing XAML designer for: {XamlFile}", xamlFilePath);
        
        // TODO: Implement XAML designer capture
        await Task.Delay(10); // Placeholder
        
        throw new NotImplementedException("XAML designer capture not yet implemented");
    }

    public async Task<XamlElement[]> GetVisualTreeAsync(string xamlFilePath)
    {
        _logger.LogInformation("Getting visual tree for: {XamlFile}", xamlFilePath);
        
        return await Task.Run(() =>
        {
            try
            {
                var dte = GetDteForXamlFile(xamlFilePath);
                return ExtractVisualTreeFromXaml(dte, xamlFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting visual tree for {XamlFile}", xamlFilePath);
                return Array.Empty<XamlElement>();
            }
        });
    }

    public async Task<XamlProperty[]> GetElementPropertiesAsync(string elementPath)
    {
        _logger.LogInformation("Getting properties for element: {ElementPath}", elementPath);
        
        return await Task.Run(() =>
        {
            try
            {
                return ExtractElementProperties(elementPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting properties for element {ElementPath}", elementPath);
                return Array.Empty<XamlProperty>();
            }
        });
    }

    /// <summary>
    /// Extracts properties for a specific XAML element.
    /// </summary>
    private XamlProperty[] ExtractElementProperties(string elementPath)
    {
        var properties = new List<XamlProperty>();

        try
        {
            // Parse element path to determine element type and find XAML file
            var pathParts = elementPath.Split('.');
            if (pathParts.Length == 0)
            {
                _logger.LogWarning("Invalid element path format: {ElementPath}", elementPath);
                return properties.ToArray();
            }

            // For now, return common XAML properties based on element type
            var elementType = pathParts.Last();
            properties.AddRange(GetCommonPropertiesForElementType(elementType));

            _logger.LogDebug("Extracted {Count} properties for element {ElementPath}", properties.Count, elementPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting properties for element {ElementPath}", elementPath);
        }

        return properties.ToArray();
    }

    /// <summary>
    /// Gets common properties for a specific XAML element type.
    /// </summary>
    private List<XamlProperty> GetCommonPropertiesForElementType(string elementType)
    {
        var properties = new List<XamlProperty>();

        // Add common properties that apply to all FrameworkElements
        properties.AddRange(new[]
        {
            new XamlProperty { Name = "Name", Type = "string", Value = null },
            new XamlProperty { Name = "Width", Type = "double", Value = double.NaN },
            new XamlProperty { Name = "Height", Type = "double", Value = double.NaN },
            new XamlProperty { Name = "Margin", Type = "Thickness", Value = null },
            new XamlProperty { Name = "HorizontalAlignment", Type = "HorizontalAlignment", Value = "Stretch" },
            new XamlProperty { Name = "VerticalAlignment", Type = "VerticalAlignment", Value = "Stretch" },
            new XamlProperty { Name = "Visibility", Type = "Visibility", Value = "Visible" }
        });

        // Add element-type specific properties
        switch (elementType.ToLower())
        {
            case "button":
                properties.AddRange(new[]
                {
                    new XamlProperty { Name = "Content", Type = "object", Value = null },
                    new XamlProperty { Name = "IsEnabled", Type = "bool", Value = true },
                    new XamlProperty { Name = "Command", Type = "ICommand", Value = null }
                });
                break;

            case "textbox":
                properties.AddRange(new[]
                {
                    new XamlProperty { Name = "Text", Type = "string", Value = "" },
                    new XamlProperty { Name = "IsReadOnly", Type = "bool", Value = false },
                    new XamlProperty { Name = "AcceptsReturn", Type = "bool", Value = false }
                });
                break;

            case "label":
            case "textblock":
                properties.AddRange(new[]
                {
                    new XamlProperty { Name = "Text", Type = "string", Value = "" },
                    new XamlProperty { Name = "FontSize", Type = "double", Value = 12.0 },
                    new XamlProperty { Name = "FontFamily", Type = "FontFamily", Value = "Segoe UI" }
                });
                break;

            case "grid":
                properties.AddRange(new[]
                {
                    new XamlProperty { Name = "RowDefinitions", Type = "RowDefinitionCollection", Value = null },
                    new XamlProperty { Name = "ColumnDefinitions", Type = "ColumnDefinitionCollection", Value = null }
                });
                break;

            case "stackpanel":
                properties.AddRange(new[]
                {
                    new XamlProperty { Name = "Orientation", Type = "Orientation", Value = "Vertical" }
                });
                break;

            case "image":
                properties.AddRange(new[]
                {
                    new XamlProperty { Name = "Source", Type = "ImageSource", Value = null },
                    new XamlProperty { Name = "Stretch", Type = "Stretch", Value = "Uniform" }
                });
                break;
        }

        return properties;
    }

    public async Task ModifyElementPropertyAsync(string elementPath, string property, object value)
    {
        _logger.LogInformation("Modifying property {Property} of element {ElementPath} to {Value}", 
            property, elementPath, value);
        
        await Task.Run(() =>
        {
            try
            {
                ModifyElementPropertyInternal(elementPath, property, value);
                _logger.LogInformation("Successfully modified property {Property} of element {ElementPath}", property, elementPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error modifying property {Property} of element {ElementPath}", property, elementPath);
                throw;
            }
        });
    }

    /// <summary>
    /// Internal method to modify an element property.
    /// </summary>
    private void ModifyElementPropertyInternal(string elementPath, string property, object value)
    {
        try
        {
            // Method 1: Direct XAML file modification
            var xamlFilePath = FindXamlFileForElement(elementPath);
            if (!string.IsNullOrEmpty(xamlFilePath) && File.Exists(xamlFilePath))
            {
                ModifyPropertyInXamlFile(xamlFilePath, elementPath, property, value);
                _logger.LogDebug("Modified property via direct XAML file modification");
                return;
            }

            // Method 2: Visual Studio designer API (when available)
            ModifyPropertyViaDesignerApi(elementPath, property, value);
            _logger.LogDebug("Modified property via VS designer API");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in property modification for {ElementPath}.{Property}", elementPath, property);
            throw;
        }
    }

    /// <summary>
    /// Finds the XAML file that contains the specified element.
    /// </summary>
    private string FindXamlFileForElement(string elementPath)
    {
        try
        {
            // For now, this is a simplified implementation
            // In a full implementation, we would track which file each element came from
            var instances = _vsService.GetRunningInstancesAsync().Result;
            
            foreach (var instance in instances)
            {
                // Check open documents in VS instance
                var potentialFiles = GetOpenXamlFilesInInstance(instance.ProcessId);
                
                foreach (var filePath in potentialFiles)
                {
                    if (ElementExistsInXamlFile(filePath, elementPath))
                    {
                        return filePath;
                    }
                }
            }

            _logger.LogWarning("Could not find XAML file for element: {ElementPath}", elementPath);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding XAML file for element {ElementPath}", elementPath);
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets open XAML files in a Visual Studio instance.
    /// </summary>
    private List<string> GetOpenXamlFilesInInstance(int processId)
    {
        var xamlFiles = new List<string>();

        try
        {
            // This would use EnvDTE to enumerate open documents
            // For now, return empty list as placeholder
            _logger.LogDebug("Getting open XAML files for process {ProcessId} not yet implemented", processId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting open XAML files for process {ProcessId}", processId);
        }

        return xamlFiles;
    }

    /// <summary>
    /// Checks if an element exists in a XAML file.
    /// </summary>
    private bool ElementExistsInXamlFile(string xamlFilePath, string elementPath)
    {
        try
        {
            if (!File.Exists(xamlFilePath))
                return false;

            var xamlContent = File.ReadAllText(xamlFilePath);
            var doc = XDocument.Parse(xamlContent);

            if (doc.Root == null)
                return false;

            // Simple check - look for elements with matching names or paths
            var pathParts = elementPath.Split('.');
            var targetName = pathParts.Last();

            return doc.Descendants()
                .Any(element => 
                    element.Attribute("Name")?.Value == targetName ||
                    element.Attribute("x:Name")?.Value == targetName ||
                    element.Name.LocalName == targetName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if element exists in XAML file {FilePath}", xamlFilePath);
            return false;
        }
    }

    /// <summary>
    /// Modifies a property directly in the XAML file.
    /// </summary>
    private void ModifyPropertyInXamlFile(string xamlFilePath, string elementPath, string property, object value)
    {
        try
        {
            var xamlContent = File.ReadAllText(xamlFilePath);
            var doc = XDocument.Parse(xamlContent);

            if (doc.Root == null)
            {
                _logger.LogError("Invalid XAML file: {FilePath}", xamlFilePath);
                return;
            }

            var pathParts = elementPath.Split('.');
            var targetName = pathParts.Last();

            // Find the target element
            var targetElement = doc.Descendants()
                .FirstOrDefault(element => 
                    element.Attribute("Name")?.Value == targetName ||
                    element.Attribute("x:Name")?.Value == targetName);

            if (targetElement == null)
            {
                // Try to find by element type if name search failed
                targetElement = doc.Descendants()
                    .FirstOrDefault(element => element.Name.LocalName == targetName);
            }

            if (targetElement == null)
            {
                _logger.LogWarning("Element not found in XAML file: {ElementPath}", elementPath);
                return;
            }

            // Modify the property
            var attributeName = property;
            var stringValue = ConvertValueToXamlString(value);

            // Set or update the attribute
            targetElement.SetAttributeValue(attributeName, stringValue);

            // Save the modified XAML file
            File.WriteAllText(xamlFilePath, doc.ToString());
            
            _logger.LogInformation("Successfully modified {Property} to {Value} in XAML file {FilePath}", 
                property, stringValue, xamlFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error modifying property in XAML file {FilePath}", xamlFilePath);
            throw;
        }
    }

    /// <summary>
    /// Converts a value to its XAML string representation.
    /// </summary>
    private string ConvertValueToXamlString(object value)
    {
        if (value == null)
            return string.Empty;

        // Handle special types that need conversion
        return value switch
        {
            bool boolValue => boolValue.ToString().ToLower(),
            double doubleValue when double.IsNaN(doubleValue) => "Auto",
            double doubleValue => doubleValue.ToString(),
            int intValue => intValue.ToString(),
            _ => value.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// Modifies property using Visual Studio designer API (placeholder).
    /// </summary>
    private void ModifyPropertyViaDesignerApi(string elementPath, string property, object value)
    {
        try
        {
            // This would use Visual Studio's designer APIs to modify properties
            // For now, this is a placeholder since the exact APIs depend on VS version
            _logger.LogDebug("Designer API property modification not yet implemented for {ElementPath}.{Property}", 
                elementPath, property);
            
            throw new NotImplementedException("Designer API property modification not yet implemented");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error modifying property via designer API: {ElementPath}.{Property}", elementPath, property);
            throw;
        }
    }

    public async Task<XamlBindingInfo[]> AnalyseDataBindingsAsync(string xamlFilePath)
    {
        _logger.LogInformation("Analysing data bindings in: {XamlFile}", xamlFilePath);
        
        return await Task.Run(() =>
        {
            try
            {
                return ExtractDataBindingsFromXaml(xamlFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analysing data bindings in {XamlFile}", xamlFilePath);
                return Array.Empty<XamlBindingInfo>();
            }
        });
    }

    public async Task<BindingValidationResult[]> ValidateDataBindingsAsync(string xamlFilePath)
    {
        _logger.LogInformation("Validating data bindings in: {XamlFile}", xamlFilePath);
        
        return await Task.Run(() =>
        {
            try
            {
                var bindings = ExtractDataBindingsFromXaml(xamlFilePath);
                return ValidateBindings(bindings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating data bindings in {XamlFile}", xamlFilePath);
                return Array.Empty<BindingValidationResult>();
            }
        });
    }

    /// <summary>
    /// Extracts data binding information from a XAML file.
    /// </summary>
    private XamlBindingInfo[] ExtractDataBindingsFromXaml(string xamlFilePath)
    {
        var bindings = new List<XamlBindingInfo>();

        try
        {
            if (!File.Exists(xamlFilePath))
            {
                _logger.LogWarning("XAML file does not exist: {FilePath}", xamlFilePath);
                return bindings.ToArray();
            }

            var xamlContent = File.ReadAllText(xamlFilePath);
            var doc = XDocument.Parse(xamlContent);

            if (doc.Root == null)
            {
                _logger.LogWarning("Invalid XAML file: {FilePath}", xamlFilePath);
                return bindings.ToArray();
            }

            // Extract bindings from all elements
            ExtractBindingsFromElement(doc.Root, bindings);

            _logger.LogInformation("Extracted {Count} data bindings from {FilePath}", bindings.Count, xamlFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting data bindings from {FilePath}", xamlFilePath);
        }

        return bindings.ToArray();
    }

    /// <summary>
    /// Recursively extracts bindings from an XML element and its descendants.
    /// </summary>
    private void ExtractBindingsFromElement(XElement element, List<XamlBindingInfo> bindings)
    {
        try
        {
            var elementName = element.Attribute("Name")?.Value ?? element.Attribute("x:Name")?.Value;
            var elementType = element.Name.LocalName;

            // Check all attributes for binding expressions
            foreach (var attribute in element.Attributes())
            {
                var attributeValue = attribute.Value;
                
                if (ContainsBindingExpression(attributeValue))
                {
                    var bindingInfo = ParseBindingExpression(attributeValue);
                    
                    bindingInfo.ElementName = elementName ?? $"[{elementType}]";
                    bindingInfo.ElementType = elementType;
                    bindingInfo.PropertyName = attribute.Name.LocalName;
                    bindingInfo.LineNumber = ((IXmlLineInfo)element)?.LineNumber ?? 0;

                    bindings.Add(bindingInfo);
                }
            }

            // Process child elements
            foreach (var child in element.Elements())
            {
                ExtractBindingsFromElement(child, bindings);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting bindings from element: {ElementName}", element.Name);
        }
    }

    /// <summary>
    /// Checks if a value contains a binding expression.
    /// </summary>
    private bool ContainsBindingExpression(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return value.Contains("{Binding") || 
               value.Contains("{StaticResource") || 
               value.Contains("{DynamicResource") ||
               value.Contains("{x:Bind") ||
               value.Contains("{RelativeSource");
    }

    /// <summary>
    /// Parses a binding expression to extract binding information.
    /// </summary>
    private XamlBindingInfo ParseBindingExpression(string expression)
    {
        var bindingInfo = new XamlBindingInfo
        {
            BindingExpression = expression
        };

        try
        {
            if (expression.Contains("{StaticResource"))
            {
                bindingInfo.IsResourceBinding = true;
                var match = Regex.Match(expression, @"\{StaticResource\s+([^}]+)\}");
                if (match.Success)
                {
                    bindingInfo.BindingPath = match.Groups[1].Value.Trim();
                }
            }
            else if (expression.Contains("{DynamicResource"))
            {
                bindingInfo.IsDynamicResourceBinding = true;
                var match = Regex.Match(expression, @"\{DynamicResource\s+([^}]+)\}");
                if (match.Success)
                {
                    bindingInfo.BindingPath = match.Groups[1].Value.Trim();
                }
            }
            else if (expression.Contains("{Binding") || expression.Contains("{x:Bind"))
            {
                // Parse standard data binding
                ParseStandardBinding(expression, bindingInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing binding expression: {Expression}", expression);
        }

        return bindingInfo;
    }

    /// <summary>
    /// Parses a standard data binding expression.
    /// </summary>
    private void ParseStandardBinding(string expression, XamlBindingInfo bindingInfo)
    {
        try
        {
            // Extract Path
            var pathMatch = Regex.Match(expression, @"Path\s*=\s*([^,}]+)");
            if (pathMatch.Success)
            {
                bindingInfo.BindingPath = pathMatch.Groups[1].Value.Trim();
            }
            else
            {
                // Check for simplified binding syntax like {Binding PropertyName}
                var simpleMatch = Regex.Match(expression, @"\{(?:x:)?Binding\s+([^,}]+)");
                if (simpleMatch.Success && !simpleMatch.Groups[1].Value.Contains("="))
                {
                    bindingInfo.BindingPath = simpleMatch.Groups[1].Value.Trim();
                }
            }

            // Extract Mode
            var modeMatch = Regex.Match(expression, @"Mode\s*=\s*([^,}]+)");
            if (modeMatch.Success)
            {
                bindingInfo.BindingMode = modeMatch.Groups[1].Value.Trim();
            }

            // Extract Converter
            var converterMatch = Regex.Match(expression, @"Converter\s*=\s*\{StaticResource\s+([^}]+)\}");
            if (converterMatch.Success)
            {
                bindingInfo.Converter = converterMatch.Groups[1].Value.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing standard binding: {Expression}", expression);
        }
    }

    /// <summary>
    /// Validates an array of bindings and returns validation results.
    /// </summary>
    private BindingValidationResult[] ValidateBindings(XamlBindingInfo[] bindings)
    {
        var results = new List<BindingValidationResult>();

        foreach (var binding in bindings)
        {
            var result = ValidateBinding(binding);
            results.Add(result);
        }

        return results.ToArray();
    }

    /// <summary>
    /// Validates a single binding and returns the result.
    /// </summary>
    private BindingValidationResult ValidateBinding(XamlBindingInfo binding)
    {
        var result = new BindingValidationResult
        {
            Binding = binding,
            IsValid = true,
            Severity = ValidationSeverity.None
        };

        try
        {
            // Check for empty binding path
            if (string.IsNullOrEmpty(binding.BindingPath) && !binding.IsResourceBinding && !binding.IsDynamicResourceBinding)
            {
                result.IsValid = false;
                result.Severity = ValidationSeverity.Warning;
                result.Issues.Add("Binding has no path specified");
                result.Recommendations.Add("Specify a Path for the binding");
            }

            // Check for suspicious binding paths
            if (binding.BindingPath.Contains(".."))
            {
                result.Severity = (ValidationSeverity)Math.Max((int)result.Severity, (int)ValidationSeverity.Warning);
                result.Issues.Add("Binding path contains relative navigation (..)");
                result.Recommendations.Add("Consider using more explicit binding paths");
            }

            // Check for missing Mode on two-way properties
            if (IsTwoWayProperty(binding.PropertyName) && string.IsNullOrEmpty(binding.BindingMode))
            {
                result.Severity = (ValidationSeverity)Math.Max((int)result.Severity, (int)ValidationSeverity.Info);
                result.Issues.Add($"Two-way property '{binding.PropertyName}' without explicit Mode");
                result.Recommendations.Add("Consider explicitly setting Mode=TwoWay");
            }

            // Check for resource bindings
            if (binding.IsResourceBinding || binding.IsDynamicResourceBinding)
            {
                if (string.IsNullOrEmpty(binding.BindingPath))
                {
                    result.IsValid = false;
                    result.Severity = ValidationSeverity.Error;
                    result.Issues.Add("Resource binding without resource key");
                }
            }

            // Performance recommendations
            if (!binding.IsResourceBinding && !binding.IsDynamicResourceBinding)
            {
                if (string.IsNullOrEmpty(binding.BindingMode))
                {
                    result.Severity = (ValidationSeverity)Math.Max((int)result.Severity, (int)ValidationSeverity.Info);
                    result.Issues.Add("Binding without explicit Mode");
                    result.Recommendations.Add("Consider specifying Mode for better performance");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating binding: {BindingExpression}", binding.BindingExpression);
            result.IsValid = false;
            result.Severity = ValidationSeverity.Error;
            result.Issues.Add($"Validation error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Checks if a property typically requires two-way binding.
    /// </summary>
    private bool IsTwoWayProperty(string propertyName)
    {
        var twoWayProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Text", "Value", "IsChecked", "SelectedItem", "SelectedValue", "SelectedIndex"
        };

        return twoWayProperties.Contains(propertyName);
    }
}