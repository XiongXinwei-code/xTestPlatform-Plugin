using Modbus.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus.UI.Validation;

/// <summary>单项和批量读复用实际转换输出规则；不通过求值变量路径来猜目标。</summary>
internal static class ModbusVariableValidator
{
    internal static void CheckKnownOutput(IExecutionContext context, string variable, ModbusRegisterType register,
        ModbusDataFormat format, List<StepSettingError> errors, string typeErrorCode, string? shapeErrorCode = null, ushort? quantity = null)
    {
        var value = context.GetVariable(variable);
        if (value == null || !Enum.IsDefined(register) || !Enum.IsDefined(format)) return;
        bool bits = register is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput;
        Type expected = bits ? typeof(bool) : format switch
        {
            ModbusDataFormat.UInt16 => typeof(ushort),
            ModbusDataFormat.Int16 => typeof(short),
            ModbusDataFormat.UInt32_AB_CD or ModbusDataFormat.UInt32_CD_AB => typeof(uint),
            ModbusDataFormat.Int32_AB_CD or ModbusDataFormat.Int32_CD_AB => typeof(int),
            ModbusDataFormat.Float_AB_CD or ModbusDataFormat.Float_CD_AB => typeof(float),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        Type actual = value.GetType();
        Type element = actual.IsArray ? actual.GetElementType()! : actual;
        if (element != expected)
        {
            errors.Add(StepSettingError.Error(typeErrorCode, $"变量 {variable} 类型不匹配，期望 {expected.Name}，实际类型 {actual.Name}"));
            return;
        }
        if (quantity is not { } count || count == 0 || shapeErrorCode == null) return;
        bool pairs = !bits && format is not (ModbusDataFormat.UInt16 or ModbusDataFormat.Int16);
        // 不替 SDK/转换器猜测不完整 32 位数据；这里只核对可确定的标量/数组形状。
        if (pairs && count % 2 != 0) return;
        bool array = (pairs ? count / 2 : count) != 1;
        if (array != actual.IsArray || actual.IsArray && actual.GetArrayRank() != 1)
            errors.Add(StepSettingError.Error(shapeErrorCode, $"变量 {variable} 形状不匹配，本次读取输出{(array ? "一维数组" : "标量")}，实际类型 {actual.Name}"));
    }
}
