using Modbus.Models;

namespace Modbus.UI.ViewModels;

/// <summary>编辑器直接绑定持久化枚举值，不把显示文本或下拉索引写入协议配置。</summary>
public static class ModbusEditorChoices
{
    public static IReadOnlyList<ModbusRegisterType> RegisterTypes { get; } = Enum.GetValues<ModbusRegisterType>();
    public static IReadOnlyList<ModbusRegisterType> WritableRegisterTypes { get; } = [ModbusRegisterType.Coil, ModbusRegisterType.HoldingRegister];
    public static IReadOnlyList<ModbusDataFormat> DataFormats { get; } = Enum.GetValues<ModbusDataFormat>();
}
