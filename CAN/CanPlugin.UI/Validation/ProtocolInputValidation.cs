using System.Globalization;
using CAN.Flash.Models;
using CAN.UDS.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.UI.Validation;

internal static class ProtocolInputValidation
{
    internal static void Check(object setting, StepEditorValidationContext context, List<StepSettingError> errors)
    {
        switch (setting)
        {
            case UdsReadDataByIdSetting read: Range(read.Did, ushort.MaxValue, "Did"); break;
            case UdsWriteDataByIdSetting write: Range(write.Did, ushort.MaxValue, "Did"); break;
            case UdsDiagSessionSetting session:
                if (!Enum.IsDefined(session.SessionType)) errors.Add(StepSettingError.Error("CAN_VALUE_RANGE", "未知的诊断会话类型"));
                Expression(session.TxId, "TxId"); Expression(session.RxId, "RxId");
                break;
            case UdsRoutineControlSetting routine:
                Range(routine.RoutineId, ushort.MaxValue, "RoutineId");
                if (!Enum.IsDefined(routine.ControlType)) errors.Add(StepSettingError.Error("CAN_VALUE_RANGE", "未知的例程控制类型"));
                if (!string.IsNullOrWhiteSpace(routine.OptionRecord)) Expression(routine.OptionRecord, "OptionRecord");
                break;
            case UdsClearDtcSetting clear: Range(clear.DtcGroup, 0xFFFFFF, "DtcGroup"); break;
            case CanFlashSetting flash:
                Range(flash.DataFormatId, byte.MaxValue, "DataFormatId");
                if (flash.EraseBeforeDownload) Range(flash.EraseRoutineId, ushort.MaxValue, "EraseRoutineId");
                if (flash.CheckMode != FlashCheckMode.None) Range(flash.CheckRoutineId, ushort.MaxValue, "CheckRoutineId");
                if (!Enum.IsDefined(flash.CheckMode)) errors.Add(StepSettingError.Error("CAN_VALUE_RANGE", "未知的校验方式"));
                break;
        }

        void Expression(string expression, string field)
        {
            if (string.IsNullOrWhiteSpace(expression) || !context.Evaluator.ValidateExpression(expression, context.ExecutionContext, out _))
                errors.Add(StepSettingError.Error("CAN_VALUE_RANGE", $"{field} 表达式不能为空且必须合法"));
        }

        void Range(string expression, uint max, string field)
        {
            if (string.IsNullOrWhiteSpace(expression) || !context.Evaluator.ValidateExpression(expression, context.ExecutionContext, out _))
            { errors.Add(StepSettingError.Error("CAN_VALUE_RANGE", $"{field} 表达式不能为空且必须合法")); return; }
            // 仅检查静态数值字面量；动态表达式仍在实际发送前求值并验证，不在编辑器执行用户代码。
            var text = expression.Trim();
            if (text.Length >= 2 && text[0] == '"' && text[^1] == '"') text = text[1..^1];
            bool hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (ulong.TryParse(hex ? text[2..] : text, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > max || text == "null")
                errors.Add(StepSettingError.Error("CAN_VALUE_RANGE", $"{field} 必须在 0~0x{max:X}，运行时禁止截断到其他目标"));
        }
    }
}
