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

    void OnAbout(object? sender, EventArgs e)
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

    /// <summary>异常落 ~/Library/Application Support/ToMP3/error.log（超过 256 KB 清一次）。
    /// 写日志本身绝不能再抛。</summary>
    static void Log(string tag, Exception? ex)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "ToMP3", "error.log");
            if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024) File.Delete(path);
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {tag} {ex}\n\n");
        }
        catch { }
    }
}
