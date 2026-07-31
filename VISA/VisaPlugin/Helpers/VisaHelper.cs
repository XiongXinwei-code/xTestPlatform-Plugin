using Ivi.Visa;

namespace VISA.Helpers;

/// <summary>
/// VISA 通用辅助方法
/// </summary>
public static class VisaHelper
{
    /// <summary>根据连接名称生成运行时数据存储的唯一键</summary>
    public static string GetSessionKey(string connectionName) => $"__VISA_{connectionName}";

    /// <summary>
    /// 打开 VISA 会话
    /// </summary>
    public static IMessageBasedSession OpenSession(string resourceString, int openTimeoutMs, int ioTimeoutMs, string terminator)
    {
        var session = (IMessageBasedSession)GlobalResourceManager.Open(resourceString, AccessModes.None, openTimeoutMs);
        session.TimeoutMilliseconds = ioTimeoutMs;

        if (!string.IsNullOrEmpty(terminator))
        {
            // 设置终止符
            var termChar = terminator switch
            {
                "\\n" => '\n',
                "\\r" => '\r',
                "\\r\\n" => '\n',
                _ => terminator.Length > 0 ? terminator[0] : '\n'
            };
            session.TerminationCharacter = (byte)termChar;
            session.TerminationCharacterEnabled = true;
        }

        return session;
    }

    /// <summary>
    /// 发送 SCPI 命令（自动追加终止符）
    /// </summary>
    public static void Write(IMessageBasedSession session, string command)
    {
        session.FormattedIO.WriteLine(command);
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
    public static string Query(IMessageBasedSession session, string command, bool trim)
    {
        Write(session, command);
        return Read(session, trim);
    }
}
