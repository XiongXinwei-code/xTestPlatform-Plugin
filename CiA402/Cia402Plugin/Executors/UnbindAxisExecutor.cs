using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Executors;

public sealed class UnbindAxisExecutor : ExecutorBase<UnbindAxisSetting>
{
    protected override async Task<Action?> RunAsync(UnbindAxisSetting setting, EvaluatedSettings values, IExecutionContext context, CancellationToken ct, CancellationToken callerToken, Action checkExecution)
    {
        var key = AxisSession.Key(values.Text(nameof(setting.AxisName)));
        using var binding = await AxisBindingGate.EnterAsync(context, key, ct).ConfigureAwait(false);
        checkExecution();
        if (!context.Resources.TryGet<AxisSession>(key, out var session)) return null;
        checkExecution();
        try { await session.CloseAsync(ct, AxisFailureContext.Operation(context, setting)).ConfigureAwait(false); }
        catch (Exception ex) { AxisFailureContext.AttachSession(ex, session); throw; }
        finally
        {
            // 超时也移除终止会话；节点实际占用保留到活动 I/O 退出。
            if (context.Resources.TryGet<AxisSession>(key, out var current) && ReferenceEquals(current, session))
                context.Resources.Remove(key);
        }
        return null;
    }
}
