using System.Windows;
using OpcUa.Helpers;
using OpcUa.Models;
using OpcUa.UI.Views;
using OpcUa.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI;

public sealed class OpcUaSubscribeEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.Subscribe";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua_subscribe.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaSubscribeEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new OpcUaSubscribePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (OpcUaSubscribeSetting)new OpcUaSubscribePlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_060", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("OPCUA_060E", $"ConnectionName 表达式无效: {connErr}"));
        if (string.IsNullOrWhiteSpace(s.NodeId))
            errors.Add(StepSettingError.Error("OPCUA_061", "节点 ID 不能为空"));
        else if (!OpcUaHelper.IsValidNodeId(s.NodeId))
            errors.Add(StepSettingError.Error("OPCUA_061F", "节点 ID 格式无效，正确格式如 ns=2;s=MyVariable"));
        if (string.IsNullOrWhiteSpace(s.ExpectedValue))
            errors.Add(StepSettingError.Error("OPCUA_064_EMPTY", "ExpectedValue 表达式不能为空；期望空字符串时请填写 \"\" 字符串表达式"));
        else if (!context.Evaluator.ValidateExpression(s.ExpectedValue, context.ExecutionContext, out var expErr))
            errors.Add(StepSettingError.Error("OPCUA_064", $"ExpectedValue 表达式无效: {expErr}"));
        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("OPCUA_062", "超时必须大于 0 ms；当前 OPC UA 步骤不支持 -1 无限等待"));
        if (!Enum.IsDefined(s.CompareMode)) errors.Add(StepSettingError.Error("OPCUA_COMPARE_MODE", "CompareMode 无效"));
        if (!string.IsNullOrWhiteSpace(s.ResultVariable) && !context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("OPCUA_SUB_VARIABLE", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
        if (s.SamplingIntervalMs <= 0)
            errors.Add(StepSettingError.Error("OPCUA_063", "采样间隔必须大于 0"));
        OpcUaLifecycleValidator.CheckPrecedingConnect(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
