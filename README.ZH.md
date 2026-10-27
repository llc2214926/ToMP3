# ToMP3

[English](README.md) | 中文

把各音乐平台的加密音频、常见音频及视频中的音轨**真正转成 MP3** 的桌面小工具，支持 macOS（Apple Silicon）。

> **⚠ 仅支持 macOS**：Windows 构建不在本项目的支持范围内，本文也不做描述。

不是改扩展名：解密得到 FLAC、OGG、M4A 等格式时，会用内置 FFmpeg（libmp3lame，`-q:a 2`）实际转码；本身就是 MP3 的不重复编码。转码有损，应用不会宣称保留原始音质。

## 支持的格式

| 来源 | 格式 |
|---|---|
| 网易云音乐 | `.ncm` |
| QQ 音乐 | `.qmc0/2/3/4/6/8` `.qmcflac` `.qmcogg` `.mflac*` `.mgg*` `.tkm` `.tm0/2/3/6` |
| 酷狗音乐 | `.kgm` `.kgma` `.kgm.flac` `.kgg` `.vpr` `.vpr.flac` |
| 酷我音乐 | `.kwm` |
| 虾米音乐 | `.xm` |
| 喜马拉雅 | `.x2m` `.x3m` |
| 百度音乐 | `.bkc*` |
| 咪咕音乐 | `.mmp4` |
| 普通音频 | `.flac` `.ogg` `.oga` `.opus` `.m4a` `.aac` `.wav` `.aiff` `.wma` `.ape` |
| 视频 | `.mp4` `.mkv` `.mov` `.avi` `.webm` `.flv` `.wmv` `.asf` `.m4v` `.mpg` `.mpeg` `.ts` `.mts` `.m2ts` `.3gp` `.3g2` `.vob` `.ogv` `.rm` `.rmvb` |

解密器与 FFmpeg 均随应用内置（`ncmdump`、`um`、FFmpeg 8.0.3 + 静态链接 LAME 3.100），无需安装任何依赖。

## 使用

1. 把文件或整个文件夹拖进窗口，也可以从原生菜单选择。
2. 选择输出目录——保留源文件，同名输出会覆盖。
3. 转换串行执行，可随时取消；失败或取消时旧的目标文件不会被破坏。
4. 完成、失败、跳过等状态在清单中按行显示，可在 Finder 中定位产物。

顶部始终显示全部支持格式，分类名称以主色蓝区分，扩展名保持灰色；输出位置框在失焦、聚焦和悬停时均保持激活色边框。

视频与音频使用同一窗口、导入入口和任务清单。视频提取第一条音轨（不是按默认音轨标记选择），保留已有标签及附带封面，不保留视频画面；MP3 音轨直接复制，其他音轨转码为 MP3。没有音轨的文件会标记失败并说明原因，源视频不变。

## 已知限制

- 新版 QQ 音乐（macOS 客户端 19.57+）下载的 `.mflac`/`.mgg` 密钥不在文件里，需要联网并用本机 QQ 音乐客户端登录过下载这些歌的账号后才能解密。
- 没有登录态、凭据过期或账号无权限时，会给出中文解决途径而不是原始英文报错。
- `.mp3` 输入不认领：已是 MP3 不重复编码。
- Linux 与移动端不在支持范围。

## 系统要求

- macOS 12+（Apple Silicon）

## 构建

需要 .NET 8 SDK；详细约定见 [AGENTS.md](AGENTS.md)。

```bash
# 构建
dotnet build src/ToMP3/ToMP3.csproj -c Release

# 启动开发界面
dotnet run --project src/ToMP3/ToMP3.csproj

# 发布 macOS arm64 自包含 .app
dotnet publish src/ToMP3/ToMP3.csproj -c Release -r osx-arm64 --self-contained true -o dist/ToMP3.app/Contents/MacOS/
codesign --force --deep --sign - dist/ToMP3.app
```

图标源文件为 `src/ToMP3/Assets/icon.svg`。导出 PNG、ICNS 和 ICO 时必须保留圆角外的透明区域，不能铺白底；图标更新时同步这些资源及 `Info.plist` 中的构建号。

## 许可

ToMP3 以 [MIT](LICENSE) 发布。

### 内置第三方工具

| 工具 | 用途 | 许可 | 随包内容 |
|---|---|---|---|
| [ncmdump](src/ToMP3/Assets/ncmdump/README.md) | 解密 `.ncm` | MIT | 版权声明 + 许可全文（`Assets/ncmdump/README.md` 文末） |
| [um](src/ToMP3/Assets/um/README.md) | 解密其余各家私有加密格式 | MIT | 版权声明 + 许可全文（`Assets/um/README.md` 文末） |
| FFmpeg / ffprobe 8.0.3 | 音频转码、视频音轨提取 | LGPL-2.1 | `Assets/ffmpeg/FFmpeg-LICENSE.txt` + `NOTICE.txt`（含 SHA-256） |
| LAME 3.100 | MP3 编码 | LGPL | `Assets/ffmpeg/LAME-LICENSE.txt` |
| TagLib 2.3（ncmdump 依赖） | 音频元数据 | LGPL-2.1 / MPL | `Assets/ncmdump/TagLib-LICENSE-*.txt`、`TagLib-AUTHORS.txt` |
| zlib 1.3.1 | PNG 封面解码 | zlib | `Assets/ffmpeg/NOTICE.txt` 记录来源与校验值 |

对应源码 tarball 与 `build.sh` 随包提供（`Assets/<工具名>/source/`），满足 LGPL 重新构建要求。本项目以独立子进程调用以上工具，不与它们产生链接关系。
