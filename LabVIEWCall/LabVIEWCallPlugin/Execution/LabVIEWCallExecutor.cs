using LabVIEWCallPlugin.Models;
using LabVIEWCallPlugin.LVadapter;
using LabVIEWCallPlugin.LVadapter.Interop;
using MessagePack;
using MessagePack.Resolvers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace LabVIEWCallPlugin.Execution
{
    /// <summary>
    /// LabVIEW Call 步骤执行器。
    /// 支持树形参数结构（标量、数组、簇及嵌套簇）。
    /// 按原生类型描述符核对端子并读写 Flatten To String 数据。
    /// </summary>
    public sealed partial class LabVIEWCallExecutor : IStepExecutor
    {
        internal delegate Dictionary<string, object?> InvokeVi(LabVIEWCallSetting setting,
            List<ViParameterNode> inputs, List<ViParameterNode> outputs, CancellationToken ct, Action checkActive);
        private readonly InvokeVi? _invoke;
        private readonly ViFileProbe _files;
        public LabVIEWCallExecutor() => _files = ViFileProbe.Shared;
        internal LabVIEWCallExecutor(InvokeVi invoke, Func<string, bool>? fileExists = null)
            => (_invoke, _files) = (invoke, fileExists == null ? ViFileProbe.Shared : new ViFileProbe(fileExists));

        private static readonly MessagePackSerializerOptions _msgpackOptions =
            MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithResolver(ContractlessStandardResolver.Instance);

        public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
        {
            var callId = Guid.NewGuid().ToString("N");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            string phase = "读取设置";
            string target = "尚未解析";
            int timeoutMs = 0;
            int port = 3363;
            int publishedVariables = 0;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var retirement = PluginCommon.ExecutionScopedExecutor.OwnOperation(deadline.Cancel);
            var ct = deadline.Token;
            try
            {
                var epoch = PluginCommon.ExecutionEpoch.Capture(context.Resources, "LabVIEWCall");
                void CheckActive()
                {
                    ct.ThrowIfCancellationRequested();
                    epoch.ThrowIfExpired();
                    if (context.IsAbortRequested)
                        throw new InvalidOperationException("平台已请求停止，禁止提交 VI 或发布结果");
                }
                CheckActive();
                var bytes = context.CurrentStep?.Step?.StepSetting?.Setting;
                var setting = bytes is { Length: > 0 }
                    ? MessagePackSerializer.Deserialize<LabVIEWCallSetting>(bytes, _msgpackOptions)
                    : new LabVIEWCallSetting();
                timeoutMs = setting.TimeoutMs;
                if (timeoutMs < 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs), "VI 执行超时不能为负数");
                port = setting.ViServerPort;
                if (port is < 1 or > ushort.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(setting.ViServerPort), "VI Server 端口必须为 1–65535");
                if (timeoutMs > 0) deadline.CancelAfter(timeoutMs);
                phase = "检查 VI 与参数";
                ArgumentException.ThrowIfNullOrWhiteSpace(setting.ViFilePath);
                if (!Path.IsPathFullyQualified(setting.ViFilePath))
                    throw new ArgumentException("请配置 VI 的完整路径");
                target = setting.ViFilePath = Path.GetFullPath(setting.ViFilePath);
                if (!Path.GetExtension(target).Equals(".vi", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("请选择扩展名为 .vi 的文件");
                phase = "检查 VI 文件";
                bool exists = await _files.ExistsAsync(target, ct).ConfigureAwait(false);
                CheckActive();
                if (!exists) throw new FileNotFoundException("找不到指定的 .vi 文件或无法访问该路径", target);
                phase = "申请调用";
                // The same deadline covers binding, native execution and publication.
                await ViCallGate.RunStepAsync($"{callId} {target}", ct, async token =>
                {
                    CheckActive();
                    var inputs = ParseParameterNodes(setting.InputParameters, false);
                    var outputs = ParseParameterNodes(setting.OutputParameters, true);
                    CheckActive();
                    string limit = timeoutMs == 0 ? "不限制" : $"{timeoutMs} ms";
                    context.Log(LogLevel.Debug,
                        $"准备调用 VI：{Path.GetFileName(target)}；已配置输入端子数={inputs.Count}；已配置输出端子数={outputs.Count}；总等待期限={limit}",
                        $"调用标识={callId}\nVI 路径={target}\nVI Server=127.0.0.1:{port}\n"
                        + "数量为步骤配置的顶层端子数，不是参数值或返回数据；簇的子项不重复计数。输出端子可不映射平台变量。此时尚未确认 VI 已开始运行。");
                    phase = "绑定输入";
                    ValidateOutputTargets(outputs, context);
                    await ResolveInputVariablesAsync(inputs, context, CheckActive).ConfigureAwait(false);
                    CheckActive();
                    phase = "调用独立 VI 桥接进程";
                    var values = _invoke != null ? _invoke(setting, inputs, outputs, token, CheckActive) :
                        ExecuteVI(setting, inputs, outputs, token, CheckActive, warning =>
                        {
                            CheckActive();
                            context.Log(LogLevel.Warn, $"LabVIEW 调用 {callId}，VI={target}：{warning}");
                        });
                    phase = "发布输出";
                    // Keep the actual ticket while any admitted host setter is still in flight.
                    // The SDK cannot retract a setter that has already entered platform code.
                    CheckActive();
                    foreach (var (path, value) in values)
                    {
                        CheckActive();
                        context.SetVariable(path, value);
                        publishedVariables++;
                    }
                    return true;
                }).ConfigureAwait(false);
                CheckActive();
                context.Log(LogLevel.Debug,
                    $"VI 调用完成：{Path.GetFileName(target)}；已写入平台变量={publishedVariables} 个；总耗时={clock.ElapsedMilliseconds} ms",
                    $"调用标识={callId}\nVI 路径={target}\nVI Server=127.0.0.1:{port}\n"
                    + "总耗时包含设置校验、参数传递、VI 执行和输出发布，不是 VI 的纯运行时间。Completed 表示调用流程成功完成，不是测试通过判定。");
                return new ExecutionResult { StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value = $"VI 调用完成，已写入 {publishedVariables} 个输出变量"
                } };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return ExecutionResults.Aborted();
            }
            catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
            {
                // Retirement is independent of caller cancellation and of the wall-clock deadline.
                try { PluginCommon.ExecutionScopedExecutor.ThrowIfCurrentStepRetired(); }
                catch (Exception retired) { return ExecutionResults.FromException(retired, $"LabVIEW 调用 {callId} 所在序列已退出，阶段={phase}"); }
                var timeout = new TimeoutException($"等待超过 {timeoutMs} ms；已提交的原生工作可能继续，实际返回前不接受新调用", ex);
                return ExecutionResults.FromException(timeout, $"LabVIEW 调用超时（{timeoutMs} ms）：{target}，VI Server=127.0.0.1:{port}，阶段={phase}，调用={callId}");
            }
            catch (Exception ex)
            {
                return ExecutionResults.FromException(ex, $"LabVIEW {phase}失败：{ex.Message}；VI={target}，VI Server=127.0.0.1:{port}，调用={callId}，总耗时={clock.ElapsedMilliseconds} ms");
            }
        }
        // ── VI 执行核心 ───────────────────────────────────────────────────
        private static Dictionary<string, object?> ExecuteVI(
            LabVIEWCallSetting setting, List<ViParameterNode> inputs, List<ViParameterNode> outputs, CancellationToken ct,
            Action checkActive, Action<string> reportWarning)
        {
            // Complete local formatting before loading a VI or writing any of its controls.
            var values = inputs.Select(input => ViWireCodec.Encode(input)).ToArray();
            var descriptors = new short[inputs.Count][];
            checkActive();
            var bridge = IsolatedViBridge.Shared;
            int port = setting.ViServerPort;
            outputs = CheckLiveConnector(bridge.GetMetadata(setting.ViFilePath, ct, checkActive, port), inputs, outputs);
            checkActive();
            uint reference = bridge.Load(setting.ViFilePath, ct, checkActive, port);

            // Validate native layout for all terminals before submitting any control writes or the Run call.
            for (int i = 0; i < inputs.Count; i++)
            {
                checkActive();
                var (td, _) = bridge.Get(reference, inputs[i].Name, ct, checkActive, port);
                ViWireCodec.CheckDescriptor(inputs[i], td);
                descriptors[i] = td;
            }
            foreach (var output in outputs)
            {
                checkActive();
                var (td, _) = bridge.Get(reference, output.Name, ct, checkActive, port);
                ViWireCodec.CheckDescriptor(output, td);
            }
            for (int i = 0; i < inputs.Count; i++)
            {
                checkActive();
                bridge.Set(reference, inputs[i].Name, descriptors[i], values[i], ct, checkActive, port);
            }
            checkActive();
            reference = bridge.Run(reference, setting.ShowPanel, setting.ClosePanel, ct, checkActive, port);

            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var output in outputs)
            {
                checkActive();
                var (td, bytes) = bridge.Get(reference, output.Name, ct, checkActive, port);
                ViWireCodec.CheckDescriptor(output, td);
                CollectOutput(output, ViWireCodec.Decode(output, bytes), result, reportWarning);
            }
            return result;
        }

        internal static void CollectOutput(ViParameterNode node, object value, Dictionary<string, object?> result,
            Action<string> reportWarning)
        {
            if (node.Target.Length != 0) result.Add(node.Target, value);
            if (!node.Wire.IsCluster) return;

            var fields = (Dictionary<string, object?>)value;
            if (IsStandardErrorCluster(node))
            {
                int code = (int)fields[node.Children[1].Name]!;
                string source = (string)fields[node.Children[2].Name]!;
                if (fields[node.Children[0].Name] is true)
                    throw node.Error($"VI 返回错误簇，错误码 {code}，来源 {source}。");
                if (code != 0) reportWarning($"VI 端子 [{node.Location}] 返回警告 {code}，来源 {source}。");
            }
            foreach (var child in node.Children) CollectOutput(child, fields[child.Name]!, result, reportWarning);
        }

    }
}
