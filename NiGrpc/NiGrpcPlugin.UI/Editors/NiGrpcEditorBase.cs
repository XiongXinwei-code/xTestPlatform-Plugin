using System.Windows;
using NiGrpc.Runtime;
using NiGrpc.UI.Views;
using NiGrpc.UI.Validation;
using StepEditor.Abstractions;
using System.Text.Json;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiGrpc.UI.Editors;

public abstract class NiGrpcEditorBase : IStepEditorPlugin
{
    protected abstract INiGrpcPlugin Plugin { get; }
    public string StepTypeId => Plugin.StepTypeId;
    public string IconPath => Plugin.IconPath;
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var plugin = Plugin;
        var view = new NiGrpcEditorView(plugin) { SequenceFile = sequenceFile };
        view.RefreshFromStep(step);
        PluginCommon.UI.PluginEditorScrolling.SetEnabled(view, true);
        return view;
    }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var errors = new List<StepSettingError>();
        try
        {
            var setting = (NiGrpcSetting)Plugin.CreateSerializer().Deserialize(context.Setting, 1);
            if (setting.TimeoutMs <= 0) errors.Add(StepSettingError.Error("NIGRPC_01", "总期限必须大于 0 毫秒"));
            Check(setting.ConnectionName, "连接名称");
            if (setting is NiGrpcConnectSetting connect) Check(connect.Endpoint, "服务地址");
            bool isOpen = StepTypeId is "NiGrpc.DaqOpen" or "NiGrpc.XnetOpen";
            bool isInvoke = StepTypeId.StartsWith("NiGrpc.Daq", StringComparison.Ordinal) || StepTypeId.StartsWith("NiGrpc.Xnet", StringComparison.Ordinal);
            isInvoke &= !isOpen;
            if (setting is NiGrpcSessionSetting session) Check(session.SessionName, "会话别名");
            if (setting is NiGrpcXnetOpenSetting xnet)
            {
                Check(xnet.ConfigurationJson, "创建配置 JSON");
            }
            if (setting is NiGrpcRequestSetting request)
            {
                Check(request.RequestJson, "请求 JSON");
                // 常量请求可以在编辑阶段与执行阶段共享协议校验；变量/复杂表达式只作语法校验，运行时再校验实际值。
                if (TryLiteral(request.RequestJson, out var json))
                {
                    try { Plugin.ValidateRequestJson(json); }
                    catch (Exception ex) { errors.Add(StepSettingError.Error("NIGRPC_08", $"DAQmx 请求参数无效：{ex.Message}")); }
                }
                if (!string.IsNullOrWhiteSpace(request.ResultVariable) && !context.ExecutionContext.HasVariable(request.ResultVariable))
                    errors.Add(StepSettingError.Error("NIGRPC_03", $"响应变量 {request.ResultVariable} 不存在"));
                else if (!string.IsNullOrWhiteSpace(request.ResultVariable) && context.ExecutionContext.GetVariable(request.ResultVariable) is { } value && value is not string)
                    errors.Add(StepSettingError.Error("NIGRPC_07", $"响应变量 {request.ResultVariable} 必须能够接收字符串，当前值类型为 {value.GetType().Name}"));
            }
            NiGrpcLifecycleValidator.Check(context.SequenceFile, context.Block, context.CurrentStep, setting, StepTypeId, errors);
            void Check(string expression, string label)
            {
                if (string.IsNullOrWhiteSpace(expression)) errors.Add(StepSettingError.Error("NIGRPC_04", $"{label}不能为空"));
                else if (!context.Evaluator.ValidateExpression(expression, context.ExecutionContext, out var error))
                    errors.Add(StepSettingError.Error("NIGRPC_05", $"{label}表达式无效：{error}"));
            }
        }
        catch (Exception ex) { errors.Add(StepSettingError.Error("NIGRPC_06", $"设置校验失败：{ex.Message}")); }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }

    private static bool TryLiteral(string expression, out string value)
    {
        value = "";
        var text = expression.Trim();
        if (System.Text.RegularExpressions.Regex.IsMatch(text, "^@\"(?:[^\"]|\"\")*\"$"))
        { value = text[2..^1].Replace("\"\"", "\""); return true; }
        if (!text.StartsWith('"') || !text.EndsWith('"')) return false;
        try { value = JsonSerializer.Deserialize<string>(text) ?? ""; return true; }
        catch (JsonException) { return false; }
    }
}
