namespace VisualStudioMcp.Imaging;

/// <summary>
/// Service interface for Visual Studio window classification and discovery.
/// </summary>
public interface IWindowClassificationService
{
    /// <summary>
    /// Discovers all Visual Studio windows in the current session.
    /// </summary>
    /// <returns>Collection of discovered Visual Studio windows.</returns>
    Task<IEnumerable<VisualStudioWindow>> DiscoverVSWindowsAsync();

    /// <summary>
    /// Classifies a window by its handle to determine the window type.
    /// </summary>
    /// <param name="windowHandle">Handle to the window to classify.</param>
    /// <returns>The classified window type.</returns>
    Task<VisualStudioWindowType> ClassifyWindowAsync(IntPtr windowHandle);

    /// <summary>
    /// Analyses the current window layout and docking arrangement.
    /// </summary>
    /// <returns>Complete window layout information.</returns>
    Task<WindowLayout> AnalyzeLayoutAsync();

    /// <summary>
    /// Gets the currently active/focused Visual Studio window.
    /// </summary>
    /// <returns>The active window, or null if no VS window is focused.</returns>
    Task<VisualStudioWindow?> GetActiveWindowAsync();

    /// <summary>
    /// Finds windows of a specific type within Visual Studio.
    /// </summary>
    /// <param name="windowType">The type of windows to find.</param>
    /// <returns>Collection of windows matching the specified type.</returns>
    Task<IEnumerable<VisualStudioWindow>> FindWindowsByTypeAsync(VisualStudioWindowType windowType);
}

/// <summary>
/// Represents the different types of Visual Studio windows.
/// </summary>
public enum VisualStudioWindowType
{
    /// <summary>Unknown or unclassified window type.</summary>
    Unknown,
    
    /// <summary>Main Visual Studio IDE window.</summary>
    MainWindow,
    
    /// <summary>Solution Explorer panel.</summary>
    SolutionExplorer,
    
    /// <summary>Properties window panel.</summary>
    PropertiesWindow,
    
    /// <summary>Error List panel.</summary>
    ErrorList,
    
    /// <summary>Output window panel.</summary>
    OutputWindow,
    
    /// <summary>Code editor window.</summary>
    CodeEditor,
    
    /// <summary>XAML designer window.</summary>
    XamlDesigner,
    
    /// <summary>Toolbox panel.</summary>
    Toolbox,
    
    /// <summary>Server Explorer panel.</summary>
    ServerExplorer,
    
    /// <summary>Team Explorer panel.</summary>
    TeamExplorer,
    
    /// <summary>Package Manager Console.</summary>
    PackageManagerConsole,
    
    /// <summary>Find and Replace dialog.</summary>
    FindAndReplace,
    
    /// <summary>Immediate window (debugging).</summary>
    ImmediateWindow,
    
    /// <summary>Watch window (debugging).</summary>
    WatchWindow,
    
    /// <summary>Call Stack window (debugging).</summary>
    CallStackWindow,
    
    /// <summary>Locals window (debugging).</summary>
    LocalsWindow
}

/// <summary>
/// Represents a Visual Studio window with its metadata and properties.
/// </summary>
public class VisualStudioWindow
{
    /// <summary>
    /// The window handle.
    /// </summary>
    public IntPtr Handle { get; set; }

    /// <summary>
    /// The window title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The window class name.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// The classified window type.
    /// </summary>
    public VisualStudioWindowType WindowType { get; set; } = VisualStudioWindowType.Unknown;

    /// <summary>
    /// Whether the window is currently visible.
    /// </summary>
    public bool IsVisible { get; set; }

    /// <summary>
    /// The process ID that owns this window.
    /// </summary>
    public uint ProcessId { get; set; }

    /// <summary>
    /// The window bounds (position and size).
    /// </summary>
    public WindowBounds Bounds { get; set; } = new();

    /// <summary>
    /// The parent window handle, if this is a child window.
    /// </summary>
    public IntPtr? ParentHandle { get; set; }

    /// <summary>
    /// Child windows contained within this window.
    /// </summary>
    public List<VisualStudioWindow> ChildWindows { get; set; } = new();

    /// <summary>
    /// Additional metadata about the window.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Whether this window is currently the active/focused window.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// The timestamp when this window information was captured.
    /// </summary>
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents the window bounds (position and size).
/// </summary>
public class WindowBounds
{
    /// <summary>X coordinate of the window's left edge.</summary>
    public int X { get; set; }
    
    /// <summary>Y coordinate of the window's top edge.</summary>
    public int Y { get; set; }
    
    /// <summary>Width of the window.</summary>
    public int Width { get; set; }
    
    /// <summary>Height of the window.</summary>
    public int Height { get; set; }
    
    /// <summary>X coordinate of the window's right edge.</summary>
    public int Right => X + Width;
    
    /// <summary>Y coordinate of the window's bottom edge.</summary>
    public int Bottom => Y + Height;
}

/// <summary>
/// Represents the complete Visual Studio window layout analysis.
/// </summary>
public class WindowLayout
{
    /// <summary>
    /// The main Visual Studio window.
    /// </summary>
    public VisualStudioWindow? MainWindow { get; set; }

    /// <summary>
    /// All discovered Visual Studio windows.
    /// </summary>
    public List<VisualStudioWindow> AllWindows { get; set; } = new();

    /// <summary>
    /// Windows grouped by their type.
    /// </summary>
    public Dictionary<VisualStudioWindowType, List<VisualStudioWindow>> WindowsByType { get; set; } = new();

    /// <summary>
    /// The currently active/focused window.
    /// </summary>
    public VisualStudioWindow? ActiveWindow { get; set; }

    /// <summary>
    /// Docking layout information (if available).
    /// </summary>
    public DockingLayout DockingLayout { get; set; } = new();

    /// <summary>
    /// The timestamp when this layout analysis was performed.
    /// </summary>
    public DateTime AnalysisTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata about the layout.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents the docking layout information for Visual Studio panels.
/// </summary>
public class DockingLayout
{
    /// <summary>
    /// Panels docked to the left side.
    /// </summary>
    public List<VisualStudioWindow> LeftDockedPanels { get; set; } = new();

    /// <summary>
    /// Panels docked to the right side.
    /// </summary>
    public List<VisualStudioWindow> RightDockedPanels { get; set; } = new();

    /// <summary>
    /// Panels docked to the top.
    /// </summary>
    public List<VisualStudioWindow> TopDockedPanels { get; set; } = new();

    /// <summary>
    /// Panels docked to the bottom.
    /// </summary>
    public List<VisualStudioWindow> BottomDockedPanels { get; set; } = new();

    /// <summary>
    /// Floating panels not docked to any side.
    /// </summary>
    public List<VisualStudioWindow> FloatingPanels { get; set; } = new();

    /// <summary>
    /// The central editor area.
    /// </summary>
    public VisualStudioWindow? EditorArea { get; set; }
}