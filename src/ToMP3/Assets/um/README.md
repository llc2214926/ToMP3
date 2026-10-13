# um（vendor 进来的可执行文件）

[Unlock Music](https://git.unlock-music.dev/um/cli) 的 CLI 版。**直接放二进制，不改、不重写解密逻辑**：
界面把它当子进程调，`src/ToMP3/FormatConvert.cs` 里的 `Um` 就是它。
覆盖国内各家私有加密格式（除 ncm 外的全部，见下）——ncm 仍走隔壁的 `ncmdump`（它会把封面/标签写进产物）。

- 来源：https://git.unlock-music.dev/um/cli （tag `v0.2.12`，module 路径 `unlock-music.dev/cli`）
- 版本：`um -v` → `Unlock Music CLI version v0.2.12`
- 平台：macOS arm64 单文件（Mach-O thin arm64，13 MB）
- 许可：MIT（全文见文末）

## 支持的格式

`um --supported-ext` 列全部。界面认领的是其中的**加密**格式（纯 mp3/flac/wav/ogg/m4a/aac/wma
是 um 的 noop 解码器，默认关掉，不认领——那些文件转不出新东西）：

| 平台 | 扩展名 |
|---|---|
| QQ 音乐 | qmc0 qmc2 qmc3 qmc4 qmc6 qmc8 qmcflac qmcogg mflac mflac0 mflac1 mflaca mflach mflacl mflacm mgg mgg0 mgg1 mgga mggh mggl mggm tkm tm0 tm2 tm3 tm6 |
| 酷狗音乐 | kgm kgma kgm.flac kgg vpr vpr.flac |
| 酷我音乐 | kwm |
| 虾米音乐 | xm |
| 喜马拉雅 | x2m x3m |
| 百度音乐 | bkcmp3 bkcflac bkcogg bkcwav bkcwma bkcm4a bkcape |
| 咪咕音乐 | mmp4 |

**新版 QQ 音乐的 `mflach` / 部分 `mflac`（QM 加密）需要 ekey**，要从 QQ 音乐客户端的 MMKV
数据库里取，用 `--qmc-mmkv` / `--qmc-mmkv-key` 传。界面没暴露这两个（库里没有就别转了，
um 会如实报「no any decoder can resolve the file」）。酷狗的 `kgg`（v11 PC 端）同理要 `--kgg-db`。

## 这个二进制是怎么来的

在 macOS arm64 + Go 上，从**官方 module 源**（`proxy.golang.org`，即 `git.unlock-music.dev/um/cli`
的 tag 镜像）编译，不用第三方转载的成品。在仓库的 `artifacts/um-build/` 目录运行：

```bash
curl -sSL -o um.zip "https://proxy.golang.org/unlock-music.dev/cli/@v/v0.2.12.zip"
unzip -q um.zip && cp -R "unlock-music.dev/cli@v0.2.12" umsrc && chmod -R u+w umsrc
cd umsrc
CGO_ENABLED=0 GOOS=darwin GOARCH=arm64 \
  go build -trimpath -ldflags="-s -w -X main.AppVersion=v0.2.12" -o ../um ./cmd/um
```

换版本同理，改 `@v/<版本>` 和 `-X main.AppVersion=`，然后重跑
本应用的临时冒烟（真文件走一遍，见下）。

## 换版本时注意

下载来的副本带 `com.apple.quarantine`，macOS 会直接把它 SIGKILL（退出码 137，**连 `-v` 都跑不起来**）。
放进本目录前要清掉，否则开发时跑不了、装到 /Applications 后也一样跑不起来：

```bash
xattr -c src/ToMP3/Assets/um/um  # 在仓库根目录执行
```

（`cp` 会连 xattr 一起抄，所以从别处拷过来之后都要清；已经装好的 .app 里那份同理。）

## 调用约定

```bash
um --overwrite -o <输出目录> <文件>      # 一个文件一次，界面串行跑
```

- `--output` 必填；单个文件输入时产物直接落在输出目录，文件名 = 原名去掉加密后缀 + 真实扩展名
  （um 按内容嗅探 mp3/flac/ogg/m4a，不是照抄输入后缀）。
- **退出码可信**：成功 0，失败 1（不像 ncmdump 任何情况都退 0）。
- 不给 `--overwrite` 时，产物已存在就**跳过并退 0**（只打一行 `output file already exist, skip`），
  会跟「成功」混起来——所以固定传 `--overwrite`，覆盖与否由界面自己记。
- 日志走 **stderr**，格式是 zap 的 `<RFC3339 时间>\t<级别>\t<消息>\t<JSON 字段>`；级别带 ANSI 色码，
  解析前先剥。成功那行消息是 `successfully converted`，JSON 里的 `destination` 就是真实产物路径。
  失败最后一行是 `FATAL run app failed {"error": "…"}`。
- `--update-metadata`（写标签/封面）**要本机有 ffmpeg**，没有就直接失败，所以界面不传；代价是
  um 转出来的文件不带封面与标签（ncm 因此仍走 ncmdump）。

## MIT License

```
MIT License

Copyright (c) 2020-2021 Unlock Music

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
