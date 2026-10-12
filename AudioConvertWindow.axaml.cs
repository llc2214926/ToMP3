using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace ToMP3;

/// <summary>待转清单里的一行。Status / Note 要跟着转换过程变，所以走 INotifyPropertyChanged。</summary>
public class Job(string path) : INotifyPropertyChanged
{
    public string Path { get; } = path;
    public string Name => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";

    /// <summary>转换器的实际产物路径（ncm 里可能是 mp3 也可能是 flac），成功后才填。</summary>
    public string? Output { get; set; }

    string _status = "等待";
    public string Status
    {
        get => _status;
        set { _status = value; P(nameof(Status)); }
    }

    string _note = "";
    public string Note
    {
        get => _note;
        set { _note = value; P(nameof(Note)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void P(string n) => PropertyChanged?.Invoke(this, new(n));
}

/// <summary>「各种格式转 MP3」：选文件 / 拖文件 → 按扩展名挑转换器 → 串行转换 → 看每行结果。
/// 一体式标题栏保留原生窗口按钮；紧凑设置区支持折叠说明，原生选择窗口可同时选择文件/文件夹。
/// 转换器内核对界面一无所知，加格式只动 <see cref="FormatConvert.All"/>。</summary>
public partial class AudioConvertWindow : Window
{
    public ObservableCollection<Job> Items { get; } = new();

    /// <summary>已在清单里的源文件（拖重复的进来不重复加）。</summary>
    readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    /// <summary>输出目录；空串 = 与源文件同目录。</summary>
    string _outDir = "";

    CancellationTokenSource? _cts;

    public AudioConvertWindow()
    {
        InitializeComponent();
        Grid.ItemsSource = Items;
        FormatHint.Text = FormatConvert.SupportList;
        FormatSummary.Text = $"支持 {FormatConvert.Count} 种加密音乐格式 · 查看完整列表";
        ShowOutDir();
        Report();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Handled = true;
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            BeginMoveDrag(e);
    }

    void ShowOutDir() => OutDirBox.Text = _outDir.Length == 0 ? "与源文件同目录" : _outDir;

    void Say(string msg) => FooterText.Text = msg;

    void Report() => Say(Items.Count == 0 ? "还没有文件" : $"共 {Items.Count} 个文件");

    // ── 加 / 清清单 ────────────────────────────────────────────────

    void OnPickSources(object? sender, RoutedEventArgs e)
    {
        foreach (var path in MacSourcePicker.Pick())
        {
            try { Add(path, Directory.Exists(path)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Say($"无法读取 {System.IO.Path.GetFileName(path)}：{ex.Message}");
            }
        }
    }

    /// <summary>加进清单。<paramref name="expand"/> 的文件夹按支持的扩展名递归展开（跳过隐藏文件，
    /// 别把 ._xxx 这种 AppleDouble 当输入）；单独选的文件不筛——界面标「跳过」比悄悄丢掉强。</summary>
    void Add(string path, bool expand)
    {
        IEnumerable<string> files = expand && Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Where(f => !System.IO.Path.GetFileName(f).StartsWith('.')
                            && FormatConvert.Match(f) is not null)
            : [path];

        int added = 0;
        foreach (var f in files)
            if (_seen.Add(f)) { Items.Add(new Job(f)); added++; }

        Report();
        if (expand && added == 0) Say("这个文件夹里没有支持的加密音乐文件");
    }

    void OnRemove(object? sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Job j) return;
        Items.Remove(j);
        _seen.Remove(j.Path);
        Report();
    }

    void OnClear(object? sender, RoutedEventArgs e)
    {
        Items.Clear();
        _seen.Clear();
        Report();
    }

    /// <summary>重新转换：清掉状态，下次「开始转换」会重跑这一行（会覆盖上次的产物）。</summary>
    void OnRedo(object? sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is not Job j) return;
        j.Status = "等待";
        j.Note = "";
        j.Output = null;
        Report();
    }

    // ── 拖入 ───────────────────────────────────────────────────────

    void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;

    void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.GetFiles() is not { } items) return;
        foreach (var item in items)
            if (item.TryGetLocalPath() is { } p) Add(p, Directory.Exists(p));
    }

