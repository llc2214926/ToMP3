# ToMP3

[中文](README.ZH.md) | English

A small desktop tool that **actually converts** encrypted audio from music platforms and common audio files into MP3, for macOS (Apple Silicon).

> **⚠ macOS only**: the Windows build is not part of this project's supported surface and is not described here.

Not a rename: when decryption yields FLAC, OGG, M4A and friends, the file is really transcoded with the bundled FFmpeg (libmp3lame, `-q:a 2`); files that are already MP3 are not re-encoded. Transcoding is lossy — the app never claims to preserve original audio quality.

## Supported formats

| Source | Formats |
|---|---|
| NetEase Cloud Music | `.ncm` |
| QQ Music | `.qmc0/2/3/4/6/8` `.qmcflac` `.qmcogg` `.mflac*` `.mgg*` `.tkm` `.tm0/2/3/6` |
| Kugou Music | `.kgm` `.kgma` `.kgm.flac` `.kgg` `.vpr` `.vpr.flac` |
| Kuwo Music | `.kwm` |
| Xiami Music | `.xm` |
| Ximalaya | `.x2m` `.x3m` |
| Baidu Music | `.bkc*` |
| Migu Music | `.mmp4` |
| Plain audio | `.flac` `.ogg` `.oga` `.opus` `.m4a` `.aac` `.wav` `.aiff` `.wma` `.ape` |

The decryptors and FFmpeg ship inside the app (`ncmdump`, `um`, FFmpeg 8.0.3 with statically linked LAME 3.100), so nothing has to be installed.

## Usage

1. Drag files or whole folders into the window (or pick them from the native menu).
2. Choose an output directory — source files are kept; same-name outputs overwrite.
3. Conversion runs serially and can be cancelled at any time; on failure or cancellation the previous target file is left intact.
4. Statuses (done, failed, skipped, …) are shown per row, and finished files can be revealed in Finder.

## Known limitations

- Newer QQ Music (macOS client 19.57+) stores the key of downloaded `.mflac`/`.mgg` files outside the file, so decrypting them needs network access and a QQ Music account that was signed in on this machine when the song was downloaded.
- Without a signed-in session, with expired credentials, or without permission for the account, the app gives a Chinese explanation and next steps instead of the raw English error.
- `.mp3` inputs are not claimed: an MP3 is not re-encoded.
- Linux and mobile platforms are out of scope.

## System requirements

- macOS 12+ (Apple Silicon)

## Build

Requires the .NET 8 SDK; see [AGENTS.md](AGENTS.md) for the full conventions.

```bash
# build
dotnet build src/ToMP3/ToMP3.csproj -c Release

# run the app from source
dotnet run --project src/ToMP3/ToMP3.csproj

# publish a self-contained macOS arm64 .app
dotnet publish src/ToMP3/ToMP3.csproj -c Release -r osx-arm64 --self-contained true -o dist/ToMP3.app/Contents/MacOS/
codesign --force --deep --sign - dist/ToMP3.app
```

## License

ToMP3 is released under the [MIT](LICENSE) license.

### Bundled third-party tools

| Tool | Purpose | License | Bundled contents |
|---|---|---|---|
| [ncmdump](src/ToMP3/Assets/ncmdump/README.md) | Decrypts `.ncm` | MIT | Copyright notice + full license text (end of `Assets/ncmdump/README.md`) |
| [um](src/ToMP3/Assets/um/README.md) | Decrypts the other vendors' private encrypted formats | MIT | Copyright notice + full license text (end of `Assets/um/README.md`) |
| FFmpeg / ffprobe 8.0.3 | Transcoding after decryption | LGPL-2.1 | `Assets/ffmpeg/FFmpeg-LICENSE.txt` + `NOTICE.txt` (with SHA-256) |
| LAME 3.100 | MP3 encoding | LGPL | `Assets/ffmpeg/LAME-LICENSE.txt` |
| TagLib 2.3 (ncmdump dependency) | Audio metadata | LGPL-2.1 / MPL | `Assets/ncmdump/TagLib-LICENSE-*.txt`, `TagLib-AUTHORS.txt` |
| zlib 1.3.1 | PNG cover decoding | zlib | Source and checksums recorded in `Assets/ffmpeg/NOTICE.txt` |

The corresponding source tarballs and `build.sh` are shipped as well (`Assets/<tool>/source/`), satisfying the LGPL rebuild requirement. This project calls the above tools as separate subprocesses and is not linked against them.
