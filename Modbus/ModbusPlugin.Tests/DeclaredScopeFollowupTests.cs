using Modbus.Helpers;
using Modbus.Models;
using Xunit;

namespace Modbus.ContractTests;

public sealed class DeclaredScopeFollowupTests
{
    [Theory]
    [InlineData("Globals.value")]
    [InlineData("CustomScope.value")]
    public void Scoped_path_uses_context_declaration_authority_instead_of_a_plugin_root_whitelist(string path)
    {
        var (context, state) = PublicationContextProxy.Create(new ModbusReadPlugin(), new ModbusReadSetting());
        state.Variables = new() { [path] = (ushort)0 };
        Assert.Null(Record.Exception(() => ModbusRequestPreflight.ValidateDestination(context, path, ModbusRegisterType.HoldingRegister, ModbusDataFormat.UInt16, 1, false)));
        state.Variables.Clear();
        Assert.Throws<ArgumentException>(() => ModbusRequestPreflight.ValidateDestination(context, path, ModbusRegisterType.HoldingRegister, ModbusDataFormat.UInt16, 1, false));
        Assert.Throws<ArgumentException>(() => ModbusRequestPreflight.ValidateDestination(context, "value", ModbusRegisterType.HoldingRegister, ModbusDataFormat.UInt16, 1, false));
    }
}
