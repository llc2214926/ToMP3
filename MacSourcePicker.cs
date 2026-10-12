using System.Runtime.InteropServices;
using Avalonia.Threading;

namespace ToMP3;

/// <summary>macOS 原生混合选择窗口。Avalonia 11 的 StorageProvider 将文件和文件夹选择分开，
/// 这里使用 NSOpenPanel 在一次选择中同时接收两者；取消返回空数组，不改变原清单。</summary>
internal static class MacSourcePicker
{
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
            Set(panel, Sel("setMessage:"), Text("可同时选择音乐文件和文件夹；按住 Command 多选，文件夹会递归查找支持的格式。"));
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
