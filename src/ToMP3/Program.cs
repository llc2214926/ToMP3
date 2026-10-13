using Avalonia;

namespace ToMP3;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        // 不装 Inter 字体包：Inter 没有中文字形，直接用系统字体回退
        // （窗口里已指定「微软雅黑 / 苹方 / Noto CJK」优先级链）。
        .LogToTrace();
}
