using System.Runtime.CompilerServices;
using Ivi.Visa;

namespace VISA.Helpers;

/// <summary>
/// VISA 通用辅助方法
/// </summary>
public static class VisaHelper
{
    private static readonly ConditionalWeakTable<IMessageBasedSession, SemaphoreSlim> SessionLocks = new();

    /// <summary>获取会话对应的 I/O 互斥锁，用于串行化并行步骤对同一会话的访问（会话销毁后锁自动回收）</summary>
    public static SemaphoreSlim GetLock(IMessageBasedSession session) =>
        SessionLocks.GetValue(session, _ => new SemaphoreSlim(1, 1));

    /// <summary>根据连接名称生成运行时数据存储的唯一键</summary>
    public static string GetSessionKey(string connectionName) => $"__VISA_{connectionName}";

    /// <summary>根据连接名称生成终止符存储的唯一键</summary>
    public static string GetTerminatorKey(string connectionName) => $"__VISA_{connectionName}__TERM";

    /// <summary>
    /// 将用户配置的终止符归一化为真实字符（同时支持转义文本 \n、\r、\r\n 与真实字符），为空时默认换行符
    /// </summary>
    public static string NormalizeTerminator(string? terminator)
    {
        if (string.IsNullOrEmpty(terminator))
            return "\n";
        return terminator.Replace("\\r", "\r").Replace("\\n", "\n");
    }

    /// <summary>
    /// 打开 VISA 会话
    /// </summary>
    public static IMessageBasedSession OpenSession(string resourceString, int openTimeoutMs, int ioTimeoutMs, string terminator)
    {
        var visaSession = GlobalResourceManager.Open(resourceString, AccessModes.None, openTimeoutMs);
        if (visaSession is not IMessageBasedSession session)
        {
            visaSession.Dispose();
            throw new InvalidOperationException($"资源 {resourceString} 不是消息型 VISA 设备，无法进行 SCPI 通信");
        }

        session.TimeoutMilliseconds = ioTimeoutMs;

        // 读取终止符取归一化后的最后一个字符（如 \r\n 取 \n）
        var term = NormalizeTerminator(terminator);
        var termChar = term[^1];
        if (termChar > 0xFF)
        {
            session.Dispose();
            throw new InvalidOperationException("终止符必须是单字节字符（如 \\n、\\r\\n），不支持中文等多字节字符");
        }
        session.TerminationCharacter = (byte)termChar;
        session.TerminationCharacterEnabled = true;

        return session;
    }

    /// <summary>
    /// 发送 SCPI 命令（追加配置的终止符），并刷新写缓冲区确保数据实际发出（END/EOI 结尾）
    /// </summary>
    public static void Write(IMessageBasedSession session, string command, string terminator = "\n")
    {
        session.FormattedIO.Write(command + terminator);
        session.FormattedIO.FlushWrite(true);
    }

    /// <summary>
    /// 读取响应
    /// </summary>
    public static string Read(IMessageBasedSession session, bool trim)
    {
        var response = session.FormattedIO.ReadLine();
        return trim ? response.Trim() : response;
    }

    /// <summary>
    /// 发送命令并读取响应（Query）
    /// </summary>
    public static string Query(IMessageBasedSession session, string command, bool trim, string terminator = "\n")
    {
        Write(session, command, terminator);
        return Read(session, trim);
    }

    /// <summary>
    /// 在后台线程执行同步 VISA I/O，并施加软超时兜底。
    /// VISA 的同步 API 不响应 CancellationToken，若驱动层超时失效会导致步骤永久阻塞，
    /// 因此统一在插件层用 <see cref="Task.WaitAsync(TimeSpan, CancellationToken)"/> 限时，超时抛 <see cref="TimeoutException"/>。
    /// </summary>
    public static async Task<T> RunWithTimeoutAsync<T>(Func<T> action, int timeoutMs, string operation, CancellationToken cancellationToken)
    {
        var task = Task.Run(action, CancellationToken.None);
        try
        {
            return await task.WaitAsync(TimeSpan.FromMilliseconds(GetSoftTimeoutMs(timeoutMs)), cancellationToken);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"VISA {operation} 超时（{timeoutMs} ms），设备未在规定时间内响应");
        }
    }

    /// <summary>
    /// 在后台线程执行同步 VISA I/O（无返回值），并施加软超时兜底
    /// </summary>
    public static async Task RunWithTimeoutAsync(Action action, int timeoutMs, string operation, CancellationToken cancellationToken)
    {
        await RunWithTimeoutAsync(() => { action(); return true; }, timeoutMs, operation, cancellationToken);
    }

    /// <summary>获取会话的 I/O 超时（毫秒），读取失败时回退到默认值</summary>
    public static int GetIoTimeoutMs(IMessageBasedSession session, int fallbackMs = 5000)
    {
        try
        {
            var timeout = session.TimeoutMilliseconds;
            return timeout > 0 ? timeout : fallbackMs;
        }
        catch
        {
            return fallbackMs;
        }
    }

    /// <summary>软超时在驱动超时基础上留出余量，优先让 VISA 自身抛出更精确的超时异常</summary>
    private static int GetSoftTimeoutMs(int timeoutMs)
    {
        if (timeoutMs <= 0) return 5000;
        var soft = timeoutMs + 1000;
        return soft < timeoutMs ? int.MaxValue : soft;
    }
}
