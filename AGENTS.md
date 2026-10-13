# AGENTS.md

本文件约束当前仓库中的 AI 编码协作。修改前先读相关实现，不凭项目名称推断功能。

## 沟通与改动原则

- 除代码、命令和被处理的原文外，使用中文。
- 先明确目标和验收方式；多步骤任务给出简短计划。有实质歧义且仓库无法解答时再提问。
- 只修改需求涉及的内容，保持现有风格；不顺手重构、不增加预设性功能。
- 优先复用现有实现、标准库和已安装依赖。当前采用 Avalonia 窗口代码后置，不为小功能引入 MVVM 框架、依赖注入或通用插件层。
- 用户已有改动必须保留；修复问题前追踪调用路径，在共用根因处修复。
- 不修改系统代理、VPN 或网络代理配置，除非用户明确要求。

## 目录约定

- `src/ToMP3/`：应用源码、项目配置和随包资源；下表中的应用文件路径均相对此目录。
- `tests/ToMP3.Smoke/`：本地可运行的界面与真实音频转换检查，按项目约定忽略，不提交版本控制；新克隆不包含此目录。
- `dist/`：发布的 `.app` 和分发压缩包，不提交版本控制。
- `artifacts/`：临时构建、样例与验证产物，不提交版本控制，不在仓库根目录按日期建文件夹。
- 各项目的 `bin/`、`obj/` 使用 SDK 默认位置并忽略；不要将临时 C# 程序放进应用源码目录。

## 项目事实与入口

- 技术栈：C#、.NET 8、Avalonia 11.2.3，命名空间 `ToMP3`，启用可空引用类型。
- 当前实际运行目标为 macOS Apple Silicon：原生文件选择器依赖 AppKit/Objective-C，内置转换工具为 macOS arm64。不能因 Avalonia 支持跨平台就宣称本项目已支持 Windows/Linux。
- 产品要求：成功转换后的最终输出必须是实际 MP3 音频，不能仅把扩展名改为 `.mp3`。解密得到 FLAC、OGG、M4A 等格式时，必须继续转码为 MP3；本身已是 MP3 的无需重复编码。
- 关于窗口和其他用户可见文案必须与 MP3 输出行为一致：不能宣称保留所有输入的原始音质，应说明非 MP3 转码有损，以及已内置转换工具。
- 按钮样式统一在 `App.axaml`：常规按钮最小高度 40、紧凑按钮 32，内容水平和垂直居中；主操作蓝底、次操作浅底、停止操作浅红底。保留悬停、按下、禁用反馈与可见的圆角键盘焦点环，窗口内不重复覆盖公共按钮样式。
- 转换流程：在独立临时目录解密，用 ffprobe 识别编码；MP3 音轨直接复制，其他编码通过 FFmpeg 的 libmp3lame（质量参数 `-q:a 2`）转为 MP3。实际解码校验通过后才替换目标文件，失败或取消保留旧目标；临时目录移入废纸篓。
- 输入分两类：已注册的加密格式走 `ncmdump`/`um` 解密后再转码；`flac`、`ogg`、`m4a` 等普通音频只转码不解密。`.mp3` 不认领——已是 MP3 不该重复编码，输出目录等于源目录时会覆盖用户原文件。
- 新版 QQ 音乐（macOS 客户端 19.57+）下载的 `.mflac`/`.mgg` 文件尾是 `musicex`，密钥（ekey）不在文件里：先原样让 `um` 解一遍（老格式密钥就在文件内），失败再用 `QqEkey.cs` 联网换密钥。这条路要联网，并且要本机 QQ 音乐客户端登录过下载这些歌的账号；客户端不需要正在运行。

| 文件 | 职责 |
| --- | --- |
| `Program.cs` | Avalonia 启动入口 |
| `App.axaml` / `App.axaml.cs` | 全局样式、原生应用菜单、窗口生命周期、异常日志 |
| `AudioConvertWindow.axaml` / `AudioConvertWindow.axaml.cs` | 主界面、任务清单、拖放、串行转换、取消、访达定位 |
| `FormatConvert.cs` | 格式注册、后缀匹配、子进程调用、日志解析及真实产物路径 |
| `QqEkey.cs` | 解析 `musicex` 文件尾、读客户端登录态、换 ekey、写出带旧版密钥尾的临时副本 |
| `MacSourcePicker.cs` | 可同时多选文件和文件夹的原生 `NSOpenPanel` |
| `AboutWindow.axaml` / `AboutWindow.axaml.cs` | 关于窗口，从嵌入的 `Info.plist` 读取版本 |
| `ToMP3.csproj` / `Info.plist` | 依赖、资源复制、macOS bundle 配置与版本 |
| `Assets/ncmdump/` / `Assets/um/` | 内置转换器及其来源、版本、许可和调用说明 |
| `Assets/ffmpeg/` | 内置 ffmpeg/ffprobe、许可、对应源码和可复现构建脚本 |
| 仓库根目录下 `tests/ToMP3.Smoke/Program.cs` | 原生菜单、关于窗口和窄窗口布局冒烟检查 |

## 必须保持的行为

