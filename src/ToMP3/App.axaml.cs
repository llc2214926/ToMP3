using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace ToMP3;

public partial class App : Application
{
    AboutWindow? _aboutWindow;

    /// <summary>打开关于窗口。macOS 走原生应用菜单，Windows 走标题栏按钮，
    /// 两边都调这里，重复打开复用同一实例。</summary>
    public void ShowAbout()
    {
        if (_aboutWindow is not null)
        {
            _aboutWindow.WindowState = WindowState.Normal;
            _aboutWindow.Activate();
            return;
        }

        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner }) return;
        _aboutWindow = new AboutWindow();
        _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        _aboutWindow.Show(owner);
    }

    void OnAbout(object? sender, EventArgs e) => ShowAbout();


    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 兜底：UI 线程上没被接住的异常不该把整个程序带走（macOS 会弹「意外退出」）。
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                e.Handled = true;
                Log("ui ", e.Exception);
            };
            // 后台线程上逃出来的异常 .NET 不允许恢复，进程照样会退——这里只负责留证据。
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Log("bg ", e.ExceptionObject as Exception);

            desktop.MainWindow = new AudioConvertWindow();
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>异常落 macOS 的 ~/Library/Application Support/ToMP3/error.log 或
    /// Windows 的 %LOCALAPPDATA%\ToMP3\error.log（超过 256 KB 清一次）。
    /// 写日志本身绝不能再抛。</summary>
    static void Log(string tag, Exception? ex)
    {
        try
        {
            // .NET 在 Unix 上把 LocalApplicationData 映射到 ~/.local/share，
            // 不是 macOS 惯例的 Application Support，所以两边分开写。
            var dir = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ToMP3")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "ToMP3");
            var path = Path.Combine(dir, "error.log");
            Directory.CreateDirectory(dir);
            if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024) File.Delete(path);
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {tag} {ex}\n\n");
        }
        catch { }
    }
}
