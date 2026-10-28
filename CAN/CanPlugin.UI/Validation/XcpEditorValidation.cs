using CAN.XCP.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UI.Validation;

internal static class XcpEditorValidation
{
    internal static void Check(XcpCommonSetting setting, StepEditorValidationContext context, List<StepSettingError> errors)
    {
        Expression(nameof(setting.ConnectionName), setting.ConnectionName);
        Expression(nameof(setting.TxId), setting.TxId);
        Expression(nameof(setting.RxId), setting.RxId);
        if (setting.TimeoutMs <= 0) Error("TimeoutMs", "事务总超时必须大于 0");
        switch (setting)
        {
            case XcpConnectSetting connect:
                if (!Enum.IsDefined(connect.ConnectMode)) Error("ConnectMode", "连接模式无效");
                Output(connect.ResourceVariable, typeof(byte));
                break;
            case XcpShortUploadSetting upload:
                Expression(nameof(upload.Address), upload.Address);
                if (!Enum.IsDefined(upload.AddressExtension) || !Enum.IsDefined(upload.ByteOrder)) Error("Format", "地址扩展或字节序无效");
                Output(upload.ResultVariable, typeof(string));
                break;
            case XcpShortDownloadSetting download:
                Expression(nameof(download.Address), download.Address);
                Expression(nameof(download.Data), download.Data);
                if (!Enum.IsDefined(download.AddressExtension) || !Enum.IsDefined(download.ByteOrder)) Error("Format", "地址扩展或字节序无效");
                break;
        }
        void Error(string field, string message) => errors.Add(StepSettingError.Error("XCP_INPUT_" + field, message));
        void Expression(string field, string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) Error(field, field + " 不能为空");
            else if (!context.Evaluator.ValidateExpression(expression, context.ExecutionContext, out var reason)) Error(field, $"{field} 表达式无效：{reason}");
        }
        void Output(string path, Type expected)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!context.ExecutionContext.HasVariable(path)) Error("Output", $"变量 {path} 不存在，请先声明 {expected.Name} 变量");
            else if (context.ExecutionContext.GetVariable(path) is { } value && value.GetType() != expected)
                Error("Output", $"变量 {path} 类型应为 {expected.Name}，实际为 {value.GetType().Name}");
        }
    }
}
