using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace ToMP3;

/// <summary>选输入文件/文件夹。
/// macOS 用原生 NSOpenPanel：Avalonia 11 的 StorageProvider 把文件和文件夹选择分开，
/// 而 NSOpenPanel 能在一次选择里同时接收两者。Windows 的系统对话框（IFileOpenDialog）
/// 在 FOS_PICKFOLDERS 与文件模式之间只能二选一，所以那边拆成两个入口。
/// 取消一律返回空数组，不改变原清单。</summary>
internal static class SourcePicker
{
    /// <summary>一次选择文件或文件夹（仅 macOS）。Windows 上走 <see cref="PickFilesAsync"/> /
    /// <see cref="PickFoldersAsync"/>。</summary>
    public static string[] Pick()
    {
        Dispatcher.UIThread.VerifyAccess();
        var pool = Send(Send(GetClass("NSAutoreleasePool"), Sel("alloc")), Sel("init"));
        try
        {
            var panel = Send(GetClass("NSOpenPanel"), Sel("openPanel"));
            Set(panel, Sel("setCanChooseFiles:"), 1);
            Set(panel, Sel("setCanChooseDirectories:"), 1);
            Set(panel, Sel("setAllowsMultipleSelection:"), 1);
            Set(panel, Sel("setCanCreateDirectories:"), 0);
            Set(panel, Sel("setTreatsFilePackagesAsDirectories:"), 0);
            Set(panel, Sel("setTitle:"), Text("选择文件夹/文件"));
            Set(panel, Sel("setPrompt:"), Text("选择"));
            Set(panel, Sel("setMessage:"), Text("可同时选择音频、视频文件和文件夹；按住 Command 多选，文件夹会递归查找支持的格式。"));
            if (Send(panel, Sel("runModal")) != 1) return [];

            var urls = Send(panel, Sel("URLs"));
            var paths = new string[(int)Send(urls, Sel("count"))];
            for (int i = 0; i < paths.Length; i++)
            {
                var url = At(urls, Sel("objectAtIndex:"), i);
                var path = Send(url, Sel("path"));
                paths[i] = Marshal.PtrToStringUTF8(Send(path, Sel("UTF8String")))!;
            }
            return paths;
        }
        finally { Set(pool, Sel("drain")); }
    }

    /// <summary>多选文件（Windows）。不加扩展名过滤：单独加入不支持的文件应在清单里
    /// 显示「跳过」，而不是选不出来。</summary>
    public static async Task<string[]> PickFilesAsync(Window owner)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        { Title = "选择音频或视频文件", AllowMultiple = true });
        return Paths(files);
    }

    /// <summary>多选文件夹（Windows）；调用方负责递归展开。</summary>
    public static async Task<string[]> PickFoldersAsync(Window owner)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        { Title = "选择文件夹", AllowMultiple = true });
        return Paths(folders);
    }

    static string[] Paths(IReadOnlyList<IStorageItem> items) =>
        items.Select(i => i.TryGetLocalPath()).Where(p => p is not null).Select(p => p!).ToArray();

    static nint Text(string text) => String(GetClass("NSString"), Sel("stringWithUTF8String:"), text);
    const string Library = "/usr/lib/libobjc.A.dylib";
    [DllImport(Library, EntryPoint = "objc_getClass")] static extern nint GetClass(string name);
    [DllImport(Library, EntryPoint = "sel_registerName")] static extern nint Sel(string name);
    [DllImport(Library, EntryPoint = "objc_msgSend")] static extern nint Send(nint obj, nint selector);
    [DllImport(Library, EntryPoint = "objc_msgSend")] static extern nint At(nint obj, nint selector, nint index);
    [DllImport(Library, EntryPoint = "objc_msgSend")] static extern void Set(nint obj, nint selector, nint value);
    [DllImport(Library, EntryPoint = "objc_msgSend")] static extern void Set(nint obj, nint selector);
    [DllImport(Library, EntryPoint = "objc_msgSend")] static extern nint String(nint obj, nint selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
}
