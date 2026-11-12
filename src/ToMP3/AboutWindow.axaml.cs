using System.Runtime.InteropServices;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ToMP3;

/// <summary>版本号直接读取随程序集嵌入的发布清单 Info.plist。</summary>
public partial class AboutWindow : Window
{
    readonly string _version, _build;

    public AboutWindow()
    {
        InitializeComponent();
        if (OperatingSystem.IsMacOS())
        {
            ExtendClientAreaToDecorationsHint = true;
            AboutTitleBar.IsVisible = true;
            AboutTitleBar.PointerPressed += OnTitleBarPointerPressed;
        }
        using var stream = typeof(AboutWindow).Assembly.GetManifestResourceStream("ToMP3.Info.plist")!;
        var metadata = XDocument.Load(stream).Root!.Element("dict")!.Elements("key")
            .ToDictionary(key => key.Value, key => key.ElementsAfterSelf().First().Value);
        _version = metadata["CFBundleShortVersionString"];
        _build = metadata["CFBundleVersion"];
        if (Avalonia.Application.Current is App app)
        {
            app.SettingsChanged += OnSettingsChanged;
            Closed += (_, _) => app.SettingsChanged -= OnSettingsChanged;
        }
        RefreshText();
    }

    void OnSettingsChanged(object? sender, EventArgs e) => RefreshText();
    void RefreshText()
    {
        VersionText.Text = Text.Get("Version", _version, _build);
        PlatformText.Text = $"{(OperatingSystem.IsWindows() ? "Windows" : "macOS")} · "
            + $"{RuntimeInformation.ProcessArchitecture} · {Text.Get("FormatCount", FormatConvert.Count)}";
    }

    void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Handled) return;
        e.Handled = true;
        BeginMoveDrag(e);
    }

    void OnClose(object? sender, RoutedEventArgs e) => Close();
}
