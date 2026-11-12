using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace ToMP3;

/// <summary>共享的设置窗口；偏好即时保存，登录检测仅返回状态，不展示凭据。</summary>
public partial class SettingsWindow : Window
{
    readonly App _app;
    bool _updating;
    string _page = "appearance";
    string _loginState = "NotChecked";
    string _loginError = "";
    readonly CancellationTokenSource _lifetime = new();

    public SettingsWindow()
    {
        _app = (App)Avalonia.Application.Current!;
        InitializeComponent();
        _app.SettingsChanged += OnSettingsChanged;
        Closed += (_, _) => { _app.SettingsChanged -= OnSettingsChanged; _lifetime.Cancel(); _lifetime.Dispose(); };
        Refresh();
        SelectPage(_page);
    }

    public void SelectPage(string page)
    {
        _page = page is "output" or "quality" or "keys" ? page : "appearance";
        AppearancePage.IsVisible = _page == "appearance";
        OutputPage.IsVisible = _page == "output";
        QualityPage.IsVisible = _page == "quality";
        KeysPage.IsVisible = _page == "keys";
        foreach (var button in new[] { NavAppearance, NavOutput, NavQuality, NavKeys })
            button.Classes.Set("selected", (string?)button.Tag == _page);
    }

    void OnPage(object? sender, RoutedEventArgs e) => SelectPage((string)((Button)sender!).Tag!);
    void OnSettingsChanged(object? sender, EventArgs e) => Refresh();

    void Refresh()
    {
        _updating = true;
        try
        {
            var s = _app.Settings;
            LanguageBox.SelectedIndex = (int)s.Language;
            BitrateBox.SelectedIndex = Array.IndexOf(AppSettings.Bitrates, s.Bitrate);
            ThemeLight.Classes.Set("selected", s.Appearance == AppAppearance.Light);
            ThemeDark.Classes.Set("selected", s.Appearance == AppAppearance.Dark);
            ThemeSystem.Classes.Set("selected", s.Appearance == AppAppearance.System);
            QualityHigh.Classes.Set("selected", s.Bitrate == 0 && s.Quality == Mp3Quality.High);
            QualityBalanced.Classes.Set("selected", s.Bitrate == 0 && s.Quality == Mp3Quality.Balanced);
            QualityCompact.Classes.Set("selected", s.Bitrate == 0 && s.Quality == Mp3Quality.Compact);
            SameFolderRadio.IsChecked = s.OutputDirectory.Length == 0;
            CustomFolderRadio.IsChecked = s.OutputDirectory.Length > 0;
            SameFolderChoice.Classes.Set("selected", s.OutputDirectory.Length == 0);
            CustomFolderChoice.Classes.Set("selected", s.OutputDirectory.Length > 0);
            OutputPath.Text = s.OutputDirectory.Length == 0 ? Text.Get("ChooseFolder") : s.OutputDirectory;
            LoginStatus.Text = Text.Get(_loginState);
            LoginDetail.Text = !OperatingSystem.IsMacOS() ? Text.Error(QqEkey.WindowsUnsupported)
                : _loginError.Length > 0 ? Text.Error(_loginError)
                : _loginState == "LoginAvailable" ? Text.Get("LoginNotVerified") : "";
            LoginDetail.IsVisible = LoginDetail.Text?.Length > 0;
            CheckLoginButton.IsEnabled = OperatingSystem.IsMacOS() && _loginState != "Checking";
        }
        finally { _updating = false; }
    }

    void Change(AppSettings value)
    {
        if (_updating) return;
        var saved = _app.UpdateSettings(value, out var error);
        SaveError.Text = error;
        SaveError.IsVisible = !saved;
        if (!saved) Refresh();
    }

    void OnTheme(object? sender, RoutedEventArgs e) => Change(_app.Settings with
    { Appearance = Enum.Parse<AppAppearance>((string)((Button)sender!).Tag!) });
    void OnQuality(object? sender, RoutedEventArgs e) => Change(_app.Settings with
    { Quality = Enum.Parse<Mp3Quality>((string)((Button)sender!).Tag!), Bitrate = 0 });
    void OnLanguage(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updating && LanguageBox.SelectedIndex >= 0)
            Change(_app.Settings with { Language = (AppLanguage)LanguageBox.SelectedIndex });
    }
    void OnBitrate(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updating && BitrateBox.SelectedIndex >= 0)
            Change(_app.Settings with { Bitrate = AppSettings.Bitrates[BitrateBox.SelectedIndex] });
    }
    void OnSameFolder(object? sender, RoutedEventArgs e)
    {
        if (!_updating && SameFolderRadio.IsChecked == true)
            Change(_app.Settings with { OutputDirectory = "" });
    }
    async void OnCustomFolder(object? sender, RoutedEventArgs e)
    {
        if (!_updating && CustomFolderRadio.IsChecked == true && _app.Settings.OutputDirectory.Length == 0)
        {
            await PickOutput();
            Refresh(); // 取消选择不改变设置，单选框也回到原值。
        }
    }
    async void OnPickOutput(object? sender, RoutedEventArgs e) => await PickOutput();
    async Task PickOutput()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        { Title = Text.Get("SelectOutput"), AllowMultiple = false });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
            Change(_app.Settings with { OutputDirectory = path });
    }
    void OnResetPage(object? sender, RoutedEventArgs e)
    {
        var current = _app.Settings;
        switch (_page)
        {
            case "appearance": Change(current with { Appearance = AppAppearance.System, Language = AppLanguage.Chinese }); break;
            case "output": Change(current with { OutputDirectory = "" }); break;
            case "quality": Change(current with { Quality = Mp3Quality.High, Bitrate = 0 }); break;
            case "keys":
                if (_loginState == "Checking") return;
                _loginState = "NotChecked"; _loginError = ""; LoginGuide.IsVisible = false; Refresh(); break;
        }
    }
    async void OnCheckLogin(object? sender, RoutedEventArgs e)
    {
        if (_loginState == "Checking") return;
        _loginState = "Checking"; _loginError = ""; Refresh();
        try { await QqEkey.CheckLogin(_lifetime.Token); _loginState = "LoginAvailable"; }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException
            or System.Xml.XmlException or FormatException)
        { _loginState = "NotChecked"; _loginError = ex.Message; }
        if (!_lifetime.IsCancellationRequested) Refresh();
    }
    void OnLoginHelp(object? sender, RoutedEventArgs e) => LoginGuide.IsVisible = !LoginGuide.IsVisible;
    void OnClose(object? sender, RoutedEventArgs e) => Close();
}
