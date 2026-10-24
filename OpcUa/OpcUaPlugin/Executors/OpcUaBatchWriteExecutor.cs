using Opc.Ua;
using Opc.Ua.Client;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 批量写入执行器</summary>
public sealed class OpcUaBatchWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    private readonly Func<NodeId, CancellationToken, Task<DataValue>>? _readValue;
    private readonly Func<WriteValueCollection, CancellationToken, Task<WriteResponse>>? _writeValues;
    public OpcUaBatchWriteExecutor() { }
    internal OpcUaBatchWriteExecutor(Func<NodeId, CancellationToken, Task<DataValue>> readValue,
        Func<WriteValueCollection, CancellationToken, Task<WriteResponse>> writeValues)
    { _readValue = readValue; _writeValues = writeValues; }

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var guard = new OpcUaExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            guard.Check();
            var serializer = new OpcUaBatchWritePlugin().CreateSerializer();
            var setting = (OpcUaBatchWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (setting.Items.Any(item => !Enum.IsDefined(item.DataType))) throw new ArgumentOutOfRangeException(nameof(setting.Items), "批量写入包含未知 DataType，未提交任何节点写入");

            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            guard.Check();
            var key = OpcUaHelper.GetSessionKey(connName);

            if (!context.Resources.TryGet<OpcUaConnectionResource>(key, out var connection))
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"OPC UA 连接 {connName} 不存在，请先执行 OpcUa_Connect" }
                    }
                };
            }

            if (setting.Items.Count == 0)
            {
                guard.EnsureCurrent(key, connection);
                return new ExecutionResult
                {
                    StepResult = new StepResult { Status = TestStatus.Completed, Value = "无节点需要写入" }
                };
            }

            // 在提交事务前解析表达式；后台不再读取变化中的序列上下文。
            var resolvedValues = new List<string>();
            foreach (var item in setting.Items)
                resolvedValues.Add(await Evaluator.EvalStringAsync(item.WriteValue, context));
            guard.EnsureCurrent(key, connection);
            var response = await connection.RunAsync(async token =>
            {
            var writeValues = new WriteValueCollection();
            for (int index = 0; index < setting.Items.Count; index++)
            {
                guard.EnsureCurrent(key, connection);
                connection.ThrowIfSubmissionStopped();
                var item = setting.Items[index];
                var writeValueStr = resolvedValues[index];
                var nodeId = OpcUaHelper.ParseNodeId(item.NodeId);

                object writeValue;
                if (item.DataType == OpcUaDataType.Auto)
                {
                    var currentValue = await (_readValue?.Invoke(nodeId, token) ?? connection.Session.ReadValueAsync(nodeId, token));
                    guard.EnsureCurrent(key, connection);
                    connection.ThrowIfSubmissionStopped();
                    if (!StatusCode.IsGood(currentValue.StatusCode))
                        throw new InvalidOperationException($"Auto 类型读取失败：{item.NodeId}，质量={currentValue.StatusCode}，未提交批量写入");
                    writeValue = currentValue.Value != null
                        ? OpcUaHelper.ConvertAutoValue(writeValueStr, currentValue.Value.GetType())
                        : writeValueStr;
                }
                else
                {
                    writeValue = OpcUaHelper.ConvertValue(writeValueStr, item.DataType);
                }

                writeValues.Add(new WriteValue
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(writeValue))
                });
            }

                guard.EnsureCurrent(key, connection);
                connection.ThrowIfSubmissionStopped();
                return await (_writeValues?.Invoke(writeValues, token) ?? connection.Session.WriteAsync(null, writeValues, token));
            }, setting.TimeoutMs, "OPC UA 批量写入（含 Auto 类型读取）", cancellationToken);
            guard.EnsureCurrent(key, connection);

            if (response.Results == null || response.Results.Count != setting.Items.Count)
                return ExecutionResults.Error($"OPC UA 连接 {connName} 批量写入响应数量不匹配",
                    detail: $"请求节点数={setting.Items.Count}，响应结果数={response.Results?.Count ?? 0}");

            // 检查结果
            var failures = new List<string>();
            for (int i = 0; i < response.Results.Count; i++)
            {
                if (StatusCode.IsBad(response.Results[i]))
                    failures.Add($"第 {i + 1} 项，NodeId={setting.Items[i].NodeId}，StatusCode={response.Results[i]} (0x{response.Results[i].Code:X8})");
            }

            if (failures.Count > 0)
            {
                return ExecutionResults.Error($"OPC UA 连接 {connName} 批量写入失败：{failures.Count}/{setting.Items.Count} 个节点写入失败",
                    detail: string.Join(Environment.NewLine, failures));
            }

            context.Log($"OPC UA 批量写入: {setting.Items.Count} 个节点完成");
            guard.EnsureCurrent(key, connection);

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = $"已写入 {setting.Items.Count} 个节点" }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || context.IsAbortRequested)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = ErrorInfo.FromException(ex, $"OPC UA 批量写入失败: {ex.Message}")
                }
            };
        }
    }
}