- 格式信息集中在 `FormatConvert.All` 与 `UmGroups`；界面支持列表和计数由其派生，不再维护第二份列表。
- 后缀匹配必须忽略大小写并支持 `.kgm.flac`、`.vpr.flac` 等双点后缀，不能替换成只看 `Path.GetExtension`。
- `ConvertResult.Output` 必须指向已经验证的最终 MP3 文件，不能指向非 MP3 中间产物，也不能通过拼接 `.mp3` 猜测。只有最终 MP3 生成成功才能标记“完成”；转码失败或取消不得报告成功。
- `.ncm` 使用 `ncmdump` 解密，其他已注册加密格式使用 `um` 解密；不在应用内重写解密算法。解密后的非 MP3 音频还需经过转码阶段。
- 子进程使用 `ProcessStartInfo.ArgumentList`，不拼 shell 命令；同时读取 stdout/stderr，去除 ANSI 色码，取消时终止子进程树。
- `ncmdump` 的退出码不能证明解密成功，必须解析 `[Done]` 并验证产物存在；`um` 必须解析 `successfully converted` 日志中的 `destination` 并验证产物存在。这些检查只证明解密阶段成功，不代表最终 MP3 转换完成。
- 当前 `um` 使用 `--overwrite`，覆盖已有产物时要如实提示；不得把“跳过已有文件”当作新转换成功。用样例副本验证，不覆盖用户原文件。
- 「说明」列只写用户要知道的事：失败原因、覆盖提示。不影响产物的工具警告（如 `ncmdump` 的 `[Warn]`）直接标“完成”，不写说明。
- 状态列按值上色：完成绿、失败红，其余用正文色。颜色来自 `Job.StatusBrush`，`Status` 变更要同时通知它；`DataGridTextColumn` 的 `Foreground` 是整列的，所以状态列用模板列。
- 单元格文字放不下时给省略号，完整内容挂悬停提示（`ToolTip.ShowDelay` 设 0，鼠标放上即显示），不硬裁切。提示经 `TipText` 转换器把空字符串变 null——`ToolTip` 只要 Tip 非 null 就会弹出空白小框；状态列放得下，本地 `ToolTip.Tip="{x:Null}"` 关掉重复提示。
- 普通音频只做转码，不计入“加密格式”说法；界面文案写“支持 N 种格式”。加密格式必须排在 `FormatConvert.All` 中普通音频之前，否则 `.kgm.flac`、`.vpr.flac` 会被 `.flac` 抢走。
- `musicex` 文件的 ekey 只能联网换：`QqEkey.cs` 按文件尾的歌曲 mid 和资源文件名调 `music.vkey.GetEVkey`（`filename` 必须带加密后缀、`songtype` 必须是 1），把返回的 ekey 写成临时副本的旧版「裸密钥」尾部，解密仍由 `um` 完成，不在应用里实现 QMC 算法。副本只在临时目录里，用户原文件只读。
- 登录凭据取自 `~/Library/Preferences/com.tencent.QQMusicMac.plist` 的 `AutoLoginUserInfo`（NSKeyedArchiver），用系统 `plutil` 转换读取，只在内存里用：不落盘、不写日志、不进界面文案。
- 需要 ekey 或外部数据库的格式不能保证仅凭扩展名就能解密。没有登录态、凭据过期、账号没有权限时必须给中文解决途径（打开 QQ 音乐登录、重新登录刷新凭据），不照抄 `no any decoder can resolve the file` 这类英文日志。
- 不启用 `um --update-metadata`；解密与最终转码分开，FFmpeg 转码时复制已有标签和封面。调整工具版本或调用参数前先读对应的 `Assets/*/README.md`，保留来源及许可。
- 保持串行转换、重复路径去重、完成项默认不重跑、显式重新转换清空旧状态与产物路径的行为。
- 文件夹递归导入只收支持的格式并跳过隐藏文件名；单独加入不支持的文件应在清单中显示“跳过”，而不是静默丢弃。
- 原生选择器在 UI 线程调用；取消选择不改变清单。关于窗口重复打开复用现有实例，关闭后允许重新创建。
- 版本以 `Info.plist` 为单一来源，不在关于窗口硬编码另一份版本号。

## 构建、运行与发布

以下命令在仓库根目录执行，需要 .NET 8 SDK；运行界面和原生检查需要 macOS 桌面。

下方测试命令仅在本地保留 `tests/ToMP3.Smoke/` 时可用；新克隆需通过实际运行应用验证，不应把缺少本地测试目录当作构建失败。

发布包内置 FFmpeg 8.0.3、ffprobe 和静态链接的 LAME 3.100，并自包含 .NET 运行时。目标为 macOS 12+ Apple Silicon，用户无需安装 Homebrew、FFmpeg 或 .NET。运行时仅使用 `AppContext.BaseDirectory/Assets/ffmpeg/`，不从 PATH 或开发机路径回退；缺失文件应报告应用不完整。

