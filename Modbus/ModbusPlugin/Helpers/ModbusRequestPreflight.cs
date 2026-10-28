using Modbus.Models;
using xTestPlatform.Core.Engine;

namespace Modbus.Helpers;

/// <summary>发送前可确定的本地合同；不以设备错误代替配置检查，也不回滚已完成的通信。</summary>
internal static class ModbusRequestPreflight
{
    internal static void ValidateRead(ModbusRegisterType register, ModbusDataFormat format, ushort address, int count)
    {
        if (!Enum.IsDefined(register)) throw new ArgumentException("不支持的 Modbus 寄存器类型");
        bool bits = register is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput;
        ValidateRange(address, count, bits ? 2000 : 125, "读取");
        if (!bits)
        {
            _ = ElementType(register, format);
            if (format is not (ModbusDataFormat.UInt16 or ModbusDataFormat.Int16) && count % 2 != 0)
                throw new ArgumentException("32 位数据必须配置偶数个寄存器；请求尚未发送");
        }
    }

    internal static void ValidateWrite(ModbusRegisterType register, ushort address, int count)
    {
        if (register is not (ModbusRegisterType.Coil or ModbusRegisterType.HoldingRegister))
            throw new ArgumentException("Modbus 写入仅支持 Coil / HoldingRegister");
        ValidateRange(address, count, register == ModbusRegisterType.Coil ? 1968 : 123, "写入");
    }

    private static void ValidateRange(ushort address, int count, int maximum, string operation)
    {
        // Modbus Application Protocol V1.1b3 §§6.1–6.4, 6.11–6.12；数量指位/寄存器，不是转换后的值数。
        if (count < 1 || count > maximum)
            throw new ArgumentException($"Modbus {operation}数量必须为 1～{maximum}，实际 {count}；请求尚未发送");
        if ((long)address + count > 65536)
            throw new ArgumentException($"Modbus {operation}范围越过地址 65535：起始 {address}，数量 {count}；请求尚未发送");
    }

    internal static Type ElementType(ModbusRegisterType register, ModbusDataFormat format)
    {
        if (register is ModbusRegisterType.Coil or ModbusRegisterType.DiscreteInput) return typeof(bool);
        return format switch
        {
            ModbusDataFormat.UInt16 => typeof(ushort),
            ModbusDataFormat.Int16 => typeof(short),
            ModbusDataFormat.UInt32_AB_CD or ModbusDataFormat.UInt32_CD_AB => typeof(uint),
            ModbusDataFormat.Int32_AB_CD or ModbusDataFormat.Int32_CD_AB => typeof(int),
            ModbusDataFormat.Float_AB_CD or ModbusDataFormat.Float_CD_AB => typeof(float),
            _ => throw new ArgumentException("不支持的 Modbus 数据格式")
        };
    }

    internal static bool IsScopedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var parts = path.Split('.', 2);
        return parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]) && parts[0].Length > 0 &&
            (char.IsLetter(parts[0][0]) || parts[0][0] == '_') && parts[0].All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    internal static void ValidateDestination(IExecutionContext context, string path, ModbusRegisterType register,
        ModbusDataFormat format, int count, bool optional, Action? ensureActive = null)
    {
        ensureActive?.Invoke();
        if (optional && string.IsNullOrWhiteSpace(path)) return;
        if (!IsScopedPath(path)) throw new ArgumentException($"Modbus 输出必须为带作用域的完整变量路径：{path}");
        bool declared = context.HasVariable(path);
        ensureActive?.Invoke();
        if (!declared) throw new ArgumentException($"Modbus 输出变量 {path} 未声明或路径不存在");
        var value = context.GetVariable(path);
        ensureActive?.Invoke();
        if (value == null) return; // SDK 未提供该路径的已知值，不猜测 null 的声明类型。
        bool pairs = register is ModbusRegisterType.HoldingRegister or ModbusRegisterType.InputRegister &&
            format is not (ModbusDataFormat.UInt16 or ModbusDataFormat.Int16);
        bool array = (pairs ? count / 2 : count) != 1;
        Type expected = ElementType(register, format);
        if (array) expected = expected.MakeArrayType();
        if (value.GetType() != expected)
            throw new ArgumentException($"Modbus 输出变量 {path} 类型/形状不匹配，需要 {expected.Name}，实际 {value.GetType().Name}；请求尚未发送");
    }
}
