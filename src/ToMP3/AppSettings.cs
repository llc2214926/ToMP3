using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ToMP3;

public enum AppAppearance { System, Light, Dark }
public enum AppLanguage { Chinese, English }
public enum Mp3Quality { High, Balanced, Compact }

/// <summary>只保存普通偏好；客户端凭据、歌曲密钥不属于设置文件。</summary>
public sealed record AppSettings
{
    public AppAppearance Appearance { get; init; } = AppAppearance.System;
    public AppLanguage Language { get; init; } = AppLanguage.Chinese;
    public string OutputDirectory { get; init; } = "";
    public Mp3Quality Quality { get; init; } = Mp3Quality.High;
    /// <summary>0 使用 VBR 预设；其他值为 CBR 的 kbps。</summary>
    public int Bitrate { get; init; }

    public static readonly int[] Bitrates = [0, 128, 192, 256, 320];

    public static string SettingsDirectory => OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ToMP3")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "ToMP3");

    public AppSettings Validated() => this with
    {
        Appearance = Enum.IsDefined(Appearance) ? Appearance : AppAppearance.System,
        Language = Enum.IsDefined(Language) ? Language : AppLanguage.Chinese,
        Quality = Enum.IsDefined(Quality) ? Quality : Mp3Quality.High,
        Bitrate = Bitrates.Contains(Bitrate) ? Bitrate : 0,
        OutputDirectory = OutputDirectory ?? ""
    };

    public static AppSettings Load()
    {
        try
        {
            var path = Path.Combine(SettingsDirectory, "settings.json");
            if (!File.Exists(path)) return new();
            return (JsonSerializer.Deserialize(File.ReadAllText(path), SettingsJsonContext.Default.AppSettings)
                ?? new AppSettings()).Validated();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // 配置损坏时使用默认值，不把文件正文（或任何凭据）写入日志。
            return new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var path = Path.Combine(SettingsDirectory, "settings.json");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary,
                JsonSerializer.Serialize(Validated(), SettingsJsonContext.Default.AppSettings), new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJsonContext : JsonSerializerContext;
