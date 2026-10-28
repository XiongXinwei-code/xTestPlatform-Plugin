using System.Globalization;
using Modbus.Helpers;
using Modbus.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus.UI.Validation;

/// <summary>只预检静态可确定的配置；运行期表达式仍由执行器在发送前验证。</summary>
internal static class ModbusRequestValidator
{
    internal static void Read(ModbusRegisterType register, ModbusDataFormat format, ushort address, int count, List<StepSettingError> errors, string prefix = "")
    {
        try { ModbusRequestPreflight.ValidateRead(register, format, address, count); }
        catch (ArgumentException ex) { errors.Add(StepSettingError.Error("MB_READ_CONFIG", prefix + ex.Message)); }
    }

    internal static ushort? ReadConstants(ModbusReadSetting setting, List<StepSettingError> errors)
    {
        bool addressKnown = TryInteger(setting.StartAddress, out long address);
        bool countKnown = TryInteger(setting.Quantity, out long count);
        if (addressKnown && (address < 0 || address > ushort.MaxValue) || countKnown && (count < 0 || count > ushort.MaxValue))
        { errors.Add(StepSettingError.Error("MB_READ_CONFIG", "起始地址及读取数量必须在 0～65535 内；数量还受对应功能码上限限制")); return null; }
        if (!countKnown) return null;
        Read(setting.RegisterType, setting.DataFormat, addressKnown ? (ushort)address : (ushort)0, (int)count, errors);
        return (ushort)count;
    }

    internal static void Write(ModbusRegisterType register, ModbusDataFormat format, ushort address, string values, List<StepSettingError> errors, string prefix = "")
    {
        try
        {
            int count = register == ModbusRegisterType.Coil ? ModbusDataConverter.ParseCoils(values).Length :
                ModbusDataConverter.ConvertToRegisters(values, format).Length;
            ModbusRequestPreflight.ValidateWrite(register, address, count);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        { errors.Add(StepSettingError.Error("MB_WRITE_CONFIG", prefix + ex.Message)); }
    }

    internal static void WriteConstants(ModbusWriteSetting setting, List<StepSettingError> errors)
    {
        if (!TryInteger(setting.StartAddress, out long address)) return;
        if (address < 0 || address > ushort.MaxValue)
        { errors.Add(StepSettingError.Error("MB_WRITE_CONFIG", "起始地址必须在 0～65535 内")); return; }
        string text = setting.Values?.Trim() ?? string.Empty;
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"' && !text[1..^1].Contains('"'))
            Write(setting.RegisterType, setting.DataFormat, (ushort)address, text[1..^1], errors);
        else if (TryInteger(text, out _))
            Write(setting.RegisterType, setting.DataFormat, (ushort)address, text, errors);
    }

    private static bool TryInteger(string expression, out long value)
    {
        string text = expression?.Trim() ?? string.Empty;
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"') text = text[1..^1];
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
