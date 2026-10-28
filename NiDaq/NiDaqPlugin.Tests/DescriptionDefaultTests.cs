using System.Text.RegularExpressions;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class DescriptionDefaultTests
{
    [Fact]
    public void NumericDescriptionDefaultsMatchEveryCurrentSetting()
    {
        var failures = new List<string>();
        foreach (var type in typeof(NiDaqAiConfigPlugin).Assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IStepPlugin).IsAssignableFrom(type)))
        {
            var plugin = (IStepPlugin)Activator.CreateInstance(type)!;
            var setting = plugin.CreateSerializer().CreateDefault();
            foreach (var property in setting.GetType().GetProperties().Where(property => property.PropertyType == typeof(int)))
            {
                var match = Regex.Match(plugin.Description, @"\|\s*" + Regex.Escape(property.Name) + @"\s*\|[^|]+\|[^|]+\|\s*(-?\d+)\s*\|");
                if (match.Success && int.Parse(match.Groups[1].Value) != (int)property.GetValue(setting)!)
                    failures.Add($"{plugin.StepTypeId}.{property.Name}: Description={match.Groups[1].Value}, Setting={property.GetValue(setting)}");
            }
        }
        Assert.Empty(failures);
    }
}
