# AGENTS.md

本文件约束当前仓库中的 AI 编码协作。修改前先读相关实现，不凭项目名称推断功能。

## 沟通与改动原则

- 除代码、命令和被处理的原文外，使用中文。
- 先明确目标和验收方式；多步骤任务给出简短计划。有实质歧义且仓库无法解答时再提问。
- 只修改需求涉及的内容，保持现有风格；不顺手重构、不增加预设性功能。
- 优先复用现有实现、标准库和已安装依赖。当前采用 Avalonia 窗口代码后置，不为小功能引入 MVVM 框架、依赖注入或通用插件层。
- 用户已有改动必须保留；修复问题前追踪调用路径，在共用根因处修复。
- 不修改系统代理、VPN 或网络代理配置，除非用户明确要求。
- 删除文件或目录只能移动到废纸篓，同名时加时间戳；禁止使用 `rm`、`unlink` 等直接删除方式。

## 工作文件

- 开始工作前检查当天 `yyyy-MM-dd/` 目录，不存在则创建。临时脚本、样例副本、截图和验证产物放在其中。
- 继续某天的工作时，先把相关内容复制到当天目录，不直接修改历史工作目录。
- 检查最近一个早于当天的日期目录；若为空，移到废纸篓，不处理非日期目录。
- 正式源码、项目配置和本指令文件保留在项目约定位置；构建工具的 `bin/`、`obj/` 保留默认位置。
- SDK 默认递归收集 `.cs` 文件：不要在日期目录随手放可被主项目编译的临时 C# 程序；使用非 `.cs` 后缀保存草稿，或在执行前显式配置编译排除。

## 项目事实与入口

- 技术栈：C#、.NET 8、Avalonia 11.2.3，命名空间 `ToMP3`，启用可空引用类型。
- 当前实际运行目标为 macOS Apple Silicon：原生文件选择器依赖 AppKit/Objective-C，内置转换工具为 macOS arm64。不能因 Avalonia 支持跨平台就宣称本项目已支持 Windows/Linux。
- 当前功能是解密、解包加密音乐文件，不是通用音频转码器。产物可能是 MP3、FLAC、OGG、M4A；不得承诺所有输入都转为 MP3。

| 文件 | 职责 |
| --- | --- |
| `Program.cs` | Avalonia 启动入口 |
| `App.axaml` / `App.axaml.cs` | 全局样式、原生应用菜单、窗口生命周期、异常日志 |
| `AudioConvertWindow.axaml` / `AudioConvertWindow.axaml.cs` | 主界面、任务清单、拖放、串行转换、取消、访达定位 |
| `FormatConvert.cs` | 格式注册、后缀匹配、子进程调用、日志解析及真实产物路径 |
| `MacSourcePicker.cs` | 可同时多选文件和文件夹的原生 `NSOpenPanel` |
| `AboutWindow.axaml` / `AboutWindow.axaml.cs` | 关于窗口，从嵌入的 `Info.plist` 读取版本 |
| `ToMP3.csproj` / `Info.plist` | 依赖、资源复制、macOS bundle 配置与版本 |
| `Assets/ncmdump/` / `Assets/um/` | 内置转换器及其来源、版本、许可和调用说明 |
| `checks/Program.cs` | 原生菜单、关于窗口和窄窗口布局冒烟检查 |

## 必须保持的行为

- 格式信息集中在 `FormatConvert.All` 与 `UmGroups`；界面支持列表和计数由其派生，不再维护第二份列表。
- 后缀匹配必须忽略大小写并支持 `.kgm.flac`、`.vpr.flac` 等双点后缀，不能替换成只看 `Path.GetExtension`。
- `ConvertResult.Output` 必须来自转换器报告且已存在的真实产物，不能通过拼接 `.mp3` 猜测。
- `.ncm` 使用 `ncmdump`，其他已注册加密格式使用 `um`；不在应用内重写解密算法。
- 子进程使用 `ProcessStartInfo.ArgumentList`，不拼 shell 命令；同时读取 stdout/stderr，去除 ANSI 色码，取消时终止子进程树。
- `ncmdump` 的退出码不能证明成功，必须解析 `[Done]` 并验证产物存在；`um` 必须解析 `successfully converted` 日志中的 `destination` 并验证产物存在。
- 当前 `um` 使用 `--overwrite`，覆盖已有产物时要如实提示；不得把“跳过已有文件”当作新转换成功。用样例副本验证，不覆盖用户原文件。
- 普通 MP3/FLAC 等不作为已支持的加密输入；需要 ekey 或外部数据库的格式不能保证仅凭扩展名就能解密。
- 不擅自启用 `um --update-metadata`，它要求额外安装 ffmpeg。调整工具版本或调用参数前先读对应的 `Assets/*/README.md`，保留来源及许可。
- 保持串行转换、重复路径去重、完成项默认不重跑、显式重新转换清空旧状态与产物路径的行为。
- 文件夹递归导入只收支持的格式并跳过隐藏文件名；单独加入不支持的文件应在清单中显示“跳过”，而不是静默丢弃。
- 原生选择器在 UI 线程调用；取消选择不改变清单。关于窗口重复打开复用现有实例，关闭后允许重新创建。
- 版本以 `Info.plist` 为单一来源，不在关于窗口硬编码另一份版本号。

## 构建、运行与发布

以下命令在仓库根目录执行，需要 .NET 8 SDK；运行界面和原生检查需要 macOS 桌面。

```bash
# 构建
 dotnet build ToMP3.csproj -c Release

# 启动开发界面
 dotnet run --project ToMP3.csproj

# 原生菜单、关于窗口和窄窗口布局；需要未锁定的 macOS 桌面
 dotnet run --project checks/AboutSmoke.csproj

# 仅检查窄窗口布局，仍需要图形环境
 dotnet run --project checks/AboutSmoke.csproj -- --layout-only

# 发布 macOS arm64 自包含应用；会更新现有 bundle，先确认这是本次任务范围
 dotnet publish ToMP3.csproj -c Release -r osx-arm64 --self-contained true -o ToMP3.app/Contents/MacOS/
```

- 主项目已排除 `checks/**` 的 C# 编译项；验证程序通过项目引用使用真实应用。
- 发布时保持 `Assets/<工具名>/<工具名>` 相对可执行目录的位置。发布目标包含 `ToMP3.app` 时，项目会复制 `Info.plist` 和 `icon.icns` 到 bundle 对应位置。
- 不直接修改 `bin/`、`obj/` 或 `ToMP3.app/` 内生成的源码副本来修复问题；修改源文件后重新构建或发布。

## 验证与交付

- 代码变更至少构建一次，并实际运行改动路径；编译成功不等于功能正确。纯文档变更核对引用与命令，不必无关地重打发布包。
- 界面改动检查真实窗口：最小尺寸、格式说明展开、按钮可见性，以及受影响的原生交互。现有冒烟只覆盖菜单、关于窗口和部分布局，不代表转换流程已验证。
- 转换改动使用有权处理的真实样例副本和独立输出目录，检查成功产物、失败原因、取消及覆盖提示；没有样例时明确说明验证缺口，不宣称转换通过。
- 缺陷先复现再修复；为有风险的分支保留最小可运行回归检查，不为字段转发、源码文字或偶然实现细节堆测试。
- 工具升级需验证二进制能实际启动，并跑真实转换；仅检查版本号不够。排查隔离属性前核实来源，不盲目移除安全属性。
- 行为或调用约定变化时同步相关文档。交付说明改了什么、实际执行的验证及未验证项，不编造结果。
