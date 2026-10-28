using System.Reflection;
using SerialPort.Helpers;
using xTestPlatform.Core.Engine;
using Xunit;

namespace SerialPort.Tests;

public sealed class DeclaredScopeFollowupTests
{
    [Theory]
    [InlineData("Globals.value")]
    [InlineData("CustomScope.value")]
    public void Scoped_path_uses_context_declaration_authority_instead_of_a_plugin_root_whitelist(string path)
    {
        var context = DispatchProxy.Create<IExecutionContext, DeclarationContext>();
        var state = (DeclarationContext)(object)context; state.Path = path;
        Assert.Null(Record.Exception(() => SerialPortHelper.ValidateStringOutput(context, path)));
        state.Declared = false;
        Assert.Throws<ArgumentException>(() => SerialPortHelper.ValidateStringOutput(context, path));
        state.Declared = true;
        Assert.Throws<ArgumentException>(() => SerialPortHelper.ValidateStringOutput(context, "value"));
    }
    public class DeclarationContext : DispatchProxy
    {
        internal string Path = "";
        internal bool Declared = true;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        { "HasVariable" => Declared && Equals(args![0], Path), "GetVariable" => "", _ => null };
    }
}
