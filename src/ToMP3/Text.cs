using System.Globalization;
using Avalonia;

namespace ToMP3;

/// <summary>应用文案集中维护。XAML 使用动态资源，语言变更不重建窗口或丢失任务。</summary>
public static class Text
{
    public static AppLanguage Language { get; private set; }
    static readonly Dictionary<string, (string Zh, string En)> Strings = new()
    {
        ["Settings"] = ("设置", "Settings"),
        ["SettingsMenu"] = ("设置…", "Settings…"),
        ["About"] = ("关于", "About"),
        ["AboutTitle"] = ("关于 ToMP3", "About ToMP3"),
        ["Workspace"] = ("转换工作台", "Conversion workspace"),
        ["WorkspaceHint"] = ("所有任务集中在这里，转换参数随手可调。", "Manage your queue and conversion options in one place."),
        ["Tagline"] = ("音频 / 视频 → MP3", "Audio / video → MP3"),
        ["AddSources"] = ("添加文件 / 文件夹", "Add files / folders"),
        ["AddFiles"] = ("添加文件", "Add files"),
        ["AddFolder"] = ("添加文件夹", "Add folder"),
        ["AddTip"] = ("可多选文件和文件夹；文件夹递归导入支持的格式", "Select multiple files and folders; supported files are imported recursively."),
        ["Queue"] = ("转换列表", "Conversion queue"),
        ["FileCount"] = ("{0} 个文件", "{0} files"),
        ["Remove"] = ("移除选中", "Remove selected"),
        ["Clear"] = ("清空", "Clear"),
        ["FileName"] = ("文件名", "File name"),
        ["Status"] = ("状态", "Status"),
        ["Details"] = ("说明", "Details"),
        ["EmptyTitle"] = ("把音频或视频拖到这里", "Drop audio or video here"),
        ["EmptyHint"] = ("也可以添加整个文件夹", "You can also add an entire folder"),
        ["MoreFiles"] = ("还可以继续拖入文件或文件夹", "Drop more files or folders here"),
        ["Options"] = ("本次转换", "Conversion options"),
        ["Output"] = ("输出位置", "Output location"),
        ["SameFolder"] = ("与源文件同目录", "Same as source"),
        ["SameFolderHint"] = ("每个 MP3 与对应源文件放在一起。", "Save each MP3 alongside its source."),
        ["CustomFolder"] = ("统一保存到指定文件夹", "Save all files to one folder"),
        ["CustomFolderHint"] = ("所有 MP3 统一保存到此文件夹。", "Save all MP3 files in this folder."),
        ["ChangeLocation"] = ("更改位置", "Change location"),
        ["ChooseFolder"] = ("选择文件夹…", "Choose folder…"),
        ["Quality"] = ("压缩品质", "Audio quality"),
        ["High"] = ("高", "High"),
        ["Balanced"] = ("标准", "Balanced"),
        ["Compact"] = ("小体积", "Compact"),
        ["HighLong"] = ("高品质 · VBR 0", "High quality · VBR 0"),
        ["BalancedLong"] = ("标准品质 · VBR 2", "Balanced · VBR 2"),
        ["CompactLong"] = ("小体积 · VBR 5", "Compact · VBR 5"),
        ["CopyHint"] = ("已有 MP3 音轨直接复制，不重复编码。", "MP3 audio is copied without re-encoding."),
        ["AdvancedQuality"] = ("高级品质设置", "Advanced quality"),
        ["Keys"] = ("密钥管理", "Key management"),
        ["KeysSignIn"] = ("密钥与登录", "Keys & sign-in"),
        ["KeysSummary"] = ("QQ 音乐 · 按需自动获取", "QQ Music · Automatic lookup"),
        ["ManageKeys"] = ("管理登录状态", "Manage sign-in"),
        ["Start"] = ("开始转换", "Start conversion"),
        ["Stop"] = ("停止转换", "Stop conversion"),
        ["OpenOutput"] = ("打开输出目录", "Open output folder"),
        ["KeepSource"] = ("保留源文件 · 同名输出会覆盖", "Sources kept · Existing outputs replaced"),
        ["Formats"] = ("支持的格式", "Supported formats"),
        ["FormatCount"] = ("支持 {0} 种格式", "Supports {0} formats"),
        ["RevealMac"] = ("在访达中显示", "Show in Finder"),
        ["RevealWindows"] = ("在资源管理器中显示", "Show in File Explorer"),
        ["Redo"] = ("重新转换", "Convert again"),
        ["RemoveRow"] = ("移除", "Remove"),
        ["AppearanceLanguage"] = ("外观与语言", "Appearance & language"),
        ["AppearanceTitle"] = ("选择你喜欢的外观", "Make it feel like you"),
        ["AppearanceHint"] = ("主题即时生效，语言独立选择。", "Theme and language changes apply immediately."),
        ["Theme"] = ("主题", "Theme"),
        ["Light"] = ("浅色", "Light"),
        ["Dark"] = ("深色", "Dark"),
        ["System"] = ("跟随系统", "System"),
        ["Language"] = ("界面语言", "Language"),
        ["LanguageHint"] = ("默认中文，可选择英文。", "Chinese by default; English is also available."),
        ["SystemHint"] = ("跟随系统时，主题随系统深浅色自动切换。系统文件对话框和系统菜单项的语言由操作系统决定。", "System theme follows your device. System dialogs and system-owned menu items follow the OS language."),
        ["OutputTitle"] = ("转换结果放在哪里", "Choose an output location"),
        ["OutputHint"] = ("默认输出位置会被记住，主窗口中也可以更改。", "Your default location is remembered and can be changed in the main window."),
        ["Overwrite"] = ("同名输出", "Existing outputs"),
        ["OverwriteValue"] = ("覆盖已有文件", "Replace existing files"),
        ["OverwriteHint"] = ("成功转换并校验后才替换；失败或取消保留旧文件。", "Replace only after validation. Failure or cancellation keeps the existing file."),
        ["QualityTitle"] = ("音质与压缩", "Audio quality & compression"),
        ["QualityHint"] = ("用于非 MP3 音轨的编码。高品质为默认设置。", "Encoding quality for non-MP3 audio. High is the default."),
        ["HighCard"] = ("高品质", "High quality"),
        ["BalancedCard"] = ("标准品质", "Balanced"),
        ["HighDetail"] = ("优先保留细节", "Prioritize detail"),
        ["BalancedDetail"] = ("音质与体积平衡", "Quality / size balance"),
        ["CompactDetail"] = ("更节省空间", "Smaller files"),
        ["VbrHigh"] = ("VBR · 质量 0", "VBR · Quality 0"),
        ["VbrBalanced"] = ("VBR · 质量 2", "VBR · Quality 2"),
        ["VbrCompact"] = ("VBR · 质量 5", "VBR · Quality 5"),
        ["DefaultQuality"] = ("默认 / 推荐", "Default / Recommended"),
        ["VbrHint"] = ("VBR 根据声音内容分配码率，文件体积不固定。品质更高不意味着能恢复源文件已经丢失的细节。", "VBR allocates bitrate based on the content, so file sizes vary. Higher quality cannot restore detail already lost in the source."),
        ["BitrateHint"] = ("适合设备兼容或有固定码率要求的情况。", "For device compatibility or fixed-bitrate requirements."),
        ["OutputProtection"] = ("只写出真正的 MP3，不修改源文件。输出目录不可写时会明确提示，不会偷偷换到其他位置。", "Outputs are real MP3 files; sources stay unchanged. An unwritable destination is reported, never silently changed."),
        ["PreferencesRemembered"] = ("设置下次启动仍保留", "Settings persist across launches"),
        ["CopyLossyHint"] = ("解密或提取得到的音轨已是 MP3 时直接复制，不会重复编码；因此品质选项不改变这些文件。其他音轨转为 MP3 属于有损转换。", "MP3 audio is copied without re-encoding, so quality settings do not affect it. Encoding other audio to MP3 is lossy."),
        ["Bitrate"] = ("高级：固定码率", "Advanced: fixed bitrate"),
        ["UseVbr"] = ("使用 VBR 预设", "Use VBR preset"),
        ["KeysTitle"] = ("密钥与客户端登录", "Keys & client sign-in"),
        ["KeysHint"] = ("需要密钥的 QQ 音乐文件使用客户端登录态按需获取。", "Use client sign-in credentials to request keys for QQ Music files when needed."),
        ["QqAuto"] = ("QQ 音乐 · 自动获取", "QQ Music · Automatic lookup"),
        ["QqExplanation"] = ("macOS：读取本机 QQ 音乐客户端登录态，需要时联网换取歌曲密钥。客户端无需一直运行。", "macOS: use local QQ Music sign-in credentials to request song keys online. The client does not need to keep running."),
        ["CheckLogin"] = ("重新检测", "Check again"),
        ["LoginHelp"] = ("登录指引", "Sign-in help"),
        ["NotChecked"] = ("尚未检测", "Not checked"),
        ["Checking"] = ("正在检测…", "Checking…"),
        ["LoginAvailable"] = ("已找到本机登录态", "Local sign-in found"),
        ["LoginNotVerified"] = ("只确认本机有登录信息，是否过期或有歌曲权限会在转换时验证。", "Local sign-in exists; expiry and song permissions are checked during conversion."),
        ["LoginGuide"] = ("1. 打开 QQ 音乐客户端。\n2. 登录下载这些歌曲的账号。\n3. 回到 ToMP3 重新检测并转换。\n\n若仍提示过期，请在客户端退出后重新登录；账号必须拥有相应歌曲权限。", "1. Open the QQ Music client.\n2. Sign in with the account that downloaded the songs.\n3. Return to ToMP3, check again, and convert.\n\nIf credentials have expired, sign out and back in through QQ Music. Your account must have access to the songs."),
        ["KeyPrivacy"] = ("不显示原始凭据，不保存到设置文件或日志，也不缓存歌曲密钥。ToMP3 不修改客户端登录数据；本版不提供手动密钥或数据库导入。", "Credentials stay in memory, never in settings or logs. Song keys are not cached. ToMP3 does not modify client sign-in data. Manual key and database import are not available."),
        ["SettingsSaved"] = ("自动保存 · 转换设置下次开始时生效", "Auto-save · Conversion changes apply to the next batch"),
        ["SaveFailed"] = ("设置保存失败，请检查目录权限：{0}", "Settings could not be saved. Check directory permissions: {0}"),
        ["ResetPage"] = ("恢复本页默认", "Reset this page"),
        ["Close"] = ("关闭", "Close"),
        ["AboutSubtitle"] = ("音视频转 MP3 工具", "Audio & video to MP3"),
        ["AboutDescription"] = ("批量解密、转码音频，或提取视频首条音轨，统一输出 MP3。支持文件夹导入、品质设置和中英文界面。", "Batch decrypt and convert audio, or extract the first audio track from video. Includes folder import, quality settings, and Chinese and English interfaces."),
        ["AboutTools"] = ("内置工具：ncmdump 1.5.1 · Unlock Music 0.2.12 · FFmpeg 8.0.3", "Bundled: ncmdump 1.5.1 · Unlock Music 0.2.12 · FFmpeg 8.0.3"),
        ["AboutLossy"] = ("最终输出均为 MP3：已有 MP3 音轨不重复编码，其他格式转为有损 MP3。无需额外安装转换工具。", "All outputs are MP3: existing MP3 audio is not re-encoded; other audio is encoded lossily. No additional conversion tools are required."),
        ["Version"] = ("版本 {0}（构建 {1}）", "Version {0} (build {1})"),
        ["Waiting"] = ("等待", "Waiting"), ["Converting"] = ("转换中", "Converting"),
        ["Done"] = ("完成", "Done"), ["Failed"] = ("失败", "Failed"),
        ["Skipped"] = ("跳过", "Skipped"), ["Canceled"] = ("取消", "Canceled"),
        ["NoFiles"] = ("还没有文件", "No files yet"),
        ["BatchSummary"] = ("完成 {0} · 失败 {1} · 等待 {2}", "{0} done · {1} failed · {2} waiting"),
        ["ExtraSummary"] = (" · 跳过 {0} · 取消 {1}", " · {0} skipped · {1} canceled"),
        ["ConvertingFile"] = ("正在转换 {0}/{1} · {2}", "Converting {0}/{1} · {2}"),
        ["Stopping"] = ("正在停止…", "Stopping…"),
        ["AllDone"] = ("都转完了，右键“重新转换”可重跑。", "All done. Use “Convert again” in the context menu to rerun a file."),
        ["NoOutput"] = ("还没有可打开的输出目录", "No output folder is available yet"),
        ["NoSupportedFiles"] = ("文件夹中没有支持的音频或视频文件", "No supported audio or video files found in the folder"),
        ["ReadFailed"] = ("无法读取 {0}：{1}", "Cannot read {0}: {1}"),
        ["OpenFailed"] = ("无法打开位置：{0}", "Cannot open location: {0}"),
        ["SelectSources"] = ("选择文件夹 / 文件", "Select files / folders"),
        ["Select"] = ("选择", "Select"),
        ["SelectHint"] = ("可同时选择音频、视频文件和文件夹；按住 Command 多选，文件夹会递归查找支持的格式。", "Select audio, video, and folders together. Hold Command to select multiple items. Folders are scanned recursively."),
        ["SelectFiles"] = ("选择音频或视频文件", "Select audio or video files"),
        ["SelectFolders"] = ("选择文件夹", "Select folders"),
        ["SelectOutput"] = ("转换结果放到这里", "Choose an output folder"),
        ["BusyOptions"] = ("本批次设置已固定；修改将在下次开始时生效。", "This batch uses fixed settings. Changes apply to the next batch.")
    };

