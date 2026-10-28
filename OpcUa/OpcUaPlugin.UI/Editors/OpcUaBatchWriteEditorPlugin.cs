using System.Windows;
using OpcUa.Helpers;
using OpcUa.Models;
using OpcUa.UI.Views;
using OpcUa.UI.Validation;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI;

public sealed class OpcUaBatchWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "OpcUa.BatchWrite";
    public string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua_batch_write.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new OpcUaBatchWriteEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new OpcUaBatchWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (OpcUaBatchWriteSetting)new OpcUaBatchWritePlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("OPCUA_050", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connectionError))
            errors.Add(StepSettingError.Error("OPCUA_050E", $"ConnectionName 表达式无效: {connectionError}"));
        if (s.Items.Count == 0)
            errors.Add(StepSettingError.Warning("OPCUA_051", "节点列表为空"));
        for (int i = 0; i < s.Items.Count; i++)
        {
            var item = s.Items[i];
            if (string.IsNullOrWhiteSpace(item.NodeId))
                errors.Add(StepSettingError.Error("OPCUA_052", $"第 {i + 1} 行：节点标识不能为空"));
            else if (!OpcUaHelper.IsValidNodeId(item.NodeId))
                errors.Add(StepSettingError.Error("OPCUA_052F", $"第 {i + 1} 行：节点标识格式无效，正确格式如 ns=2;s=MyVariable"));
            if (string.IsNullOrWhiteSpace(item.WriteValue))
                errors.Add(StepSettingError.Error("OPCUA_053", $"第 {i + 1} 行：写入值不能为空"));
            else if (!context.Evaluator.ValidateExpression(item.WriteValue, context.ExecutionContext, out var valueError))
                errors.Add(StepSettingError.Error("OPCUA_053E", $"第 {i + 1} 行：WriteValue 表达式无效: {valueError}"));
            if (!Enum.IsDefined(item.DataType)) errors.Add(StepSettingError.Error("OPCUA_DATA_TYPE", $"第 {i + 1} 行：DataType 无效"));
        }
        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("OPCUA_054", "超时必须大于 0 ms；当前 OPC UA 步骤不支持 -1 无限等待"));
        OpcUaLifecycleValidator.CheckPrecedingConnect(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return errors;
    }
}