    // ── 输出目录 ───────────────────────────────────────────────────

    async void OnPickOutDir(object? sender, RoutedEventArgs e)
    {
        var dirs = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        { Title = "转换结果放到这里", AllowMultiple = false });
        if (dirs.Count > 0 && dirs[0].TryGetLocalPath() is { } p) { _outDir = p; ShowOutDir(); }
    }

    void OnClearOutDir(object? sender, RoutedEventArgs e) { _outDir = ""; ShowOutDir(); }

    // ── 转换 ───────────────────────────────────────────────────────

    async void OnStart(object? sender, RoutedEventArgs e)
    {
        if (_cts is not null) return;
        var todo = Items.Where(j => j.Status != "完成").ToList();
        if (todo.Count == 0) { Say(Items.Count == 0 ? "先加文件" : "都转完了（右键「重新转换」可重跑某一行）"); return; }

        var cts = new CancellationTokenSource();
        _cts = cts;
        SetRunning(true);
        int ok = 0, fail = 0, skip = 0;
        try
        {
            for (int i = 0; i < todo.Count; i++)
            {
                var job = todo[i];
                if (cts.IsCancellationRequested) { job.Status = "取消"; break; }

                var conv = FormatConvert.Match(job.Path);
                if (conv is null)
                {
                    job.Status = "跳过";
                    job.Note = $"没有能转 {System.IO.Path.GetExtension(job.Path)} 的转换器";
                    skip++;
                    continue;
                }

                job.Status = "转换中";
                Say($"{i + 1}/{todo.Count} … {job.Name}");
                var dir = _outDir.Length > 0 ? _outDir : job.Folder;
                try
                {
                    Directory.CreateDirectory(dir);
                    var r = await conv.Run(job.Path, dir, cts.Token);
                    job.Status = r.Ok ? "完成" : cts.IsCancellationRequested ? "取消" : "失败";
                    job.Note = r.Detail;
                    job.Output = r.Output;
                    if (r.Ok) ok++;
                    else if (cts.IsCancellationRequested) break;   // 取消时后面的行留在「等待」
                    else fail++;
                }
                catch (Exception ex)
                {
                    job.Status = "失败";
                    job.Note = ex.Message;
                    fail++;
                }
            }
        }
        finally
        {
            _cts = null;
            cts.Dispose();
            SetRunning(false);
            Say($"完成 {ok} · 失败 {fail}"
                + (skip > 0 ? $" · 跳过 {skip}" : "")
                + (Items.Any(j => j.Status == "等待") ? $" · 未处理 {Items.Count(j => j.Status == "等待")}" : ""));
        }
    }

    void OnStop(object? sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Say("正在停止…");
    }

    void SetRunning(bool running)
    {
        BtnStart.IsEnabled = !running;
        BtnStop.IsEnabled = running;
    }

    // ── 行操作 ─────────────────────────────────────────────────────

    void OnRowDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e) => OnReveal(sender, e);

    /// <summary>在访达里显示：转完的看产物，没转的看源文件。</summary>
    void OnReveal(object? sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is Job j) Reveal(j.Output ?? j.Path);
    }

    void OnOpenOutDir(object? sender, RoutedEventArgs e)
    {
        var dir = _outDir.Length > 0
            ? _outDir
            : Items.FirstOrDefault(j => j.Status == "完成")?.Folder ?? "";
        if (dir.Length == 0 || !Directory.Exists(dir)) { Say("还没有输出目录"); return; }
        Reveal(dir);
    }

    /// <summary>在访达里定位（文件用 -R 选中，目录直接打开）。</summary>
    static void Reveal(string path)
    {
        var psi = new ProcessStartInfo("open") { UseShellExecute = false };
        if (File.Exists(path)) psi.ArgumentList.Add("-R");
        psi.ArgumentList.Add(path);
        Process.Start(psi);
    }
}
