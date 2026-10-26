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
    protected override async Task<Action?> RunAsync(BindAxisSetting setting, EvaluatedSettings values, IExecutionContext context, CancellationToken ct, CancellationToken callerToken, Action checkExecution)
    {
        var name = values.Text(nameof(setting.AxisName));
        var connection = values.Text(nameof(setting.ConnectionName));
        var key = AxisSession.Key(name);
        using var binding = await AxisBindingGate.EnterAsync(context, key, ct).ConfigureAwait(false);
        checkExecution();
        if (context.Resources.Contains(key)) throw new InvalidOperationException($"轴会话 {name} 已存在，请先解除绑定");
        ICia402AxisTransport? transport = null;
        AxisSession? session = null;
        Action checkBackend = checkExecution;
        var cleanupLog = new AxisDiagnosticLog((message, ex) => context.Log(LogLevel.Warn, $"轴 {name}：{message}", ex?.ToString()));
        try
        {
            if (setting.Transport == TransportKind.CANopen)
            {
                if (!context.Resources.TryGet<ICanAdapter>(CanHelper.GetAdapterKey(connection), out var adapter))
                    throw new InvalidOperationException($"CAN 连接 {connection} 不存在，请先执行 CAN_Open");
                checkBackend = () =>
                {
                    checkExecution();
                    if (!context.Resources.TryGet<ICanAdapter>(CanHelper.GetAdapterKey(connection), out var current) || !ReferenceEquals(current, adapter))
                        throw new InvalidOperationException("绑定期间 CAN 连接已被移除或替换");
                };
                checkBackend();
                var node = CanopenConnectionAccess.For(adapter).ReserveNode((byte)values.Number(nameof(setting.NodeId)), setting.CommunicationTimeoutMs);
                var canopen = new CanopenAxisTransport(node, setting);
                transport = canopen;
                checkBackend();
                await canopen.InitializeAsync(ct).ConfigureAwait(false);
            }
            else
            {
                if (!context.Resources.TryGet<ICia402EtherCatFactory>(ICia402EtherCatFactory.ResourceKey, out var factory))
                    throw new InvalidOperationException("未配置 EtherCAT 主站适配器；当前仅提供适配接口，不能执行 EtherCAT 运动");
                checkBackend = () =>
                {
                    checkExecution();
                    if (!context.Resources.TryGet<ICia402EtherCatFactory>(ICia402EtherCatFactory.ResourceKey, out var current) || !ReferenceEquals(current, factory))
                        throw new InvalidOperationException("绑定期间 EtherCAT 工厂已被移除或替换");
                };
                var opening = Task.Run(() => { checkBackend(); return factory.OpenAsync(new(connection, (int)values.Number(nameof(setting.SlaveAddress)), setting), ct); }, ct);
                try { transport = await opening.WaitAsync(ct).ConfigureAwait(false) ?? throw new InvalidOperationException("EtherCAT 主站未返回轴通信实例"); }
                catch
                {
                    // 超时后才返回的后端也必须释放，不能泄漏从站占用。
                    _ = opening.ContinueWith(task =>
                    {
                        if (task.IsCompletedSuccessfully)
                        {
                            try { task.Result?.Dispose(); }
                            catch (Exception ex) { cleanupLog.Report("晚到的轴通信实例释放失败", ex); }
                        }
                        else _ = task.Exception;
                    }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                    throw;
                }
            }
            var controller = new AxisController(transport, setting);
            checkBackend();
            transport = controller.Transport;
            if (transport is CoordinatedAxisTransport coordinated)
                coordinated.CleanupError = ex => cleanupLog.Report("初始化失败后释放轴通信实例失败", ex);
            await controller.InitializeAsync(ct).ConfigureAwait(false);
            checkBackend();
            ct.ThrowIfCancellationRequested();
            checkExecution();
            // ct/callerToken 只约束本次绑定；已注册的跨步骤资源不能跟随它们的后续取消被关闭。
            session = new AxisSession(controller, setting,
                (message, ex) => context.Log(ex == null && !message.StartsWith("轴会话终止", StringComparison.Ordinal) ? LogLevel.Debug : LogLevel.Warn,
                    $"轴 {name}：{message}", ex == null ? null : AxisFailureContext.ExceptionDetail(ex)),
                $"轴={name}; 连接={connection}; 后端={setting.Transport}; 地址={values.Number(setting.Transport == TransportKind.CANopen ? nameof(setting.NodeId) : nameof(setting.SlaveAddress))}; 绑定时状态={AxisController.DecodeState(controller.BindingStatusWord)}(0x{controller.BindingStatusWord:X4}); 控制字=0x{controller.BindingControlWord:X4}; 绑定步骤=[{AxisFailureContext.Operation(context, setting)}]");
            PluginCommon.ExecutionScopedExecutor.Own(session.Dispose);
            if (!context.Resources.TryAdd(key, session, ResourceLifetime.Execution)) throw new InvalidOperationException($"轴会话 {name} 已存在");
            checkExecution();
            if (!context.Resources.TryGet<AxisSession>(key, out var registered) || !ReferenceEquals(registered, session))
                throw new InvalidOperationException("执行环境未提供有效的资源注册表，无法保存轴会话");
            ct.ThrowIfCancellationRequested();
            if (context.IsAbortRequested) throw new OperationCanceledException("绑定轴期间执行环境发出中止", ct);
            // 新增过程诊断复用有界日志队列；宿主日志故障不得撤回已经成功建立的会话。
            var bindingLog = new AxisDiagnosticLog((message, _) => context.Log(LogLevel.Info, message));
            bindingLog.Report($"轴 {name} 已绑定：状态={AxisController.DecodeState(controller.BindingStatusWord)}，状态字=0x{controller.BindingStatusWord:X4}，控制字=0x{controller.BindingControlWord:X4}；绑定未改变使能、模式或目标，不代表驱动已停止", null);
            var created = session;
            return () =>
            {
                created.Dispose();
                if (context.Resources.TryGet<AxisSession>(key, out var current) && ReferenceEquals(current, created))
                    context.Resources.Remove(key);
            };
        }
        catch (Exception ex)
        {
            if (session != null)
            {
                session.Dispose();
                try
                {
                    if (context.Resources.TryGet<AxisSession>(key, out var current) && ReferenceEquals(current, session))
                        context.Resources.Remove(key);
                }
                catch (Exception cleanupError) { cleanupLog.Report("绑定失败后移除轴资源失败", cleanupError); }
                AxisFailureContext.AttachSession(ex, session);
            }
            else transport?.Dispose();
            throw;
        }
    }
}
