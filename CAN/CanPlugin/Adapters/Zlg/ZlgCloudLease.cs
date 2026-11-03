using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CAN.Adapters.Zlg;

/// <summary>The SDK cloud login is process-global, unlike CAN device handles.</summary>
internal sealed class ZlgCloudLease(ZlgIo io)
{
    private sealed class Login(ZlgIo driver, string identity)
    {
        public readonly ZlgIo Driver = driver;
        public readonly string Identity = identity;
        public int References = 1;
        public bool Connected, NativeConnectStarted;
    }
    private static readonly object Gate = new();
    private static Login? _current;
    private Login? _owned;

    public void Acquire(ZlgConnectionOptions options)
    {
        string identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[]
        {
            options.CloudUserName, options.CloudPassword, options.CloudHttpServer, options.CloudHttpPort.ToString(),
            options.CloudAuthServer, options.CloudAuthPort.ToString()
        }))));
        lock (Gate)
        {
            if (_current is not null)
            {
                if (!_current.Connected || _current.Identity != identity)
                    throw new InvalidOperationException("ZLG Cloud 已有其他身份或尚未清理的登录；请先关闭其连接");
                if (_current.Driver.CloudIsConnected() != 1)
                    throw new InvalidOperationException("ZLG Cloud 现有登录已断开，请先关闭旧连接后重新登录");
                _owned = _current; _owned.References++;
                return;
            }
            if (io.CloudIsConnected() != 0)
                throw new InvalidOperationException("ZLG Cloud 已由其他调用方登录；插件不能接管或登出该登录");
            // Reserve before calling the SDK: even a failed/throwing login can have acquired native state.
            _owned = _current = new Login(io, identity);
            io.CloudSetServerInfo(options.CloudHttpServer, (ushort)options.CloudHttpPort, options.CloudAuthServer, (ushort)options.CloudAuthPort);
            _owned.NativeConnectStarted = true;
            uint result;
            try { result = io.CloudConnect(options.CloudUserName, options.CloudPassword); }
            catch (Exception ex) { throw new InvalidOperationException($"ZLG Cloud ConnectServer 调用异常（{ex.GetType().Name}），凭据不记录到日志"); }
            if (result != 0) throw new InvalidOperationException($"ZLG Cloud 登录失败，SDK 状态={result}（凭据不记录到日志）");
            if (io.CloudIsConnected() != 1) throw new InvalidOperationException("ZLG Cloud 登录返回成功但连接状态尚未成立");
            _owned.Connected = true;
        }
    }

    public void ValidateChannel(uint device, uint channel, bool fd)
    {
        lock (Gate)
        {
            if (_owned is null || !_owned.Connected) throw new InvalidOperationException("ZLG Cloud 登录尚未建立");
            // GetUserData(update=1) returns shared SDK-owned storage; update and copy stay serialized.
            _owned.Driver.ValidateCloudChannel(device, channel, fd);
        }
    }

    public void Release()
    {
        lock (Gate)
        {
            if (_owned is null) return;
            if (!ReferenceEquals(_current, _owned)) throw new InvalidOperationException("ZLG Cloud 登录归属不一致，保留资源等待检查");
            if (_owned.References > 1) { _owned.References--; _owned = null; return; }
            _owned.Connected = false; // Retiring: no new connection may join a failed/in-progress logout.
            // Zero means success for ZCLOUD, whereas ZCAN uses STATUS_OK == 1.
            if (_owned.NativeConnectStarted)
            {
                if (_owned.Driver.CloudDisconnect() != 0)
                    throw new InvalidOperationException("ZLG Cloud 登出失败，保留登录租约，可重试 CAN Close");
                _owned.NativeConnectStarted = false;
            }
            if (_owned.Driver.CloudIsConnected() != 0)
                throw new InvalidOperationException("ZLG Cloud 登出后仍连接，保留登录租约，可重试 CAN Close");
            _current = null; _owned = null;
        }
    }
}