    public static string Get(string key, params object[] args)
    {
        var pair = Strings[key];
        var value = Language == AppLanguage.English ? pair.En : pair.Zh;
        return args.Length == 0 ? value : string.Format(CultureInfo.InvariantCulture, value, args);
    }

    public static void Apply(AppLanguage language, Application app)
    {
        Language = language;
        foreach (var key in Strings.Keys) app.Resources["T_" + key] = Get(key);
    }

    public static string Quality(Mp3Quality quality) => Get(quality switch
    { Mp3Quality.Compact => "CompactLong", Mp3Quality.Balanced => "BalancedLong", _ => "HighLong" });

    public static string Category(string value) => Language != AppLanguage.English ? value : value switch
    {
        "网易云音乐" => "NetEase", "QQ 音乐" => "QQ Music", "酷狗音乐" => "KuGou",
        "酷我音乐" => "Kuwo", "虾米音乐" => "Xiami", "喜马拉雅" => "Ximalaya",
        "百度音乐" => "Baidu", "咪咕音乐" => "Migu", "普通音频" => "Audio", "视频" => "Video", _ => value
    };

    // 转换内核保留稳定的中文原因。界面翻译已知应用消息；工具诊断、文件名等原文保持不变。
    static readonly (string Zh, string En)[] Errors =
    [
        ("Windows 版暂不支持联网取 QQ 音乐密钥：请改用 64 位 macOS 版，或在 Windows 上先手动解密这些文件", "Online QQ Music key retrieval is not supported on Windows. Use the Apple Silicon macOS version, or decrypt the files first."),
        ("需要 QQ 音乐客户端的登录态：请打开 QQ 音乐，用下载这些歌的账号登录一次，再回来重新转换", "Open QQ Music and sign in with the account that downloaded these songs, then retry."),
        ("QQ 音乐接口没返回密钥信息，登录可能已过期：在客户端重新登录后重试", "QQ Music returned no key information. Sign in again through the client, then retry."),
        ("um 认不出这个文件：可能不是加密音频，或者密钥在客户端数据库里（如酷狗 kgg）", "This file could not be decrypted. It may require a client key database (such as KuGou KGG), which is not supported by this app."),
        ("文件中没有可提取的音轨，请选择包含音频的文件", "No audio track found. Choose a file that contains audio."),
        ("应用内置 FFmpeg 文件缺失，请重新安装完整的 ToMP3", "Bundled FFmpeg is missing. Reinstall the complete ToMP3 application."),
        ("覆盖了已存在的同名文件", "Replaced an existing file"), ("已取消", "Canceled"),
        ("不支持此文件格式：", "Unsupported file format: "),
        ("MP3 转码失败：", "MP3 encoding failed: "), ("MP3 校验失败：", "MP3 validation failed: "),
        ("无法识别文件中的音频：", "Cannot identify the audio: "),
        ("找不到内置 ncmdump（", "Bundled ncmdump is missing ("),
        ("找不到内置 um（", "Bundled um is missing ("),
        ("ncmdump 没有输出", "ncmdump produced no output"), ("um 没有输出", "um produced no output"),
        ("取到 QQ 音乐密钥后仍解密失败：", "Decryption failed after retrieving a QQ Music key: "),
        ("文件尾没有 musicex 元数据", "No musicex metadata found"),
        ("QQ 音乐返回的密钥长度异常，无法解密", "QQ Music returned an invalid key length"),
        ("源文件读取被截断", "Source file was truncated"),
        ("读 QQ 音乐登录信息失败", "Cannot read QQ Music sign-in information"),
        ("联网取 QQ 音乐密钥失败：", "Could not retrieve a QQ Music key online: "),
        ("联网取 QQ 音乐密钥超时", "QQ Music key request timed out")
    ];

    public static string Error(string value)
    {
        if (Language != AppLanguage.English || value.Length == 0) return value;
        foreach (var (zh, en) in Errors)
            if (value.StartsWith(zh, StringComparison.Ordinal)) return en + value[zh.Length..];
        if (value.StartsWith("QQ 音乐接口返回 ", StringComparison.Ordinal))
            return "QQ Music returned an HTTP error. Please retry later.";
        if (value.StartsWith("QQ 音乐没给这首歌的密钥", StringComparison.Ordinal))
            return "QQ Music did not grant a key for this song. Sign in again with the downloading account and check song and subscription permissions.";
        return value;
    }
}
