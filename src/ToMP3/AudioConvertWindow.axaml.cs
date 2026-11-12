using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace ToMP3;

public enum JobState { Waiting, Converting, Done, Failed, Skipped, Canceled }

/// <summary>状态保持独立于显示语言；切换主题/语言不丢任务结果。</summary>
public class Job(string path) : INotifyPropertyChanged
{
    public string Path { get; } = path;
    public string Name => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";
    public string? Output { get; set; }
    JobState _state;
    public JobState State
    {
        get => _state;
        set { _state = value; P(nameof(State)); P(nameof(Status)); P(nameof(StatusBrush)); P(nameof(StatusIcon)); P(nameof(NoteBrush)); }
    }
    public string Status => Text.Get(State.ToString());
    public IBrush StatusBrush => Resource("Brush" + (State switch
        { JobState.Done => "Success", JobState.Failed => "Danger", _ => "TextPrimary" })) as IBrush ?? Brushes.Gray;
    public IBrush NoteBrush => Resource(State == JobState.Failed ? "BrushDanger" : "BrushTextSecondary") as IBrush ?? Brushes.Gray;
    public Geometry? StatusIcon => Resource(State switch
        { JobState.Done => "IconCheck", JobState.Failed => "IconError", JobState.Canceled => "IconStop", _ => "IconClock" }) as Geometry;
    public bool IsVideo => FormatConvert.Match(Path)?.Name == "视频";
    public Geometry? FileIcon => Resource(IsVideo ? "IconVideo" : "IconNote") as Geometry;

    static object? Resource(string key)
    {
        var app = Avalonia.Application.Current;
        return app?.TryFindResource(key, app.ActualThemeVariant, out var value) == true ? value : null;
    }
    string _note = "";
    public string Note { get => Text.Error(_note); set { _note = value; P(nameof(Note)); } }
    public void RefreshAppearance() { P(nameof(Status)); P(nameof(StatusBrush)); P(nameof(Note)); P(nameof(NoteBrush)); }
    public event PropertyChangedEventHandler? PropertyChanged;
    void P(string property) => PropertyChanged?.Invoke(this, new(property));
}

public sealed class TipText : Avalonia.Data.Converters.IValueConverter
{
    public object? Convert(object? value, Type target, object? parameter, System.Globalization.CultureInfo culture)
        => value is string s && s.Length > 0 ? s : null;
    public object? ConvertBack(object? value, Type target, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>双栏工作台：左侧任务、右侧偏好。转换启动时快照参数，始终串行执行。</summary>
public partial class AudioConvertWindow : Window
{
    public ObservableCollection<Job> Items { get; } = new();
    readonly HashSet<string> _seen = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    readonly App _app;
    CancellationTokenSource? _cts;
    Job? _currentJob;
    int _batchIndex, _batchTotal;
    bool _closeWhenStopped;
    string? _noticeKey;
    object[] _noticeArgs = [];

    public AudioConvertWindow()
    {
        _app = (App)Avalonia.Application.Current!;
        InitializeComponent();
        Grid.ItemsSource = Items;
        Items.CollectionChanged += (_, _) => Report();
        _app.SettingsChanged += OnSettingsChanged;
        ActualThemeVariantChanged += OnThemeChanged;
        Closed += (_, _) => { _app.SettingsChanged -= OnSettingsChanged; ActualThemeVariantChanged -= OnThemeChanged; };
        ApplyPlatform();
        RefreshPreferences();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.OemComma && (e.KeyModifiers & (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control)) != 0)
            { _app.ShowSettings(); e.Handled = true; }
        };
    }

    void ApplyPlatform()
    {
        bool windows = OperatingSystem.IsWindows();
        ExtendClientAreaToDecorationsHint = !windows;
        TitleBar.IsVisible = !windows;
        BtnPickAll.IsVisible = !windows;
        WindowsActions.IsVisible = WindowsInfo.IsVisible = windows;
    }

    void OnSettingsChanged(object? sender, EventArgs e) => RefreshPreferences();
    void OnThemeChanged(object? sender, EventArgs e) => RefreshPreferences();
    void RefreshPreferences()
    {
        var settings = _app.Settings;
        OutDirText.Text = settings.OutputDirectory.Length == 0 ? Text.Get("SameFolder") : settings.OutputDirectory;
        ToolTip.SetTip(OutDirText, OutDirText.Text);
        OutDirHint.Text = Text.Get(settings.OutputDirectory.Length == 0 ? "SameFolderHint" : "CustomFolderHint");
        QualityText.Text = settings.Bitrate == 0 ? Text.Quality(settings.Quality) : $"{settings.Bitrate} kbps · CBR";
        QuickHigh.Classes.Set("selected", settings.Bitrate == 0 && settings.Quality == Mp3Quality.High);
        QuickBalanced.Classes.Set("selected", settings.Bitrate == 0 && settings.Quality == Mp3Quality.Balanced);
        QuickCompact.Classes.Set("selected", settings.Bitrate == 0 && settings.Quality == Mp3Quality.Compact);
        MenuReveal.Header = Text.Get(OperatingSystem.IsWindows() ? "RevealWindows" : "RevealMac");
        foreach (var job in Items) job.RefreshAppearance();
        var formats = new InlineCollection { new Run(Text.Get("FormatCount", FormatConvert.Count) + "\n") };
        var categoryBrush = this.TryFindResource("BrushBlueText", ActualThemeVariant, out var resource)
            && resource is IBrush brush ? brush : Brushes.DodgerBlue;
        foreach (var converter in FormatConvert.All)
        {
            formats.Add(new Run(Text.Category(converter.Name)) { Foreground = categoryBrush });
            formats.Add(new Run("  " + string.Join(' ', converter.Inputs.Select(x => x.TrimStart('.'))) + "\n"));
        }
        FormatHint.Inlines = formats;
        RefreshFooter();
    }

