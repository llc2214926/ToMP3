using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace ToMP3;

/// <summary>新版 QQ 音乐（macOS 客户端 19.57+）下载的 `.mflac`/`.mgg` **不再把 ekey 写进文件**，
/// 文件尾只留 `musicex` 元数据（歌曲 mid、资源文件名），ekey 要用客户端的登录凭据向官方接口换。
/// 这里把那条路补上：读文件尾 → 从客户端登录态取 uin/authst → 换 ekey →
/// 把 ekey 按**旧版「裸密钥」尾部**写进临时副本（这正是老版客户端自己的写法，um 直接认）。
/// 解密仍然全部由 um 完成，应用里不实现 QMC 算法；用户的原文件只读不改。
/// 失败一律抛带中文原因的 <see cref="InvalidOperationException"/>，由 <see cref="FormatConvert"/> 当失败原因显示。</summary>
static class QqEkey
{
    /// <summary>客户端登录态。注意不在沙盒容器里，而是普通的 Preferences。</summary>
    static readonly string Prefs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Preferences", "com.tencent.QQMusicMac.plist");

    const string NeedLogin = "需要 QQ 音乐客户端的登录态：请打开 QQ 音乐，用下载这些歌的账号登录一次，再回来重新转换";

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>文件尾 16 字节是 `[4]tagSize [4]version=1 [8]"musicex\0"`，tagSize **含**这 16 字节，
    /// 所以加密音频段就是 `文件长度 - tagSize`。元数据结构紧贴尾部：相对结构开头 +0x0C 起 60 字节是歌曲 mid、
    /// +0x48 起 68 字节是资源文件名，都是定长 UTF-16LE、NUL 截断。不是这个格式就返回 null（老格式 um 自己能解）。</summary>
    static (int TagSize, string Mid, string FileName)? ReadTag(string path)
    {
        using var f = File.OpenRead(path);
        if (f.Length < 0xC0) return null;
        Span<byte> tail = stackalloc byte[16];
        f.Seek(-16, SeekOrigin.End);
        f.ReadExactly(tail);
        if (!tail[8..].SequenceEqual("musicex\0"u8)) return null;
        int size = (int)BinaryPrimitives.ReadUInt32LittleEndian(tail);
        if (BinaryPrimitives.ReadUInt32LittleEndian(tail[4..]) != 1 || size < 0xC0 || size > f.Length) return null;
        var body = new byte[size - 16];
        f.Seek(-size, SeekOrigin.End);
        f.ReadExactly(body);
        return (size, Text(body, 0x0C, 60), Text(body, 0x48, 68));
    }

    /// <summary>定长 UTF-16LE 字段，取到第一个 NUL。</summary>
    static string Text(byte[] buf, int offset, int length)
    {
        var s = Encoding.Unicode.GetString(buf, offset, Math.Min(length, buf.Length - offset));
        int end = s.IndexOf('\0');
        return end < 0 ? s : s[..end];
    }

    /// <summary>这个文件是不是「密钥在服务器」的新版格式。</summary>
    public static bool IsMusicEx(string path) => ReadTag(path) is not null;

    /// <summary>把源文件的加密音频段 + 取回的 ekey 写成 <paramref name="workDir"/> 里的临时副本并返回路径。
    /// 尾部格式 = ekey 原文 + 4 字节小端长度，um 的旧版「裸密钥」分支直接认。</summary>
    public static async Task<string> RawKeyCopy(string src, string workDir, CancellationToken ct)
    {
        var tag = ReadTag(src) ?? throw new InvalidOperationException("文件尾没有 musicex 元数据");
        var (uin, authst, loginType) = await Credentials(ct);
        var key = Encoding.ASCII.GetBytes(await FetchEkey(tag, uin, authst, loginType, ct));
        if (key.Length is 0 or > 0xFFFF) throw new InvalidOperationException("QQ 音乐返回的密钥长度异常，无法解密");

        var dst = Path.Combine(workDir, Path.GetFileName(src));
        await using var input = File.OpenRead(src);
        await using var output = File.Create(dst);
        var buf = new byte[1 << 20];
        long left = input.Length - tag.TagSize;
        while (left > 0)
        {
            int n = await input.ReadAsync(buf.AsMemory(0, (int)Math.Min(buf.Length, left)), ct);
            if (n == 0) throw new InvalidOperationException("源文件读取被截断");
            await output.WriteAsync(buf.AsMemory(0, n), ct);
            left -= n;
        }
        await output.WriteAsync(key, ct);
        var len = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(len, (uint)key.Length);
        await output.WriteAsync(len, ct);
        return dst;
    }

