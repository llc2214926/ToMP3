using System.Diagnostics;
using System.Text.Json;

namespace ToMP3;

/// <summary>单个文件的转换结果。Ok=false 时 <see cref="Detail"/> 是原因；Ok=true 时
/// <see cref="Output"/> 是已验证的最终 MP3 路径，界面用它在访达里定位。</summary>
public sealed record ConvertResult(bool Ok, string Detail, string? Output = null)
{
    public static ConvertResult Fail(string why) => new(false, why);
}

/// <summary>一种「转 mp3」的转换器：认领的输入扩展名 + 单文件的转换动作。
/// 加新格式 = 在 <see cref="FormatConvert.All"/> 里加一条，界面按扩展名自动认领，不用改界面。</summary>
public sealed record Converter(string Name, string[] Inputs,
    Func<string, string, CancellationToken, Task<ConvertResult>> Run);

/// <summary>「各种格式 → mp3」的转换器表。两个 vendor 进来的可执行文件，都不重写解密逻辑：
/// <list type="bullet">
/// <item>`ncmdump` 管 .ncm（自己解包，会把封面/标签写进产物）</item>
/// <item>`um`（Unlock Music CLI）管其余各家私有加密格式——QQ/酷狗/酷我/虾米/喜马拉雅/百度/咪咕</item>
/// </list>
/// 两者都在 <c>Assets/</c> 下，各自的 README 写了出处、版本、许可和换版本的坑。</summary>
public static class FormatConvert
{
    /// <summary>随包发布的两个可执行文件：相对 AppContext.BaseDirectory 找，publish 到
    /// ToMP3.app/Contents/MacOS 后相对路径不变。</summary>
    static readonly string Ncmdump = Path.Combine(AppContext.BaseDirectory, "Assets", "ncmdump", "ncmdump");
    static readonly string Um = Path.Combine(AppContext.BaseDirectory, "Assets", "um", "um");

