using System.Diagnostics;
using NiGrpc.Runtime;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace NiGrpc.Executors;

public enum Operation { Connect, OpenSession, Invoke, CloseSession, Disconnect }

internal sealed class NiGrpcExecutor<T>(Operation operation, IStepSettingSerializer serializer, NiDriver? driver, string? fixedMethod) : IStepExecutor where T : NiGrpcSetting
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        RemoteSession? session = null;
        string? completedMethod = null;
        CrossRunCoordinator.Lease? lease = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var epoch = PluginCommon.ExecutionEpoch.Capture(context.Resources, "NiGrpc");
            var step = context.CurrentStep!.Step;
            NiGrpcSetting setting = (T)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setting.TimeoutMs);
            var clock = Stopwatch.StartNew();
            bool cleanupOperation = operation is Operation.CloseSession or Operation.Disconnect;
            void CheckAdmission()
            {
                cancellationToken.ThrowIfCancellationRequested(); epoch.ThrowIfExpired();
                lease?.CheckAdmission(cleanupOperation);
            }
            int Remaining()
            {
                CheckAdmission();
                return clock.ElapsedMilliseconds < setting.TimeoutMs ? setting.TimeoutMs - (int)clock.ElapsedMilliseconds
                    : throw new TimeoutException($"NI gRPC 步骤超过配置的总期限 {setting.TimeoutMs} ms");
            }
            lease = await CrossRunCoordinator.Shared.EnterAsync(epoch, epoch.StopSignal.StopRequested, Remaining(), cancellationToken,
                cleanupOperation);
            async Task<string> Evaluate(string expression) => await ExpressionHelper.EvalBoundedAsync(
                () => Evaluator.EvalStringAsync(expression, context), Remaining(), cancellationToken, lease.Track);
            string name = await Evaluate(setting.ConnectionName);
            _ = Remaining();
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("连接名称不能为空");
            string key = "NiGrpc.Connection:" + name;
            if (operation == Operation.Connect)
            {
                string endpoint = await Evaluate(((NiGrpcConnectSetting)setting).Endpoint);
                _ = Remaining();
                var wire = new GrpcNiWire(endpoint);
                var created = new RemoteConnection(wire, wire, setting.TimeoutMs,
                    ex => context.Log(LogLevel.Warn, "NI gRPC 自动关闭未确认，请检查远端会话", ex.ToString()), lease.TrackCleanup, lease.MarkUnknown);
                try { lease.RegisterConnection(() => created.RequestClose(setting.TimeoutMs), created.CloseCompletion, created.CloseStarted); }
                catch { created.Dispose(); throw; }
                var registry = context.Resources;
                PluginCommon.ExecutionScopedExecutor.Own(() =>
                {
                    created.Dispose();
                    _ = RegistryFence.RemoveAfterCloseAsync(registry, key, created, setting.TimeoutMs).ContinueWith(t => _ = t.Exception,
                        TaskContinuationOptions.OnlyOnFaulted);
                });
                try { await wire.ProbeAsync(Remaining(), cancellationToken, epoch.StopSignal.StopRequested, lease.TrackCleanup); _ = Remaining(); }
                catch { created.Dispose(); throw; }
                var gate = RegistryFence.Get(registry, key);
                if (!await gate.WaitAsync(Remaining(), cancellationToken)) { created.Dispose(); throw new TimeoutException("NI gRPC 连接登记等待超时"); }
                try
                {
                    _ = Remaining();
                    if (!registry.TryAdd(key, created, ResourceLifetime.Execution))
                    { created.Dispose(); throw new InvalidOperationException($"NI gRPC 连接 {name} 已存在"); }
                    try { _ = Remaining(); }
                    catch
                    {
                        _ = RegistryFence.RemoveAfterCloseAsync(registry, key, created, setting.TimeoutMs).ContinueWith(t => _ = t.Exception,
                            TaskContinuationOptions.OnlyOnFaulted);
                        throw;
                    }
                }
                finally { gate.Release(); }
                return Done($"NI gRPC 服务器健康检查通过：{name}；驱动可用性以实际调用为准");
            }
            if (!context.Resources.TryGet<RemoteConnection>(key, out var connection))
                throw new InvalidOperationException($"NI gRPC 连接 {name} 不存在，请先连接");
            if (operation == Operation.Disconnect)
            {
                Task? detach = null;
                try { await connection.CloseAsync(Remaining(), cancellationToken); }
                finally
                {
                    if (connection.IsClosing)
                    {
                        detach = RegistryFence.RemoveAfterCloseAsync(context.Resources, key, connection, setting.TimeoutMs);
                        _ = detach.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                    }
                }
                if (detach != null) await detach.WaitAsync(TimeSpan.FromMilliseconds(Remaining()), cancellationToken);
                _ = Remaining();
                return Done($"NI gRPC 连接已关闭：{name}");
            }
            string alias = await Evaluate(((NiGrpcSessionSetting)setting).SessionName);
            _ = Remaining();
            if (string.IsNullOrWhiteSpace(alias)) throw new ArgumentException("会话别名不能为空");
            if (operation == Operation.OpenSession)
            {
                string json = setting is NiGrpcXnetOpenSetting xnetSetting ? await Evaluate(xnetSetting.ConfigurationJson) : "{}";
                try
                {
                    session = await connection.OpenSessionAsync(alias, driver!.Value, json, Remaining(), cancellationToken, CheckAdmission, epoch.StopSignal.StopRequested);
                    _ = Remaining();
                }
                catch
                {
                    if (session != null)
                    {
                        var closing = lease.BeginSessionClose();
                        _ = connection.CloseSessionIfSameAsync(alias, session, setting.TimeoutMs, CancellationToken.None).ContinueWith(t =>
                        {
                            try
                            {
                                if (t.Exception is { } cleanup)
                                {
                                    if (!session.ClearConfirmed) lease.MarkUnknown($"已创建会话 {session.RemoteName} 的自动收尾未确认");
                                    context.Log(LogLevel.Warn, $"NI gRPC 已创建会话的自动收尾未确认；远端名称={session.RemoteName}", cleanup.ToString());
                                }
                            }
                            finally { closing.Dispose(); }
                        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                    }
                    throw;
                }
                return Done($"已创建 {driver} 会话 {alias}；远端名称={session.RemoteName}");
            }
            if (operation == Operation.CloseSession)
            {
                using var closing = lease.BeginSessionClose();
                await connection.CloseSessionAsync(alias, Remaining(), cancellationToken, CheckAdmission, epoch.StopSignal.StopRequested);
                _ = Remaining();
                return Done($"NI gRPC 会话已关闭：{alias}");
            }
            session = await connection.GetSessionAsync(alias, cancellationToken).WaitAsync(TimeSpan.FromMilliseconds(Remaining()), cancellationToken);
            if (session.Driver != driver) throw new InvalidOperationException($"会话驱动为 {session.Driver}，本步骤需要 {driver}；未提交调用");
            string method = fixedMethod ?? throw new InvalidOperationException("未指定步骤对应的驱动方法");
            var requestSetting = (NiGrpcRequestSetting)setting;
            string request = await Evaluate(requestSetting.RequestJson);
            if (!string.IsNullOrWhiteSpace(requestSetting.ResultVariable))
            {
                if (!context.HasVariable(requestSetting.ResultVariable)) throw new InvalidOperationException($"响应变量 {requestSetting.ResultVariable} 不存在；未提交驱动调用");
                if (context.GetVariable(requestSetting.ResultVariable) is { } value && value is not string)
                    throw new InvalidOperationException($"响应变量 {requestSetting.ResultVariable} 不是字符串；未提交驱动调用");
            }
            string result = await session.InvokeAsync(method, request, Remaining(), cancellationToken, CheckAdmission, epoch.StopSignal.StopRequested);
            completedMethod = method;
            RemoteResultPublication.Publish(method, session.RemoteName, () => _ = Remaining(), () =>
            {
                if (!string.IsNullOrWhiteSpace(requestSetting.ResultVariable)) context.SetVariable(requestSetting.ResultVariable, result);
            });
            return Done(result);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            if (completedMethod != null && session != null)
                context.Log(LogLevel.Warn, $"NI {completedMethod} 已收到服务端成功响应，但步骤取消；远端名称={session.RemoteName}，不能盲目重试");
            else if (session?.State == RemoteState.Unknown)
                context.Log(LogLevel.Warn, $"NI gRPC 调用已取消，远端结果未知；会话名称={session.RemoteName}");
            else if (ex.Message.Contains("远端", StringComparison.Ordinal))
                context.Log(LogLevel.Warn, "NI gRPC 操作已取消；远端状态可能未知", ex.Message);
            return new() { StepResult = new() { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        { return new() { StepResult = new() { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex, $"NI gRPC 操作失败：{ex.Message}") } }; }
        finally { lease?.Dispose(); }
    }
    private static ExecutionResult Done(string value) => new() { StepResult = new() { Status = TestStatus.Completed, Value = value } };
}
