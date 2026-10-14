using System.Runtime.InteropServices;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ToMP3;

/// <summary>版本号直接读取随程序集嵌入的发布清单 Info.plist。</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        using var stream = typeof(AboutWindow).Assembly.GetManifestResourceStream("ToMP3.Info.plist")!;
        var metadata = XDocument.Load(stream).Root!.Element("dict")!.Elements("key")
            .ToDictionary(key => key.Value, key => key.ElementsAfterSelf().First().Value);
        VersionText.Text = $"版本 {metadata["CFBundleShortVersionString"]}（构建 {metadata["CFBundleVersion"]}）";
        PlatformText.Text = $"{(OperatingSystem.IsWindows() ? "Windows" : "macOS")} · "
            + $"{RuntimeInformation.ProcessArchitecture} · 支持 {FormatConvert.Count} 种格式";
    }

    void OnClose(object? sender, RoutedEventArgs e) => Close();
}
