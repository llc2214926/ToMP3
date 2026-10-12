using System.Diagnostics;
using System.Text.Json;

namespace ToMP3;

/// <summary>单个文件的转换结果。Ok=false 时 <see cref="Detail"/> 是原因；Ok=true 时
/// <see cref="Output"/> 是**实际产物路径**——不是猜的，转换器自己报，界面拿它去访达里定位
/// （加密文件里可能是 mp3 也可能是 flac，猜扩展名会指错文件）。</summary>
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
    /// mp3/flac/wav/ogg/m4a/aac/wma 属于它的 noop 解码器、默认关掉，不列——那些文件转不出新东西）。
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

    public static readonly Converter[] All =
    [
        new Converter("网易云音乐", [".ncm"], NcmToMp3),
        .. UmGroups.Select(g => new Converter(g.Platform, g.Exts, UmToAudio)),
    ];

    /// <summary>按扩展名找认领的转换器；不认识返回 null（界面标「跳过」）。
    /// 后缀匹配（不是 <see cref="Path.GetExtension"/>）：um 的 `kgm.flac`、`vpr.flac` 是双点后缀。</summary>
    public static Converter? Match(string path) =>
        All.FirstOrDefault(c => c.Inputs.Any(x => path.EndsWith(x, StringComparison.OrdinalIgnoreCase)));

    /// <summary>认领的加密扩展名总数（界面上写「支持 N 种」）。</summary>
    public static int Count => All.Sum(c => c.Inputs.Length);

    /// <summary>界面上那行「支持什么格式」：按平台分组、摊开全部扩展名（含 ncm）。数据驱动——
    /// 加平台/扩展名只改 <see cref="All"/> 与 <see cref="UmGroups"/>，界面自动跟着变。</summary>
    public static string SupportList =>
        $"支持 {Count} 种加密格式：" + string.Join(" ｜ ",
            All.Select(c => $"{c.Name} {string.Join(' ', c.Inputs.Select(x => x.TrimStart('.')))}"));

    // ── ncmdump：.ncm ────────────────────────────────────────────────────────────

    /// <summary>ncmdump 的调用是 `ncmdump 文件 -o 输出目录`。**它任何情况都退 0**
    /// （文件不存在、不是 ncm，也只是打一行 [Error]/[Exception]），所以不能看退出码，
    /// 只能解析那行 `[Done] '源' -> '产物'`——顺便拿到真实产物路径。日志带 ANSI 色码、
    /// 走哪条流也不固定（反正 stdout/stderr 都读），所以先合并再剥色码。</summary>
    static async Task<ConvertResult> NcmToMp3(string src, string outDir, CancellationToken ct)
    {
        if (!File.Exists(Ncmdump)) return ConvertResult.Fail("找不到内置 ncmdump（Assets/ncmdump/ncmdump）");
        EnsureExecutable(Ncmdump);

        // ncmdump 覆盖同名产物前不问一声，先记下已存在的，完事如实告诉用户
        var stem = Path.Combine(outDir, Path.GetFileNameWithoutExtension(src));
        var existed = new[] { stem + ".mp3", stem + ".flac" }.Where(File.Exists).ToHashSet();

        var psi = new ProcessStartInfo(Ncmdump)
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add(src);      // 绝对路径、不拼 shell 串
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(outDir);

        var (outText, canceled) = await Run(psi, ct);
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
        if (existed.Contains(produced)) notes.Add("覆盖了已存在的同名文件");
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

        // um 默认「产物已存在就跳过」（退出码还是 0，跟成功混一起），统一传 --overwrite 覆盖；
        // 覆盖与否自己记：先记下输出目录里已有的文件，完事看产物是不是原来就在。
        var existed = Directory.Exists(outDir)
            ? Directory.GetFiles(outDir).ToHashSet(StringComparer.Ordinal)
            : [];

        var psi = new ProcessStartInfo(Um)
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("--overwrite");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(outDir);
        psi.ArgumentList.Add(src);

        var (outText, canceled) = await Run(psi, ct);
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
            return ConvertResult.Fail(why ?? (outText.Length > 0 ? outText : "um 没有输出"));

        return new ConvertResult(true,
            existed.Contains(produced) ? "覆盖了已存在的同名文件" : "", produced);
    }

    // ── 两家的共用部分 ───────────────────────────────────────────────────────────

    /// <summary>跑一个子进程、合并 stdout/stderr 并剥掉 ANSI 色码，返回 (日志, 是否被取消)。
    /// 两个流都得读，不然管道写满后子进程卡死、WaitForExit 永远不回。</summary>
    static async Task<(string Text, bool Canceled)> Run(ProcessStartInfo psi, CancellationToken ct)
    {
        using var p = Process.Start(psi)!;
        using var reg = ct.Register(() => { try { p.Kill(true); } catch { } });
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        try { await p.WaitForExitAsync(ct); }
        catch (OperationCanceledException) { return ("", true); }

        var text = Ansi.Replace(string.Join('\n', new[] { (await stderr).Trim(), (await stdout).Trim() }
            .Where(s => s.Length > 0)), "");
        return (text, false);
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
