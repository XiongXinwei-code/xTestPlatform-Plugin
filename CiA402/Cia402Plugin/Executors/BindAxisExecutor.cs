using CAN.Adapters;
using CAN.CANopen;
using CAN.Helpers;
using CiA402.Core;
using CiA402.Models;
using CiA402.Transport;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class BindAxisExecutor : ExecutorBase<BindAxisSetting>
{
    protected override async Task RunAsync(BindAxisSetting setting, EvaluatedSettings values, IExecutionContext context, CancellationToken ct, CancellationToken runToken)
    {
        var name = values.Text(nameof(setting.AxisName));
        var connection = values.Text(nameof(setting.ConnectionName));
        var key = AxisSession.Key(name);
        if (context.Resources.Contains(key)) throw new InvalidOperationException($"轴会话 {name} 已存在，请先解除绑定");
        ICia402AxisTransport? transport = null;
        AxisSession? session = null;
        try
        {
            if (setting.Transport == TransportKind.CANopen)
            {
                if (!context.Resources.TryGet<ICanAdapter>(CanHelper.GetAdapterKey(connection), out var adapter))
                    throw new InvalidOperationException($"CAN 连接 {connection} 不存在，请先执行 CAN_Open");
                var node = CanopenConnectionAccess.For(adapter).ReserveNode((byte)values.Number(nameof(setting.NodeId)), setting.CommunicationTimeoutMs);
                var canopen = new CanopenAxisTransport(node, setting);
                transport = canopen;
                await canopen.InitializeAsync(ct).ConfigureAwait(false);
            }
            else
            {
                if (!context.Resources.TryGet<ICia402EtherCatFactory>(ICia402EtherCatFactory.ResourceKey, out var factory))
                    throw new InvalidOperationException("未配置 EtherCAT 主站适配器；当前仅提供适配接口，不能执行 EtherCAT 运动");
                transport = await factory.OpenAsync(new(connection, (int)values.Number(nameof(setting.SlaveAddress)), setting), ct).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("EtherCAT 主站未返回轴通信实例");
            }
            var controller = new AxisController(transport, setting);
            await controller.InitializeAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            session = new AxisSession(controller, setting, runToken,
                (message, ex) => context.Log(LogLevel.Warn, $"轴 {name}：{message}", ex?.ToString()));
            if (!context.Resources.TryAdd(key, session)) throw new InvalidOperationException($"轴会话 {name} 已存在");
            if (!context.Resources.TryGet<AxisSession>(key, out var registered) || !ReferenceEquals(registered, session))
                throw new InvalidOperationException("执行环境未提供有效的资源注册表，无法保存轴会话");
        }
        catch
        {
            if (session != null) session.Dispose();
            else transport?.Dispose();
            throw;
        }
    }
}
