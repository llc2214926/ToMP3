using System.Globalization;

namespace ToMP3;

/// <summary>一次转换的不可变编码参数；MP3 音轨直接复制时不使用它。</summary>
public sealed record Mp3Encoding(Mp3Quality Quality = Mp3Quality.High, int Bitrate = 0)
{
    public string[] Arguments()
    {
        if (!AppSettings.Bitrates.Contains(Bitrate)) throw new ArgumentOutOfRangeException(nameof(Bitrate));
        if (Bitrate != 0) return ["-b:a", Bitrate.ToString(CultureInfo.InvariantCulture) + "k"];
        return ["-q:a", Quality switch
        {
            Mp3Quality.High => "0", Mp3Quality.Balanced => "2", Mp3Quality.Compact => "5",
            _ => throw new ArgumentOutOfRangeException(nameof(Quality))
        }];
    }
}
