using System.Windows;
using System.Text.Json;
using CAN.Dbc;
using CAN.Helpers;
using CAN.Models;
using CAN.UI.Validation;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public abstract class CanBackgroundEditorPluginBase(IStepPlugin plugin) : IStepEditorPlugin
{
    public string StepTypeId => plugin.StepTypeId;
    public string IconPath => plugin.IconPath ?? "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_read.png";
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanBackgroundEditorView(plugin) { SequenceFile = sequenceFile };
        view.RefreshFromStep(step); PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true); return view;
    }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        try
        {
            ct.ThrowIfCancellationRequested();
            var serializer = plugin.CreateSerializer();
            var value = serializer.Deserialize(context.Setting, context.CurrentStep?.StepSetting.SettingVersion ?? serializer.SettingVersion);
            var (connection, task, timeout) = value switch
            {
                CanReadBackgroundSetting s => (s.ConnectionName, s.TaskName, s.TimeoutMs),
                CanReadBackgroundStopSetting s => (s.ConnectionName, s.TaskName, s.TimeoutMs),
                CanTaskStatusSetting s => (s.ConnectionName, s.TaskName, s.TimeoutMs),
                _ => throw new ArgumentException("后台步骤设置类型无效")
            };
            CheckExpression(connection, "连接名称", context, errors);
            CheckExpression(task, "任务名称", context, errors);
            if (value is not CanTaskStatusSetting && task?.Trim() == "\"\"") errors.Add(StepSettingError.Error("CAN_BACKGROUND_TASK", "启动/停止任务名称不能是空字符串"));
            if (timeout <= 0) errors.Add(StepSettingError.Error("CAN_BACKGROUND_TIMEOUT", "步骤等待期限必须大于 0 ms"));
            if (value is CanTaskStatusSetting status)
            {
                try { CanTaskStatusOutput.ValidateTarget(status, context.ExecutionContext); }
                catch (ArgumentException ex) { errors.Add(StepSettingError.Error("CAN_BACKGROUND_TARGET", ex.Message)); }
            }
            if (value is CanReadBackgroundSetting start)
            {
                if (start.PublishIntervalMs <= 0 || start.MaxAgeMs <= 0) errors.Add(StepSettingError.Error("CAN_BACKGROUND_INTERVAL", "发布间隔和新鲜度期限必须大于 0 ms"));
                var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(start.ResultVariable)) { CheckTarget(start.ResultVariable, context, errors); destinations.Add(start.ResultVariable); }
                if (start.Mappings == null || start.Mappings.Count is < 1 or > 128) errors.Add(StepSettingError.Error("CAN_BACKGROUND_MAPPINGS", "总线监听需要配置 1～128 条 ID 映射"));
                int enabled = 0;
                var literalRoutes = new List<(uint Id, CanFrameType Type, CanReceiveFrameFormat Format)>();
                foreach (var row in start.Mappings ?? [])
                {
                    ct.ThrowIfCancellationRequested();
                    if (row == null) { errors.Add(StepSettingError.Error("CAN_BACKGROUND_ROW", "ID 映射不能为空")); continue; }
                    if (!row.Enabled) continue;
                    enabled++;
                    CheckExpression(row.CanId, "报文 ID", context, errors);
                    if (!Enum.IsDefined(row.FrameType) || !Enum.IsDefined(row.FrameFormat)) errors.Add(StepSettingError.Error("CAN_BACKGROUND_FORMAT", "ID 映射中的帧类型/格式无效"));
                    CheckLiteralRoute(row, literalRoutes, errors);
                    if (string.IsNullOrWhiteSpace(row.DataVariable)) continue;
                    CheckTarget(row.DataVariable, context, errors);
                    if (!destinations.Add(row.DataVariable)) errors.Add(StepSettingError.Error("CAN_BACKGROUND_DUPLICATE_TARGET", $"映射及 JSON 不能共用输出变量：{row.DataVariable}"));
                }
                if (enabled == 0) errors.Add(StepSettingError.Error("CAN_BACKGROUND_EMPTY", "至少启用一条 ID 映射"));
                if (destinations.Count == 0) errors.Add(StepSettingError.Error("CAN_BACKGROUND_OUTPUT", "请指定 JSON 结果变量或至少一个启用 ID 的数据变量"));
            }
            if (context.CurrentStep is { } step) CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, step, connection, errors);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { errors.Add(StepSettingError.Error("CAN_BACKGROUND_SETTING", $"后台任务设置无效：{ex.Message}")); }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
    private static void CheckExpression(string expression, string name, StepEditorValidationContext context, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(expression)) errors.Add(StepSettingError.Error("CAN_BACKGROUND_EXPRESSION", $"{name}表达式不能为空"));
        else if (!context.Evaluator.ValidateExpression(expression, context.ExecutionContext, out var error)) errors.Add(StepSettingError.Error("CAN_BACKGROUND_EXPRESSION", $"{name}表达式无效：{error}"));
    }
    private static void CheckTarget(string path, StepEditorValidationContext context, List<StepSettingError> errors)
    {
        try { DbcVariableBinding.ValidateJsonTarget(path, context.ExecutionContext); }
        catch (ArgumentException ex) { errors.Add(StepSettingError.Error("CAN_BACKGROUND_TARGET", $"输出变量 {path} 必须是已声明的字符串完整路径：{ex.Message}")); }
    }
    private static void CheckLiteralRoute(CanReceiveMapping mapping,
        List<(uint Id, CanFrameType Type, CanReceiveFrameFormat Format)> routes, List<StepSettingError> errors)
    {
        // 仅解析字符串字面量，不运行用户表达式；动态 ID 在启动时再次校验。
        string expression = mapping.CanId?.Trim() ?? "";
        if (!expression.StartsWith('"') || !expression.EndsWith('"')) return;
        try
        {
            uint id = CanHelper.ParseCanId(JsonSerializer.Deserialize<string>(expression) ?? "");
            if (id > (mapping.FrameType == CanFrameType.Standard ? 0x7FFu : 0x1FFFFFFFu))
                errors.Add(StepSettingError.Error("CAN_BACKGROUND_ID", $"ID 0x{id:X} 超出所选标准/扩展帧范围"));
            if (routes.Any(r => r.Id == id && r.Type == mapping.FrameType &&
                (r.Format == mapping.FrameFormat || r.Format == CanReceiveFrameFormat.Any || mapping.FrameFormat == CanReceiveFrameFormat.Any)))
                errors.Add(StepSettingError.Error("CAN_BACKGROUND_ROUTE_OVERLAP", $"ID 0x{id:X} 的格式筛选重复或重叠；经典 CAN 与 FD 可分开映射，Any 不能再与其中一种同时配置"));
            routes.Add((id, mapping.FrameType, mapping.FrameFormat));
        }
        catch (JsonException) { /* 可能是拼接表达式，仅交给表达式语法校验，运行时解析实际 ID。 */ }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        { errors.Add(StepSettingError.Error("CAN_BACKGROUND_ID", $"监听 ID 无效：{ex.Message}")); }
    }
}
