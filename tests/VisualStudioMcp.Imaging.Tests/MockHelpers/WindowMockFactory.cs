using VisualStudioMcp.Imaging;

namespace VisualStudioMcp.Imaging.Tests.MockHelpers;

/// <summary>
/// Factory for creating mock Visual Studio window objects for testing.
/// </summary>
public static class WindowMockFactory
{
    /// <summary>
    /// Creates a mock VisualStudioWindow with specified properties.
    /// </summary>
    public static VisualStudioWindow CreateMockWindow(
        string title = "Test Window",
        string className = "TestWindowClass", 
        uint processId = 1234,
        IntPtr handle = default,
        bool isVisible = true,
        bool isActive = false,
        VisualStudioWindowType windowType = VisualStudioWindowType.Unknown)
    {
        return new VisualStudioWindow
        {
            Handle = handle == IntPtr.Zero ? new IntPtr(Random.Shared.Next(10000, 99999)) : handle,
            Title = title,
            ClassName = className,
            ProcessId = processId,
            IsVisible = isVisible,
            IsActive = isActive,
            WindowType = windowType,
            Bounds = new WindowBounds 
            { 
                X = 100, 
                Y = 100, 
                Width = 800, 
                Height = 600 
            },
            ChildWindows = new List<VisualStudioWindow>()
        };
    }

    /// <summary>
    /// Creates a mock Visual Studio main window.
    /// </summary>
    public static VisualStudioWindow CreateMainWindow()
    {
        return CreateMockWindow(
            title: "Microsoft Visual Studio",
            className: "HwndWrapper[DefaultDomain;;]",
            windowType: VisualStudioWindowType.MainWindow,
            isActive: true
        );
    }

    /// <summary>
    /// Creates a mock Solution Explorer window.
    /// </summary>
    public static VisualStudioWindow CreateSolutionExplorerWindow()
    {
        return CreateMockWindow(
            title: "Solution Explorer",
            className: "WindowsForms10.Window.8.app.0.2bf8098_r6_ad1",
            windowType: VisualStudioWindowType.SolutionExplorer
        );
    }

    /// <summary>
    /// Creates a mock Code Editor window.
    /// </summary>
    public static VisualStudioWindow CreateCodeEditorWindow(string fileName = "Program.cs")
    {
        return CreateMockWindow(
            title: $"{fileName} - MyProject - Microsoft Visual Studio",
            className: "Afx:00400000:8:00010003:00000006:0191036D",
            windowType: VisualStudioWindowType.CodeEditor
        );
    }

    /// <summary>
    /// Creates a mock Properties window.
    /// </summary>
    public static VisualStudioWindow CreatePropertiesWindow()
    {
        return CreateMockWindow(
            title: "Properties",
            className: "WindowsForms10.Window.8.app.0.2bf8098_r6_ad1",
            windowType: VisualStudioWindowType.PropertiesWindow
        );
    }

    /// <summary>
    /// Creates a mock Error List window.
    /// </summary>
    public static VisualStudioWindow CreateErrorListWindow()
    {
        return CreateMockWindow(
            title: "Error List",
            className: "WindowsForms10.Window.8.app.0.2bf8098_r6_ad1",
            windowType: VisualStudioWindowType.ErrorList
        );
    }

    /// <summary>
    /// Creates a collection of typical Visual Studio windows.
    /// </summary>
    public static List<VisualStudioWindow> CreateTypicalVSWindowCollection()
    {
        return new List<VisualStudioWindow>
        {
            CreateMainWindow(),
            CreateSolutionExplorerWindow(),
            CreateCodeEditorWindow("Program.cs"),
            CreateCodeEditorWindow("MyClass.cs"),
            CreatePropertiesWindow(),
            CreateErrorListWindow()
        };
    }

    /// <summary>
    /// Creates a window with a specified process ID for process testing.
    /// </summary>
    public static VisualStudioWindow CreateWindowWithProcessId(uint processId)
    {
        return CreateMockWindow(
            title: "Test Process Window",
            processId: processId
        );
    }

    /// <summary>
    /// Creates a window with specific bounds for layout testing.
    /// </summary>
    public static VisualStudioWindow CreateWindowWithBounds(int x, int y, int width, int height)
    {
        var window = CreateMockWindow();
        window.Bounds = new WindowBounds { X = x, Y = y, Width = width, Height = height };
        return window;
    }
}