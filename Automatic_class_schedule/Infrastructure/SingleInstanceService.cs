using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace Automatic_class_schedule.Infrastructure;

/// <summary>
/// 跨进程单实例服务。
/// 当第二个实例启动时，将项目路径通过命名管道发送给第一个实例，
/// 第一个实例收到后激活对应窗口（或打开新项目），第二个实例退出。
/// </summary>
public static class SingleInstanceService
{
    private const string MutexName = "ACS_SingleInstance_Mutex";
    private const string PipeName = "ACS_SingleInstance_Pipe";

    private static Mutex? _mutex;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hWnd);

    private const int SW_RESTORE = 9;

    /// <summary>
    /// 尝试获取单实例锁。
    /// 返回 true 表示当前是第一个实例（应继续运行）；
    /// 返回 false 表示已有实例在运行（当前实例应退出）。
    /// </summary>
    public static bool TryAcquireLock()
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        return createdNew;
    }

    /// <summary>
    /// 将项目路径发送给已运行的第一个实例。
    /// 返回 true 表示发送成功。
    /// </summary>
    public static bool SendToExistingInstance(string? projectPath)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(3000); // 最多等待 3 秒
            using var writer = new StreamWriter(client);
            writer.WriteLine(projectPath ?? "");
            writer.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 启动命名管道服务端，持续监听来自新实例的消息。
    /// 收到项目路径后在 UI 线程上处理（激活窗口或打开项目）。
    /// 必须在 UI 线程上调用。
    /// </summary>
    public static void StartListening(Action<string?> onProjectReceived)
    {
        // 在 UI 线程上捕获 Dispatcher
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync();

                    using var reader = new StreamReader(server);
                    var projectPath = await reader.ReadLineAsync();
                    var path = string.IsNullOrEmpty(projectPath) ? null : projectPath;

                    // 在 UI 线程上处理
                    if (dispatcher != null)
                    {
                        dispatcher.TryEnqueue(() => onProjectReceived(path));
                    }
                    else
                    {
                        onProjectReceived(path);
                    }
                }
                catch
                {
                    // 管道异常时短暂等待后重试
                    await Task.Delay(100);
                }
            }
        });
    }

    /// <summary>释放单实例锁</summary>
    public static void ReleaseLock()
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;
    }
}
