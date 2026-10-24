using Opc.Ua;
using Opc.Ua.Client;
using OpcUa.Helpers;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace OpcUa.Executors;

/// <summary>OPC UA 写入节点执行器</summary>
public sealed class OpcUaWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;
    private readonly Func<NodeId, CancellationToken, Task<DataValue>>? _readValue;
    private readonly Func<WriteValueCollection, CancellationToken, Task<WriteResponse>>? _writeValues;
    public OpcUaWriteExecutor() { }
    internal OpcUaWriteExecutor(Func<NodeId, CancellationToken, Task<DataValue>> readValue,
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
            var serializer = new OpcUaWritePlugin().CreateSerializer();
            var setting = (OpcUaWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            if (!Enum.IsDefined(setting.DataType)) throw new ArgumentOutOfRangeException(nameof(setting.DataType), "未知 DataType，不能按字符串猜测写入");

            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var nodeIdStr = setting.NodeId;
            var writeValueStr = await Evaluator.EvalStringAsync(setting.WriteValue, context);
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

            var nodeId = OpcUaHelper.ParseNodeId(nodeIdStr);
            var response = await connection.RunAsync(async token =>
            {
            guard.EnsureCurrent(key, connection);
            connection.ThrowIfSubmissionStopped();
            // 如果是 Auto 类型，先读取节点的数据类型
            object writeValue;
            if (setting.DataType == OpcUaDataType.Auto)
            {
                var currentValue = await (_readValue?.Invoke(nodeId, token) ?? connection.Session.ReadValueAsync(nodeId, token));
                guard.EnsureCurrent(key, connection);
                connection.ThrowIfSubmissionStopped();
                if (!StatusCode.IsGood(currentValue.StatusCode))
                    throw new InvalidOperationException($"Auto 类型读取失败，节点质量={currentValue.StatusCode}，未提交写入");
                if (currentValue.Value != null)
                {
                    writeValue = OpcUaHelper.ConvertAutoValue(writeValueStr, currentValue.Value.GetType());
                }
                else
                {
                    writeValue = writeValueStr;
                }
            }
            else
            {
                writeValue = OpcUaHelper.ConvertValue(writeValueStr, setting.DataType);
            }

            var writeValueCollection = new WriteValueCollection
            {
                new WriteValue
                {
                    NodeId = nodeId,
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(writeValue))
                }
            };

                guard.EnsureCurrent(key, connection);
                connection.ThrowIfSubmissionStopped();
                return await (_writeValues?.Invoke(writeValueCollection, token) ?? connection.Session.WriteAsync(null, writeValueCollection, token));
            }, setting.TimeoutMs, "OPC UA 单点写入（含 Auto 类型读取）", cancellationToken);
            guard.EnsureCurrent(key, connection);

            if (response.Results == null || response.Results.Count != 1)
                throw new InvalidOperationException("OPC UA 单点写入响应数量不为 1，不能判定写入成功");

            if (StatusCode.IsBad(response.Results[0]))
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"写入节点失败: {response.Results[0]}" }
                    }
                };
            }

            context.Log($"OPC UA 写入: {nodeIdStr} = {writeValueStr}");
            guard.EnsureCurrent(key, connection);

            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Completed, Value = writeValueStr }
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
                    Error = ErrorInfo.FromException(ex, $"OPC UA 写入失败: {ex.Message}")
                }
            };
        }
    }
}
