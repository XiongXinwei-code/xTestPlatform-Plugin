using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace CAN.Tests;

/// <summary>验证插件提供的参数契约；不等同于实际宿主助手工具的端到端验证。</summary>
public sealed class CanParameterContractTests
{
    public static IEnumerable<object[]> Plugins() => ExecutorSettingsContractTests.PluginInventory();

    [Fact]
    public void Inventory_covers_all_registered_CAN_family_steps() => Assert.Equal(44, Plugins().Count());

    [Theory]
    [MemberData(nameof(Plugins))]
    public void Every_setting_tree_is_round_trippable_and_exposes_named_enums_and_complete_complex_text(string pluginType)
    {
        var plugin = (IStepPlugin)Activator.CreateInstance(typeof(CanOpenPlugin).Assembly.GetType(pluginType)!)!;
        var serializer = plugin.CreateSerializer();
        object settings = serializer.CreateDefault();
        var options = new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        string before = JsonSerializer.Serialize(settings, settings.GetType(), options);
        var restored = JsonSerializer.Deserialize(before, settings.GetType(), options)!;
        Assert.Equal(before, JsonSerializer.Serialize(restored, settings.GetType(), options));
        var stored = serializer.Deserialize(serializer.Serialize(restored), serializer.SettingVersion);
        Assert.Equal(before, JsonSerializer.Serialize(stored, settings.GetType(), options));
        foreach (var property in settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (property.CanRead && property.GetCustomAttribute<JsonIgnoreAttribute>() == null &&
                property.GetCustomAttribute<IgnoreMemberAttribute>() == null)
                Assert.Contains(property.Name, plugin.Description);
        VerifyType(settings.GetType(), plugin.StepTypeId, new HashSet<Type>(), true);
    }

    private static void VerifyType(Type type, string path, HashSet<Type> visited, bool root = false)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsEnum)
        {
            Assert.True(type.GetCustomAttribute<JsonConverterAttribute>() != null, $"{path}: 枚举缺少字符串 JSON 转换器");
            foreach (object value in Enum.GetValues(type))
            {
                string json = JsonSerializer.Serialize(value, type);
                Assert.StartsWith("\"", json);
                Assert.Equal(value, JsonSerializer.Deserialize(json, type));
            }
            return;
        }
        if (type == typeof(string) || type.IsPrimitive || type == typeof(decimal)) return;
        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            VerifyType(type.IsArray ? type.GetElementType()! : type.GetGenericArguments().Single(), path + "[]", visited);
            return;
        }
        if (!visited.Add(type)) return;
        if (!root)
        {
            object value = Activator.CreateInstance(type)!;
            Assert.NotEqual(typeof(object), type.GetMethod(nameof(ToString), Type.EmptyTypes)!.DeclaringType);
            using var text = JsonDocument.Parse(value.ToString()!);
            Assert.Equal(JsonValueKind.Object, text.RootElement.ValueKind);
            Assert.Equal(JsonSerializer.Serialize(value, type), text.RootElement.GetRawText());
        }
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
            if (property.GetCustomAttribute<IgnoreMemberAttribute>() != null)
            {
                Assert.NotNull(property.GetCustomAttribute<JsonIgnoreAttribute>());
                continue;
            }
            if (property.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
            var expression = property.GetCustomAttribute<ExpressionFieldAttribute>();
            var variable = property.GetCustomAttribute<VariablePathFieldAttribute>();
            Assert.False(expression != null && variable != null, $"{path}.{property.Name}: 表达式与变量路径标记互斥");
            if (expression != null || variable != null) Assert.Equal(typeof(string), property.PropertyType);
            Assert.True(property.CanWrite, $"{path}.{property.Name}: 设置参数需可写或显式忽略");
            VerifyType(property.PropertyType, path + "." + property.Name, visited);
        }
    }

    [Fact]
    public void Shared_stop_documentation_names_both_start_steps_and_matching_task_identity()
    {
        var stop = new CanCyclicSendStopPlugin();
        var dbc = new CAN.Dbc.DbcCyclicWritePlugin();
        string description = stop.Description;
        Assert.Contains(new CanCyclicSendStartPlugin().StepTypeId, description);
        Assert.Contains(dbc.StepTypeId, description.Split("## 参数")[0]);
        Assert.Contains(stop.StepTypeId, dbc.Description.Split("## 参数")[0]);
        Assert.Contains("ConnectionName + TaskName", description);
        foreach (string help in new[] { description, dbc.Description })
        {
            Assert.Contains("CAN > Cyclic > SendStop", help);
            Assert.Contains(new CAN.Dbc.DbcCyclicWriteSetting().TaskName, help);
            Assert.Contains(new CAN.Models.CanCyclicSendStopSetting().TaskName, help);
        }
        Assert.Contains("不代表硬件队列已排空或电机已停止", description);
    }
}
