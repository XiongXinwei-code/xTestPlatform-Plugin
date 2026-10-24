using System.Text;
using CiA402.Models;
using xTestPlatform.Core.Engine;

namespace CiA402.Core;

/// <summary>仅从当前调用和本地快照生成诊断，不为日志额外访问驱动。</summary>
internal static class AxisFailureContext
{
    public const string TransportKey = "CiA402.Transport";
    private const string SessionKey = "CiA402.Session";

    public static void AttachTransport(Exception error, string detail)
    {
        // 部分第三方异常提供只读 Data；诊断附加失败不能替换原异常。
        try { error.Data[TransportKey] = detail; } catch (Exception) { }
    }

    public static void AttachSession(Exception error, AxisSession session)
    {
        // Unbind 的 finally 会移除资源，必须在移除之前冻结本次失败的轴上下文。
        try { error.Data[SessionKey] = session.DiagnosticContext; } catch (Exception) { }
    }

    public static string Step(IExecutionContext context)
    {
        var current = context.CurrentStep;
        return $"步骤={current?.StepName}; StepType={current?.Step?.StepSetting.StepType}; 地址={current?.StepAddress}; LoopIndex={current?.LoopIndex}";
    }

    public static string Operation(IExecutionContext context, AxisSetting setting)
        => $"{Step(context)}; TimeoutMs={setting.TimeoutMs}";

    public static string ExceptionDetail(Exception exception)
    {
        var text = new StringBuilder(exception.ToString());
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        Append(exception);
        return text.ToString();

        void Append(Exception error)
        {
            if (!visited.Add(error)) return;
            if (error.Data[TransportKey] is string transport) text.AppendLine().Append(transport);
            if (error is AggregateException aggregate)
                foreach (var inner in aggregate.InnerExceptions) Append(inner);
            else if (error.InnerException is { } inner) Append(inner);
        }
    }

    public static string Detail(string step, AxisSetting? setting, EvaluatedSettings? values,
        IExecutionContext context, string stage, long elapsedMs, bool callerCancelled, Exception error)
    {
        var text = new StringBuilder()
            .AppendLine($"CiA402 插件版本={typeof(AxisSession).Assembly.GetName().Version}; {step}")
            .AppendLine($"阶段={stage}; 耗时={elapsedMs} ms; 调用方令牌取消={callerCancelled}; IsAbortRequested={context.IsAbortRequested}")
            .AppendLine($"AxisName表达式={setting?.AxisName}; TimeoutMs={setting?.TimeoutMs}; PollIntervalMs={setting?.PollIntervalMs}");
        if (values != null)
        {
            var name = values.Text(nameof(AxisSetting.AxisName));
            text.AppendLine($"已解析轴名={name}; 资源键={AxisSession.Key(name)}");
            if (error.Data[SessionKey] is string snapshot) text.AppendLine($"异常发生时轴快照：{snapshot}");
            else if (context.Resources.TryGet<AxisSession>(AxisSession.Key(name), out var session))
                text.AppendLine(session.DiagnosticContext);
            else text.AppendLine("当前资源表无该轴会话（可能未绑定或已移除）");
            if (setting is BindAxisSetting binding)
                text.AppendLine($"绑定目标={binding.Transport}; 连接={values.Text(nameof(binding.ConnectionName))}; 地址={values.Number(binding.Transport == TransportKind.CANopen ? nameof(binding.NodeId) : nameof(binding.SlaveAddress))}; CommunicationTimeoutMs={binding.CommunicationTimeoutMs}");
        }
        return text.AppendLine(ExceptionDetail(error)).ToString();
    }
}
