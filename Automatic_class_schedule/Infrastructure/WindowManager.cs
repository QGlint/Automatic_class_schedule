using System.Runtime.InteropServices;

namespace Automatic_class_schedule.Infrastructure;

/// <summary>
/// 管理已打开窗口与项目路径的映射关系。
/// 当尝试打开已在某窗口中打开的项目时，将该窗口置于前台而非创建新窗口。
/// </summary>
public static class WindowManager
{
    private static readonly Dictionary<string, nint> _projectWindowMap = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();

    // ========== Win32 P/Invoke ==========

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(nint hWnd);

    // ========== 公共方法 ==========

    /// <summary>规范化项目路径用于比较</summary>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>注册窗口与项目的关联</summary>
    public static void RegisterProject(nint hwnd, string projectPath)
    {
        if (hwnd == 0 || string.IsNullOrEmpty(projectPath)) return;
        var key = NormalizePath(projectPath);
        lock (_lock)
        {
            // 先移除该窗口之前的注册（窗口可能切换了项目）
            RemoveByHwnd(hwnd);
            _projectWindowMap[key] = hwnd;
        }
    }

    /// <summary>注销窗口与项目的关联</summary>
    public static void UnregisterProject(nint hwnd)
    {
        if (hwnd == 0) return;
        lock (_lock)
        {
            RemoveByHwnd(hwnd);
        }
    }

    /// <summary>
    /// 尝试将已打开指定项目的窗口置于前台。
    /// 返回 true 表示找到了已打开的窗口并已激活；false 表示该项目未被任何窗口打开。
    /// </summary>
    public static bool TryBringToFront(string projectPath)
    {
        if (string.IsNullOrEmpty(projectPath)) return false;
        var key = NormalizePath(projectPath);

        nint hwnd;
        lock (_lock)
        {
            if (!_projectWindowMap.TryGetValue(key, out hwnd))
                return false;
        }

        // 验证窗口是否仍然有效
        if (!IsWindowValid(hwnd))
        {
            lock (_lock) { _projectWindowMap.Remove(key); }
            return false;
        }

        ActivateWindow(hwnd);
        return true;
    }

    // ========== 私有方法 ==========

    private static void RemoveByHwnd(nint hwnd)
    {
        var keysToRemove = _projectWindowMap
            .Where(kvp => kvp.Value == hwnd)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in keysToRemove)
            _projectWindowMap.Remove(key);
    }

    private static bool IsWindowValid(nint hwnd)
    {
        // GetWindowThreadProcessId 返回 0 表示窗口无效
        return GetWindowThreadProcessId(hwnd, out _) != 0;
    }

    /// <summary>使用 Win32 API 将窗口激活并置于前台</summary>
    private static void ActivateWindow(nint hwnd)
    {
        // 如果窗口最小化，先恢复
        if (IsIconic(hwnd))
            ShowWindow(hwnd, SW_RESTORE);
        else
            ShowWindow(hwnd, SW_SHOW);

        // 通过 AttachThreadInput 绕过前台窗口锁定限制
        uint foregroundThreadId = GetWindowThreadProcessId(hwnd, out _);
        uint currentThreadId = GetCurrentThreadId();

        if (foregroundThreadId != currentThreadId)
        {
            AttachThreadInput(currentThreadId, foregroundThreadId, true);
            SetForegroundWindow(hwnd);
            BringWindowToTop(hwnd);
            AttachThreadInput(currentThreadId, foregroundThreadId, false);
        }
        else
        {
            SetForegroundWindow(hwnd);
            BringWindowToTop(hwnd);
        }
    }
}
