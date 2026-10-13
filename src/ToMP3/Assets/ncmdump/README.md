# ncmdump（vendor 进来的可执行文件）

网易云音乐 `.ncm` → mp3 / flac 的解密工具。**直接放二进制，不改、不重写解密逻辑**：
界面把它当子进程调，`src/ToMP3/FormatConvert.cs` 里那一行
`Ncmdump` 就是它。

- 来源：https://github.com/taurusxin/ncmdump （C++ 版；源码复刻自已删库的 anonymous5l/ncmdump）
- 版本：`ncmdump -v` → `ncmdump version 1.5.1`
- 平台：macOS arm64；可执行文件旁必须保留 `libtag.2.dylib`（TagLib 2.3），通过 `@loader_path` 加载，不再依赖 Homebrew。
- 许可：MIT（全文见文末）
- TagLib 来源：https://taglib.org/；对应完整源码 `source/taglib-2.3.tar.gz` 随包提供，SHA-256 为 `7349f6fd942418bc7009ebe743eb7c9d055f02921ec56fa436ec25007c47fd38`。
- TagLib 按 LGPL-2.1 分发，附 `TagLib-LICENSE-LGPL.txt`、`TagLib-LICENSE-MPL.txt` 和 `TagLib-AUTHORS.txt`。库来自 Homebrew 2.3 构建，仅修改 Mach-O install name 并重新做 ad-hoc 签名，允许替换为 ABI 兼容的修改版。
- 更新后用 `otool -L` 检查 ncmdump 与 libtag，不允许出现 `/opt/homebrew` 或其他开发机绝对路径；修改加载路径后对两者执行 `codesign --force --sign -`。

## 换版本时注意

下载来的副本带 `com.apple.quarantine`，macOS 会直接把它 SIGKILL（退出码 137，**连 `-v` 都跑不起来**）。
放进本目录前要清掉，否则开发时跑不了、装到 /Applications 后也一样跑不起来：

```bash
xattr -c src/ToMP3/Assets/ncmdump/ncmdump  # 在仓库根目录执行
```

（`cp` 会连 xattr 一起抄，所以从别处拷过来之后都要清；已经装好的 .app 里那份同理。）

## 调用约定

```bash
ncmdump <文件> -o <输出目录>      # 多文件 / -d 目录 / -r 递归都支持，界面是按文件逐个调
```

**它任何情况都退 0**（文件不存在、不是 ncm 文件也只是打一行 `[Error]`/`[Exception]`），
只能解析日志那行 `[Done] '源' -> '产物'`；日志带 ANSI 颜色前缀，并且不保证在 stdout 还是 stderr。

## MIT License

```
MIT License

Copyright (c) [year] [fullname]

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