    /// <summary>两家工具日志里的 ANSI 颜色序列（SGR）。</summary>
    static readonly System.Text.RegularExpressions.Regex Ansi =
        new("\u001b\\[[0-9;]*m", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>um 认领的加密扩展名，按平台分组（`um --supported-ext` 里挑出来的，纯
    /// mp3/flac/wav/ogg/m4a/aac/wma 是它的 noop 解码器、默认关掉，不走 um——那些归
    /// <see cref="PlainAudio"/>，直接交给 FFmpeg 转码）。
    /// **有双点后缀**（`kgm.flac`、`vpr.flac`），所以匹配走「文件名后缀」而不是 Path.GetExtension。
    /// 分组同时喂了界面那行「支持 …」，加扩展名改这里就够了。</summary>
    static readonly (string Platform, string[] Exts)[] UmGroups =
    [
        ("QQ 音乐", [
            ".qmc0", ".qmc2", ".qmc3", ".qmc4", ".qmc6", ".qmc8", ".qmcflac", ".qmcogg",
            ".mflac", ".mflac0", ".mflac1", ".mflaca", ".mflach", ".mflacl", ".mflacm",
            ".mgg", ".mgg0", ".mgg1", ".mgga", ".mggh", ".mggl", ".mggm",
            ".tkm", ".tm0", ".tm2", ".tm3", ".tm6"]),
        ("酷狗音乐", [".kgm", ".kgma", ".kgm.flac", ".kgg", ".vpr", ".vpr.flac"]),
        ("酷我音乐", [".kwm"]),
        ("虾米音乐", [".xm"]),
        ("喜马拉雅", [".x2m", ".x3m"]),
        ("百度音乐", [".bkcmp3", ".bkcflac", ".bkcogg", ".bkcwav", ".bkcwma", ".bkcm4a", ".bkcape"]),
        ("咪咕音乐", [".mmp4"]),
    ];

    /// <summary>不加密、ffmpeg 直接能读的常见音频：只转码，不解密。`.mp3` 故意不收——
    /// 已经是 MP3 不该重复编码，输出目录等于源目录时还会覆盖用户原文件。</summary>
    static readonly string[] PlainAudio =
        [".flac", ".ogg", ".oga", ".opus", ".m4a", ".aac", ".wav", ".aiff", ".aif", ".wma", ".ape"];

    /// <summary>登记顺序就是匹配优先级：加密格式必须排在 <see cref="PlainAudio"/> 前面——
    /// `.kgm.flac`、`.vpr.flac` 也以 `.flac` 结尾，<see cref="Match"/> 取第一个命中的转换器。</summary>
    public static readonly Converter[] All =
    [
        new Converter("网易云音乐", [".ncm"], (src, dir, ct) => ToMp3(src, dir, ct, NcmToMp3)),
        .. UmGroups.Select(g => new Converter(g.Platform, g.Exts,
            (src, dir, ct) => ToMp3(src, dir, ct, UmToAudio))),
        new Converter("普通音频", PlainAudio, (src, dir, ct) =>
            ToMp3(src, dir, ct, (s, _, _) => Task.FromResult(new ConvertResult(true, "", s)))),
    ];

    /// <summary>按扩展名找认领的转换器；不认识返回 null（界面标「跳过」）。
    /// 后缀匹配（不是 <see cref="Path.GetExtension"/>）：um 的 `kgm.flac`、`vpr.flac` 是双点后缀。</summary>
    public static Converter? Match(string path) =>
        All.FirstOrDefault(c => c.Inputs.Any(x => path.EndsWith(x, StringComparison.OrdinalIgnoreCase)));

    /// <summary>认领的扩展名总数（界面上写「支持 N 种」）。</summary>
    public static int Count => All.Sum(c => c.Inputs.Length);

    /// <summary>界面上那行「支持什么格式」：按平台分组、摊开全部扩展名（含 ncm 和普通音频）。数据驱动——
    /// 加平台/扩展名只改 <see cref="All"/> 与 <see cref="UmGroups"/>，界面自动跟着变。</summary>
    public static string SupportList =>
        $"支持 {Count} 种格式：" + string.Join(" ｜ ",
            All.Select(c => $"{c.Name} {string.Join(' ', c.Inputs.Select(x => x.TrimStart('.')))}"));

    // 所有解密器共用：隔离中间文件，只有最终 MP3 验证成功才替换目标。
    static async Task<ConvertResult> ToMp3(string src, string outDir, CancellationToken ct,
        Func<string, string, CancellationToken, Task<ConvertResult>> decrypt)
    {
        if (ct.IsCancellationRequested) return ConvertResult.Fail("已取消");
        var ffmpeg = Path.Combine(AppContext.BaseDirectory, "Assets", "ffmpeg", "ffmpeg");
        var ffprobe = Path.Combine(AppContext.BaseDirectory, "Assets", "ffmpeg", "ffprobe");
        if (!File.Exists(ffmpeg) || !File.Exists(ffprobe))
            return ConvertResult.Fail("应用内置 FFmpeg 文件缺失，请重新安装完整的 ToMP3.app");
        EnsureExecutable(ffmpeg);
        EnsureExecutable(ffprobe);

        Directory.CreateDirectory(outDir);
        var work = Directory.CreateDirectory(Path.Combine(Path.GetFullPath(outDir), ".tomp3-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var decoded = await decrypt(src, work, ct);
            if (!decoded.Ok) return decoded;
            var audio = decoded.Output!;
            var probe = await Run(Command(ffprobe, "-v", "error", "-select_streams", "a:0",
                "-show_entries", "stream=codec_name", "-of", "default=noprint_wrappers=1:nokey=1", audio), ct);
            if (probe.Canceled) return ConvertResult.Fail("已取消");
            if (probe.ExitCode != 0 || probe.Text.Length == 0)
                return ConvertResult.Fail("无法识别解密后的音频：" + probe.Text);

            var output = Path.Combine(Path.GetFullPath(outDir), Path.GetFileNameWithoutExtension(audio) + ".mp3");
            // 避免解密后的文件恰好叫 final.mp3 时输入输出相同。
            var encodedDir = Directory.CreateDirectory(Path.Combine(work, "encoded")).FullName;
            var staged = Path.Combine(encodedDir, "final.mp3");
            var args = new List<string> { "-nostdin", "-v", "error", "-y", "-i", audio,
                "-map", "0:a:0", "-map", "0:v?", "-map_metadata", "0", "-c:v", "copy", "-c:a" };
            args.Add(probe.Text == "mp3" ? "copy" : "libmp3lame");
            if (probe.Text != "mp3") args.AddRange(["-q:a", "2"]);
            args.AddRange(["-id3v2_version", "3", "-f", "mp3", staged]);
            var encoded = await Run(Command(ffmpeg, args.ToArray()), ct);
            if (encoded.Canceled) return ConvertResult.Fail("已取消");
            if (encoded.ExitCode != 0 || !File.Exists(staged) || new FileInfo(staged).Length == 0)
                return ConvertResult.Fail("MP3 转码失败：" + encoded.Text);
            // 真正解码检查，不能把扩展名正确但损坏或截断的文件报告为成功。
            var verified = await Run(Command(ffmpeg, "-nostdin", "-v", "error", "-xerror",
                "-i", staged, "-map", "0:a:0", "-f", "null", "-"), ct);
            if (verified.Canceled || ct.IsCancellationRequested) return ConvertResult.Fail("已取消");
            if (verified.ExitCode != 0) return ConvertResult.Fail("MP3 校验失败：" + verified.Text);
            var existed = File.Exists(output);
            File.Move(staged, output, true);
            return new ConvertResult(true, string.Join(" · ", new[] { decoded.Detail,
                existed ? "覆盖了已存在的同名文件" : "" }.Where(s => s.Length > 0)), output);
        }
        finally
        {
            // 中间产物只移入废纸篓，不直接删除；不会触碰源文件。
            var trash = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Trash");
            try
            {
                Directory.CreateDirectory(trash);
                Directory.Move(work, Path.Combine(trash, "ToMP3-" + Guid.NewGuid().ToString("N")));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning($"中间文件保留在 {work}：{ex.Message}");
            }
        }
    }


    static ProcessStartInfo Command(string tool, params string[] args)
    {
        var psi = new ProcessStartInfo(tool)
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        return psi;
    }

    // ── ncmdump：.ncm ────────────────────────────────────────────────────────────

    /// <summary>ncmdump 的调用是 `ncmdump 文件 -o 输出目录`。**它任何情况都退 0**
    /// （文件不存在、不是 ncm，也只是打一行 [Error]/[Exception]），所以不能看退出码，
    /// 只能解析那行 `[Done] '源' -> '产物'`——顺便拿到真实产物路径。日志带 ANSI 色码、
    /// 走哪条流也不固定（反正 stdout/stderr 都读），所以先合并再剥色码。</summary>
    static async Task<ConvertResult> NcmToMp3(string src, string outDir, CancellationToken ct)
    {
        if (!File.Exists(Ncmdump)) return ConvertResult.Fail("找不到内置 ncmdump（Assets/ncmdump/ncmdump）");
        EnsureExecutable(Ncmdump);


        var psi = new ProcessStartInfo(Ncmdump)
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add(src);      // 绝对路径、不拼 shell 串
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(outDir);

        var (outText, canceled, _) = await Run(psi, ct);
        if (canceled) return ConvertResult.Fail("已取消");

        string? produced = null;
        foreach (var line in outText.Split('\n'))
        {
            if (!line.StartsWith("[Done]", StringComparison.Ordinal)) continue;
            int k = line.IndexOf("-> '", StringComparison.Ordinal);
            if (k >= 0) produced = line[(k + 4)..].TrimEnd().TrimEnd('\'');
        }
        if (produced is null || !File.Exists(produced))
            return ConvertResult.Fail(Line(outText, "[Error]")
                                   ?? Line(outText, "[Exception]")
                                   ?? (outText.Length > 0 ? outText : "ncmdump 没有输出"));

        var notes = new List<string>();
        int warns = outText.Split('\n').Count(l => l.StartsWith("[Warn]", StringComparison.Ordinal));
        if (warns > 0) notes.Add($"ncmdump 打了 {warns} 条警告（不影响产物）");
        return new ConvertResult(true, string.Join(" · ", notes), produced);
    }

    // ── um：其余各家私有加密格式 ──────────────────────────────────────────────────

    /// <summary>um 的调用是 `um --overwrite -o 输出目录 文件`。退出码可信（0 成功 / 1 失败），
    /// 成功与否仍以 `successfully converted` 那行日志为准——顺便从它的 `destination` 拿真实产物路径
    /// （um 按内容嗅探 mp3/flac/ogg/m4a，不是照抄输入后缀）。日志走 stderr、格式是 zap 的
    /// `时间 \t 级别 \t 消息 \t JSON`，级别带 ANSI 色码，解析前先剥。</summary>
    static async Task<ConvertResult> UmToAudio(string src, string outDir, CancellationToken ct)
    {
        if (!File.Exists(Um)) return ConvertResult.Fail("找不到内置 um（Assets/um/um）");
        EnsureExecutable(Um);


        var psi = new ProcessStartInfo(Um)
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("--overwrite");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(outDir);
        psi.ArgumentList.Add(src);

        var (outText, canceled, _) = await Run(psi, ct);
        if (canceled) return ConvertResult.Fail("已取消");

        string? produced = null, why = null;
        foreach (var line in outText.Split('\n'))
        {
            var t = line.Split('\t');
            if (t.Length < 4) continue;
            if (t[2] == "successfully converted") produced = Json(line, "destination");
            // 失败时会先打几条 WARN（探测各解码器），最后一行 FATAL 才是真正的原因
            else if (t[2] == "run app failed") why = Json(line, "error");
        }
        if (produced is null || !File.Exists(produced))
            // 新版 QQ 音乐的 mflac/mgg（文件尾 `musicex`）把 ekey 存在客户端里，文件内没有密钥；
            // um 找不到密钥库时只会报 "no any decoder can resolve the file"，照抄对用户没用。
            return ConvertResult.Fail(outText.Contains("read key from mmkv failed", StringComparison.Ordinal)
                ? "密钥（ekey）不在文件里，要从音乐客户端的密钥库取；本机没有可用密钥库，无法解密"
                : why ?? (outText.Length > 0 ? outText : "um 没有输出"));

        return new ConvertResult(true, "", produced);
    }

    // ── 两家的共用部分 ───────────────────────────────────────────────────────────

    /// <summary>并发读取两个日志流；取消后等待子进程退出，避免清理时仍在写文件。</summary>
    static async Task<(string Text, bool Canceled, int ExitCode)> Run(ProcessStartInfo psi, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return ("", true, -1);
        using var p = Process.Start(psi)!;
        using var reg = ct.Register(() => { try { p.Kill(true); } catch { } });
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();

        var text = Ansi.Replace(string.Join('\n', new[] { (await stderr).Trim(), (await stdout).Trim() }
            .Where(s => s.Length > 0)), "");
        return (text, ct.IsCancellationRequested, p.ExitCode);
    }

    /// <summary>取 zap 日志行末尾 JSON 字段（`successfully converted` 的 destination、FATAL 的 error）。</summary>
    static string? Json(string line, string key)
    {
        int i = line.LastIndexOf('\t');
        if (i < 0) return null;
        try
        {
            using var doc = JsonDocument.Parse(line[(i + 1)..]);
            return doc.RootElement.TryGetProperty(key, out var v) ? v.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>取第一条以 tag 开头的行（去掉 tag），没有则 null。</summary>
    static string? Line(string text, string tag) => text.Split('\n')
        .FirstOrDefault(l => l.StartsWith(tag, StringComparison.Ordinal))?[tag.Length..].Trim();

    /// <summary>有些拷贝途径会丢执行位，跑之前补一下。</summary>
    static void EnsureExecutable(string path)
    {
        if (!OperatingSystem.IsMacOS()) return;
        try
        {
            var mode = File.GetUnixFileMode(path);
            if ((mode & UnixFileMode.UserExecute) == 0)
                File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute);
        }
        catch (IOException) { }
    }
}