    void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // 按钮自己处理点击，不把齿轮按下当成拖动窗口。
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Handled) return;
        e.Handled = true;
        if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else BeginMoveDrag(e);
    }

    void Say(string key, params object[] args) { _noticeKey = key; _noticeArgs = args; RefreshFooter(); }
    void Report() { _noticeKey = null; RefreshFooter(); }
    void RefreshFooter()
    {
        CountText.Text = Text.Get("FileCount", Items.Count);
        EmptyState.IsVisible = Items.Count == 0;
        MoreFilesHint.IsVisible = Items.Count > 0;
        BtnStart.IsEnabled = _cts is null && Items.Any(j => j.State != JobState.Done);
        if (_cts is not null)
        {
            FooterText.Text = _cts.IsCancellationRequested ? Text.Get("Stopping")
                : Text.Get("ConvertingFile", _batchIndex, _batchTotal, _currentJob?.Name ?? "");
            return;
        }
        if (_noticeKey is not null) { FooterText.Text = Text.Get(_noticeKey, _noticeArgs); return; }
        if (Items.Count == 0) { FooterText.Text = Text.Get("NoFiles"); return; }
        FooterText.Text = Text.Get("BatchSummary", Items.Count(j => j.State == JobState.Done),
            Items.Count(j => j.State == JobState.Failed), Items.Count(j => j.State == JobState.Waiting));
        int skipped = Items.Count(j => j.State == JobState.Skipped), canceled = Items.Count(j => j.State == JobState.Canceled);
        if (skipped + canceled > 0) FooterText.Text += Text.Get("ExtraSummary", skipped, canceled);
    }

    void OnToggleFormats(object? sender, RoutedEventArgs e)
    {
        FormatsPanel.IsVisible = !FormatsPanel.IsVisible;
        FormatsChevron.RenderTransform = new RotateTransform(FormatsPanel.IsVisible ? 90 : 0);
    }

    void OnPickSources(object? sender, RoutedEventArgs e)
    {
        foreach (var path in SourcePicker.Pick()) Ingest(path);
    }
    async void OnPickFiles(object? sender, RoutedEventArgs e)
    {
        foreach (var path in await SourcePicker.PickFilesAsync(this)) Ingest(path);
    }
    async void OnPickFolder(object? sender, RoutedEventArgs e)
    {
        foreach (var path in await SourcePicker.PickFoldersAsync(this)) Ingest(path);
    }
    void OnAbout(object? sender, RoutedEventArgs e) => _app.ShowAbout();
    void OnSettings(object? sender, RoutedEventArgs e) => _app.ShowSettings();
    void OnOutputSettings(object? sender, RoutedEventArgs e) => _app.ShowSettings("output");
    void OnQualitySettings(object? sender, RoutedEventArgs e) => _app.ShowSettings("quality");
    void OnKeySettings(object? sender, RoutedEventArgs e) => _app.ShowSettings("keys");
    void OnQuality(object? sender, RoutedEventArgs e)
    {
        var quality = Enum.Parse<Mp3Quality>((string)((Button)sender!).Tag!);
        if (!_app.UpdateSettings(_app.Settings with { Quality = quality, Bitrate = 0 }, out var error))
        { _noticeKey = null; FooterText.Text = error; }
    }
    void Ingest(string path)
    {
        try { Add(path, Directory.Exists(path)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Say("ReadFailed", System.IO.Path.GetFileName(path), ex.Message); }
    }
    void Add(string path, bool expand)
    {
        IEnumerable<string> files = expand && Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Where(f => !System.IO.Path.GetFileName(f).StartsWith('.') && FormatConvert.Match(f) is not null)
            : [path];
        int added = 0;
        foreach (var f in files)
        {
            if (!_seen.Add(f)) continue;
            var job = new Job(f);
            if (FormatConvert.Match(f) is null)
            { job.State = JobState.Skipped; job.Note = "不支持此文件格式：" + System.IO.Path.GetExtension(f); }
            Items.Add(job); added++;
        }
        Report();
        if (expand && added == 0) Say("NoSupportedFiles");
    }
    void OnRemove(object? sender, RoutedEventArgs e)
    {
        if (_cts is not null || Grid.SelectedItem is not Job job) return;
        Items.Remove(job); _seen.Remove(job.Path); Report();
    }
    void OnClear(object? sender, RoutedEventArgs e)
    {
        if (_cts is not null) return;
        Items.Clear(); _seen.Clear(); Report();
    }
    void OnRedo(object? sender, RoutedEventArgs e)
    {
        if (_cts is not null || Grid.SelectedItem is not Job job) return;
        job.State = JobState.Waiting; job.Note = ""; job.Output = null; Report();
    }
    void OnDragOver(object? sender, DragEventArgs e) => e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
    void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.GetFiles() is not { } items) return;
        foreach (var item in items) if (item.TryGetLocalPath() is { } path) Ingest(path);
    }

    async void OnStart(object? sender, RoutedEventArgs e)
    {
        if (_cts is not null) return;
        var pending = Items.Where(j => j.State != JobState.Done).ToList();
        if (pending.Count == 0) { Say(Items.Count == 0 ? "NoFiles" : "AllDone"); return; }
        var settings = _app.Settings; // 不可变快照：本批次输出位置和品质不跟随中途修改。
        var encoding = new Mp3Encoding(settings.Quality, settings.Bitrate);
        using var cts = new CancellationTokenSource();
        _cts = cts; _batchTotal = pending.Count; _batchIndex = 0;
        SetRunning(true);
        try
        {
            for (int i = 0; i < pending.Count; i++)
            {
                var job = pending[i];
                if (cts.IsCancellationRequested) break;
                _batchIndex = i + 1; _currentJob = job;
                var converter = FormatConvert.Match(job.Path);
                if (converter is null)
                { job.State = JobState.Skipped; job.Note = "不支持此文件格式：" + System.IO.Path.GetExtension(job.Path); BatchProgress.Value = (double)(i + 1) / pending.Count; continue; }
                job.State = JobState.Converting; job.Note = ""; job.Output = null;
                RefreshFooter();
                var directory = settings.OutputDirectory.Length > 0 ? settings.OutputDirectory : job.Folder;
                try
                {
                    var result = await converter.Run(job.Path, directory, cts.Token, encoding);
                    job.State = result.Ok ? JobState.Done : cts.IsCancellationRequested ? JobState.Canceled : JobState.Failed;
                    job.Note = result.Detail; job.Output = result.Output;
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested) { job.State = JobState.Canceled; }
                catch (Exception ex) { job.State = JobState.Failed; job.Note = ex.Message; }
                BatchProgress.Value = (double)(i + 1) / pending.Count;
                if (cts.IsCancellationRequested) break;
            }
        }
        finally
        {
            _cts = null; _currentJob = null; SetRunning(false); Report();
            if (_closeWhenStopped) Close();
        }
    }
    void OnStop(object? sender, RoutedEventArgs e) { _cts?.Cancel(); RefreshFooter(); }
    void SetRunning(bool running)
    {
        BtnStart.IsVisible = !running; BtnStop.IsVisible = running;
        BtnRemove.IsEnabled = BtnClear.IsEnabled = MenuRedo.IsEnabled = MenuRemove.IsEnabled = !running;
        BusyOptions.IsVisible = BatchProgress.IsVisible = running;
        if (running) BatchProgress.Value = 0;
        RefreshFooter();
    }
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_cts is not null) { e.Cancel = true; _closeWhenStopped = true; _cts.Cancel(); RefreshFooter(); }
        base.OnClosing(e);
    }
    void OnRowDoubleTapped(object? sender, TappedEventArgs e) => OnReveal(sender, e);
    void OnReveal(object? sender, RoutedEventArgs e)
    {
        if (Grid.SelectedItem is Job job) TryReveal(job.Output ?? job.Path);
    }
    void OnOpenOutDir(object? sender, RoutedEventArgs e)
    {
        var directory = _app.Settings.OutputDirectory;
        if (directory.Length == 0)
        {
            var output = Items.FirstOrDefault(j => j.State == JobState.Done)?.Output;
            directory = output is null ? "" : System.IO.Path.GetDirectoryName(output) ?? "";
        }
        if (directory.Length == 0 || !Directory.Exists(directory)) { Say("NoOutput"); return; }
        TryReveal(directory);
    }
    void TryReveal(string path)
    {
        try { Reveal(path); }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        { Say("OpenFailed", ex.Message); }
    }
    static void Reveal(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo("explorer.exe")
            { UseShellExecute = false, ArgumentList = { "/select," + System.IO.Path.GetFullPath(path) } });
            return;
        }
        var psi = new ProcessStartInfo("open") { UseShellExecute = false };
        if (File.Exists(path)) psi.ArgumentList.Add("-R");
        psi.ArgumentList.Add(path); Process.Start(psi);
    }
}
