using System.Net;
using Ethernet.Models;
using Ethernet.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.Executors;

public sealed class UdpOpenExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var execution = PluginCommon.ExecutionEpoch.Capture(context.Resources, "Ethernet");
            void Check() { cancellationToken.ThrowIfCancellationRequested(); execution.ThrowIfExpired(); }
            var step = context.CurrentStep!.Step;
            var setting = (UdpOpenSetting)new UdpOpenPlugin().CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.LocalPort is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(setting.LocalPort), "本地端口必须为 0~65535");
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setting.TimeoutMs);
            EthernetStringOutput.Validate(context, setting.LocalEndpointVariable, Check);
            var name = await EthernetExecutorHelper.EvalStringAsync(setting.ConnectionName, context); Check();
            var address = await EthernetExecutorHelper.EvalStringAsync(setting.LocalAddress, context); Check();
            if (!IPAddress.TryParse(address, out var ip)) throw new ArgumentException("LocalAddress 必须求值为本机 IP 地址，例如 0.0.0.0 或 127.0.0.1");
            var socket = await UdpConnectionManager.OpenAsync(name!, new IPEndPoint(ip, setting.LocalPort), setting.EnableBroadcast,
                setting.TimeoutMs, cancellationToken, context.Resources, Check);
            void CheckPublication()
            {
                Check(); socket.CheckOpen();
                if (!ReferenceEquals(socket, UdpConnectionManager.Get(name!, context.Resources)))
                    throw new InvalidOperationException("UDP 连接代次已替换，拒绝发布旧绑定端点");
                Check();
            }
            CheckPublication();
            var endpoint = socket.LocalEndpoint.ToString();
            context.Log($"UDP 已绑定: {name}，{endpoint}；此操作不进行远端握手");
            CheckPublication();
            if (!string.IsNullOrWhiteSpace(setting.LocalEndpointVariable)) context.SetVariable(setting.LocalEndpointVariable, endpoint);
            CheckPublication();
            return ExecutionResults.Completed($"UDP 已绑定: {name} ({endpoint})");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } }; }
        catch (Exception ex) { return ExecutionResults.FromException(ex, $"UDP 打开失败: {ex.Message}"); }
    }
}
