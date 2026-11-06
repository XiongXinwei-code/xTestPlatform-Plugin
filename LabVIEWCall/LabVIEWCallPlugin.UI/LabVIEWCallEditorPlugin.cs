using LabVIEWCallPlugin.Execution;
using LabVIEWCallPlugin.Models;
using LabVIEWCallPlugin.UI.Converters;
using LabVIEWCallPlugin.UI.Models;
using LabVIEWCallPlugin.UI.Views;
using StepEditor.Abstractions;
using System.IO;
using System.Windows;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LabVIEWCallPlugin.UI;

public class LabVIEWCallEditorPlugin : IStepEditorPlugin
{
    private readonly ViFileProbe _files;
    public LabVIEWCallEditorPlugin() => _files = ViFileProbe.Shared;
    internal LabVIEWCallEditorPlugin(Func<string, bool> fileExists) => _files = new ViFileProbe(fileExists);

    public virtual string StepTypeId => LabVIEWCallStepPlugin.TypeId;
    public string IconPath =>
        "pack://application:,,,/LabVIEWCall.StepPlugin.UI;component/Resources/Icons/labview.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile) =>
        new LabVIEWCallEditorView(step, sequenceFile);

    // Project validation works with the saved connector snapshot; Reload is the explicit online check.
    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<StepSettingError>();
        LabVIEWCallSetting setting;
        try
        {
            var serializer = new LabVIEWCallStepPlugin().CreateSerializer();
            setting = context.Setting is { Length: > 0 } bytes
                ? (LabVIEWCallSetting)serializer.Deserialize(bytes, serializer.SettingVersion)
                : new LabVIEWCallSetting();
        }
        catch (Exception ex)
        {
            errors.Add(StepSettingError.Error("LV_SETTING_INVALID", $"无法读取 LabVIEW 步骤设置：{ex.Message}"));
            return errors;
        }

        if (string.IsNullOrWhiteSpace(setting.ViFilePath))
            errors.Add(StepSettingError.Error("LV_VI_PATH_EMPTY", "请选择 VI 文件。"));
        else if (!Path.IsPathFullyQualified(setting.ViFilePath) ||
                 !string.Equals(Path.GetExtension(setting.ViFilePath), ".vi", StringComparison.OrdinalIgnoreCase))
            errors.Add(StepSettingError.Error("LV_VI_PATH_INVALID", $"VI 文件不存在或扩展名有误：{setting.ViFilePath}"));
        else if (setting.TimeoutMs >= 0)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (setting.TimeoutMs > 0) deadline.CancelAfter(setting.TimeoutMs);
            try
            {
                if (!await _files.ExistsAsync(setting.ViFilePath, deadline.Token))
                    errors.Add(StepSettingError.Error("LV_VI_PATH_INVALID", $"VI 文件不存在或无法访问：{setting.ViFilePath}"));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
            {
                errors.Add(StepSettingError.Error("LV_PATH_TIMEOUT", $"检查 VI 文件超过 {setting.TimeoutMs} ms：{setting.ViFilePath}"));
            }
        }

        if (setting.ViServerPort is < 1 or > 65535)
            errors.Add(StepSettingError.Error("LV_PORT_INVALID", "VI Server 端口必须在 1 至 65535 之间。"));

        if (setting.TimeoutMs < 0)
            errors.Add(StepSettingError.Error("LV_TIMEOUT_INVALID", "超时时间不能为负数。"));
        else if (setting.TimeoutMs == 0)
            errors.Add(StepSettingError.Warning("LV_TIMEOUT_UNLIMITED", "已选择无限等待，请确认 VI 具备退出条件。"));

        CheckDocument(setting.InputParameters, false);
        CheckDocument(setting.OutputParameters, true);
        cancellationToken.ThrowIfCancellationRequested();
        return errors;

        void CheckDocument(string json, bool output)
        {
            try
            {
                LabVIEWCallExecutor.ValidateParameterDocument(json, output);
                foreach (var node in Flatten(LvPanelConverter.ConvertFromJson(json)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (output)
                    {
                        if (!string.IsNullOrWhiteSpace(node.Variable) && !context.ExecutionContext.HasVariable(node.Variable))
                            errors.Add(StepSettingError.Error("LV_OUTPUT_NOT_FOUND", $"输出 [{node.Name}] 的目标变量未声明：{node.Variable}"));
                    }
                    else if (node.ValueSourceTypeEnum == ValueSourceType.Variable)
                    {
                        foreach (var path in context.Evaluator.GetReferencedVariables(node.Variable).Distinct())
                            if (!context.ExecutionContext.HasVariable(path))
                                errors.Add(StepSettingError.Error("LV_INPUT_NOT_FOUND", $"输入 [{node.Name}] 引用了未声明变量：{path}"));
                        if (!context.Evaluator.ValidateExpression(node.Variable, context.ExecutionContext, out var error))
                            errors.Add(StepSettingError.Error("LV_EXPRESSION_INVALID", $"输入 [{node.Name}] 表达式无效：{error}"));
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                errors.Add(StepSettingError.Error("LV_PARAMETERS_INVALID",
                    $"{(output ? "输出" : "输入")}端子配置无效：{ex.Message}"));
            }
        }
    }

    private static IEnumerable<LvPanelNode> Flatten(IEnumerable<LvPanelNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children)) yield return child;
        }
    }
}