Release 默认启用 `PublishTrimmed=true`、`TrimMode=partial`；界面使用编译绑定，表格列声明 `Job` 类型和 `SortMemberPath`。保留 `Avalonia.Base` 与 `Avalonia.Controls.DataGrid`，避免库内反射入口被裁掉；不屏蔽 Avalonia 11.2.3 的第三方裁剪警告。改动裁剪配置后必须用实际发布的程序集和运行时验证表格数据、状态更新、排序、拖放、原生选择器取消及关于窗口，不能只另行裁剪测试程序。

切换裁剪或工具配置时发布到全新目录，验证后替换 `dist/ToMP3.app`，避免覆盖发布残留旧程序集。FFmpeg 保留原有 211 个音频解码器、全部原有解封装器及 PNG/JPEG/BMP 封面支持，移除无关视频/字幕解码与视频滤镜；完整对应源码仍随包提供，不靠删除许可或源码缩小体积。

升级工具时运行 `bash src/ToMP3/Assets/ffmpeg/build.sh artifacts/ffmpeg-build`（仅构建机需要 Xcode 工具），保留 `NOTICE.txt`、许可和完整对应源码；用 `otool -L` 确认只依赖 macOS 系统库，再运行转换检查。不得直接复制 Homebrew 的动态链接二进制。

```bash
# 构建
 dotnet build src/ToMP3/ToMP3.csproj -c Release

# 启动开发界面
 dotnet run --project src/ToMP3/ToMP3.csproj

# 原生菜单、关于窗口和窄窗口布局；需要未锁定的 macOS 桌面
 dotnet run --project tests/ToMP3.Smoke/AboutSmoke.csproj

# 仅检查窄窗口布局，仍需要图形环境
 dotnet run --project tests/ToMP3.Smoke/AboutSmoke.csproj -- --layout-only

# 无需图形界面的真实音频转换回归
 dotnet run --project tests/ToMP3.Smoke/AboutSmoke.csproj -- --conversion-only artifacts/mp3-smoke

# 发布 macOS arm64 自包含应用；会更新现有 bundle，先确认这是本次任务范围
 dotnet publish src/ToMP3/ToMP3.csproj -c Release -r osx-arm64 --self-contained true -o dist/ToMP3.app/Contents/MacOS/
 codesign --force --deep --sign - dist/ToMP3.app
 ditto -c -k --sequesterRsrc --keepParent dist/ToMP3.app dist/ToMP3-macOS-arm64.zip

# 拖拽安装用的 dmg：暂存目录里只放 app 和 /Applications 软链，避免把 .DS_Store 之类打进去
 rm -rf artifacts/dmg && mkdir -p artifacts/dmg/ToMP3
 ditto dist/ToMP3.app artifacts/dmg/ToMP3/ToMP3.app && ln -s /Applications artifacts/dmg/ToMP3/Applications
 hdiutil create -volname ToMP3 -srcfolder artifacts/dmg/ToMP3 -fs HFS+ -format UDZO -imagekey zlib-level=9 -ov dist/ToMP3-macOS-arm64.dmg
```

- 应用与检查项目位于独立目录，验证程序通过项目引用使用真实应用，无需主项目排除测试源码。
- 发布时保持 `Assets/<工具名>/<工具名>` 相对可执行目录的位置。发布目标包含 `ToMP3.app` 时，项目会复制 `Info.plist` 和 `icon.icns` 到 bundle 对应位置。
- 不直接修改 `bin/`、`obj/` 或 `dist/` 内生成的文件来修复问题；修改源文件后重新构建或发布。

## 验证与交付

- 代码变更至少构建一次，并实际运行改动路径；编译成功不等于功能正确。纯文档变更核对引用与命令，不必无关地重打发布包。
- 界面改动检查真实窗口：最小尺寸、格式说明展开、按钮可见性，以及受影响的原生交互。菜单与布局冒烟不代表转换已验证；转换路径另运行 `--conversion-only`，使用现场生成的正弦音 NCM、KWM 容器校验两种工具的真实解密与 MP3 编码，并检查 MP3 无重复编码及目标文件保护。
- 无环境依赖发布：转换检查必须使用内置工具并排除 Homebrew PATH；发布后检查包内工具动态依赖，并把 app 复制到新路径验证，不能只在开发目录成功就宣称独立运行。
- 转换改动使用有权处理的真实样例副本和独立输出目录，检查成功产物、失败原因、取消及覆盖提示；没有样例时明确说明验证缺口，不宣称转换通过。
- `musicex` 取密钥这条路必须用真实 `.mflac`/`.mgg` 副本跑通到 MP3，并单独验证没有登录态时的中文指引（可用临时 `HOME` 指向空目录触发，不要动用户的 plist）。缺网络或客户端未登录时如实说明这部分未验证，不靠文件尾解析成功就宣称能解密。
- 缺陷先复现再修复；为有风险的分支保留最小可运行回归检查，不为字段转发、源码文字或偶然实现细节堆测试。
- 工具升级需验证二进制能实际启动，并跑真实转换；仅检查版本号不够。排查隔离属性前核实来源，不盲目移除安全属性。
- 行为或调用约定变化时同步相关文档。交付说明改了什么、实际执行的验证及未验证项，不编造结果。
