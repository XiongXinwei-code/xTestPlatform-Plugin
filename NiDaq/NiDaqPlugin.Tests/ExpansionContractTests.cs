using System.Reflection;
using NiDaq.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class ExpansionContractTests
{
    [Theory]
    [InlineData("CounterInputConfig")]
    [InlineData("CounterRead")]
    [InlineData("CounterOutputConfig")]
    [InlineData("DiConfig")]
    [InlineData("DoConfig")]
    [InlineData("DigitalRead")]
    [InlineData("DigitalWrite")]
    public void New_capabilities_have_serializable_steps_and_independent_editors(string name)
    {
        var type = typeof(NiDaqAiConfigPlugin).Assembly.GetType("NiDaq.NiDaq" + name + "Plugin");
        Assert.NotNull(type);
        var plugin = (IStepPlugin)Activator.CreateInstance(type)!;
        var serializer = plugin.CreateSerializer();
        var setting = serializer.CreateDefault();
        Assert.Equal(setting.GetType(), serializer.Deserialize(serializer.Serialize(setting), serializer.SettingVersion).GetType());
        var editors = Assembly.Load("NiDaq.StepPlugin.UI").GetTypes().Where(t => !t.IsAbstract && typeof(IStepEditorPlugin).IsAssignableFrom(t));
        Assert.Single(editors.Select(t => (IStepEditorPlugin)Activator.CreateInstance(t)!), e => e.StepTypeId == plugin.StepTypeId);
    }
}