    /// <summary>登录凭据在 prefs 的 `AutoLoginUserInfo`：NSKeyedArchiver 编码的二进制 plist。
    /// 交给系统 plutil 转 XML 再读，不自己解 plist 格式；凭据只在内存里用，不落盘、不进日志。</summary>
    static async Task<(string Uin, string Authst, string LoginType)> Credentials(CancellationToken ct)
    {
        if (!File.Exists(Prefs)) throw new InvalidOperationException(NeedLogin);
        // 装了客户端但从没登录过时，plutil 会以「No value at that key path」退 1，同样是没有登录态。
        byte[] archive;
        try { archive = Convert.FromBase64String((await Plutil(null, ct, "-extract", "AutoLoginUserInfo", "raw", "-o", "-", Prefs)).Trim()); }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        { throw new InvalidOperationException(NeedLogin); }

        var xml = XDocument.Parse(await Plutil(archive, ct, "-convert", "xml1", "-o", "-", "-"));
        var objects = xml.Descendants("key").FirstOrDefault(k => k.Value == "$objects")
            ?.ElementsAfterSelf().FirstOrDefault()?.Elements().ToList()
            ?? throw new InvalidOperationException(NeedLogin);
        var user = objects.FirstOrDefault(e => e.Name == "dict" && e.Elements("key").Any(k => k.Value == "strAuthst"))
            ?? throw new InvalidOperationException(NeedLogin);

        // 归档里的字符串是对象引用：<dict><key>CF$UID</key><integer>n</integer></dict> 指向 $objects[n]。
        string Field(string name)
        {
            var value = user.Elements("key").FirstOrDefault(k => k.Value == name)?.ElementsAfterSelf().FirstOrDefault();
            if (value is null) return "";
            if (value.Name == "dict" && value.Element("key")?.Value == "CF$UID")
                return int.TryParse(value.Element("integer")?.Value, out int i) && i >= 0 && i < objects.Count
                    ? objects[i].Value : "";
            return value.Value;
        }

        var (uin, authst) = (Field("nUserId"), Field("strAuthst"));
        if (uin.Length == 0 || authst.Length == 0) throw new InvalidOperationException(NeedLogin);
        var type = Field("loginType");
        return (uin, authst, type.Length > 0 ? type : "1");
    }

    /// <summary>调系统 plutil；<paramref name="stdin"/> 非空时从标准输入喂数据，避免把凭据写成临时文件。</summary>
    static async Task<string> Plutil(byte[]? stdin, CancellationToken ct, params string[] args)
    {
        var psi = new ProcessStartInfo("/usr/bin/plutil")
        {
            UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, RedirectStandardInput = stdin is not null,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        if (stdin is not null)
        {
            await p.StandardInput.BaseStream.WriteAsync(stdin, ct);
            p.StandardInput.Close();
        }
        var stdout = p.StandardOutput.ReadToEndAsync(ct);
        var stderr = p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0) throw new InvalidOperationException("读 QQ 音乐登录信息失败：" + (await stderr).Trim());
        return await stdout;
    }

    /// <summary>官方接口 `music.vkey.GetEVkey`：filename 必须带 `.mflac`/`.mgg` 后缀、songtype 必须是 1，
    /// 否则只回 vkey 不给 ekey；platform `20` 是 macOS 客户端。ekey 有效期约 22 小时，所以每次现取不缓存。</summary>
    static async Task<string> FetchEkey((int TagSize, string Mid, string FileName) tag,
        string uin, string authst, string loginType, CancellationToken ct)
    {
        var json = new MemoryStream();
        using (var w = new Utf8JsonWriter(json))
        {
            w.WriteStartObject();
            w.WriteStartObject("comm");
            w.WriteString("authst", authst);
            w.WriteString("ct", "19");
            w.WriteString("cv", "1859");
            w.WriteString("uin", uin);
            w.WriteString("tmeLoginType", loginType);
            w.WriteEndObject();
            w.WriteStartObject("req_1");
            w.WriteString("module", "music.vkey.GetEVkey");
            w.WriteString("method", "CgiGetEVkey");
            w.WriteStartObject("param");
            w.WriteStartArray("filename");
            w.WriteStringValue(tag.FileName);
            w.WriteEndArray();
            w.WriteString("guid", "10000");
            w.WriteStartArray("songmid");
            w.WriteStringValue(tag.Mid);
            w.WriteEndArray();
            w.WriteStartArray("songtype");
            w.WriteNumberValue(1);
            w.WriteEndArray();
            w.WriteString("uin", uin);
            w.WriteNumber("loginflag", 1);
            w.WriteString("platform", "20");
            w.WriteNumber("ctx", 1);
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteEndObject();
        }

        using var content = new ByteArrayContent(json.GetBuffer(), 0, (int)json.Length);
        content.Headers.ContentType = new("application/json");
        byte[] body;
        try
        {
            using var resp = await Http.PostAsync("https://u.y.qq.com/cgi-bin/musicu.fcg", content, ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"QQ 音乐接口返回 {(int)resp.StatusCode}，稍后重试");
            body = await resp.Content.ReadAsByteArrayAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("联网取 QQ 音乐密钥失败：" + ex.Message);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException("联网取 QQ 音乐密钥超时");
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("req_1", out var req)
            || !req.TryGetProperty("data", out var data)
            || !data.TryGetProperty("midurlinfo", out var list)
            || list.GetArrayLength() == 0)
            throw new InvalidOperationException("QQ 音乐接口没返回密钥信息，登录可能已过期：在客户端重新登录后重试");

        var item = list[0];
        var ekey = item.TryGetProperty("ekey", out var e) ? e.GetString() ?? "" : "";
        if (ekey.Length == 0)
        {
            var result = item.TryGetProperty("result", out var r) ? r.GetInt32() : -1;
            throw new InvalidOperationException(
                $"QQ 音乐没给这首歌的密钥（result={result}）：确认登录的是下载这些歌的账号、会员仍然有效，必要时在客户端重新登录");
        }
        return ekey;
    }
}
