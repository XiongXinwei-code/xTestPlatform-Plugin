using System.Globalization;
using System.Reflection;
using System.Text;
using CiA402.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CiA402;

[AttributeUsage(AttributeTargets.Class)]
public sealed class StepDefinitionAttribute(string name, string purpose, string behavior) : Attribute
{
    public string Name { get; } = name;
    public string Purpose { get; } = purpose;
    public string Behavior { get; } = behavior;
}

public interface ICia402Plugin : IStepPlugin
{
    Type SettingType { get; }
}

public abstract class Cia402Plugin<TSetting, TExecutor> : StepPluginBase<TSetting>, ICia402Plugin
    where TSetting : AxisSetting, new() where TExecutor : IStepExecutor, new()
{
    private StepDefinitionAttribute Definition => GetType().GetCustomAttribute<StepDefinitionAttribute>()!;
    public Type SettingType => typeof(TSetting);
    public override string StepTypeId => "Motion.CiA402" + Definition.Name;
    public override string DisplayName => "CiA402_" + Definition.Name;
    public override string Category => "Motion";
    public override string IconPath => $"pack://application:,,,/CiA402.StepPlugin.UI;component/Resources/Icons/cia402_{Definition.Name.ToLowerInvariant()}.png";
    public override IStepExecutor CreateExecutor() => new TExecutor();
    public override string GenerateDescription(byte[] setting)
    {
        var value = DeserializeSetting(setting);
        return $"{Definition.Purpose}，轴：{value.AxisName}";
    }
    public override string Description
    {
        get
        {
            var defaults = new TSetting();
            var text = new StringBuilder($"## 功能\n\n{Definition.Purpose}。\n\n## 参数\n\n| 参数 | 类型 | 必填 | 默认值 | 说明 |\n|---|---|---|---|---|\n");
            foreach (var field in SettingFields.Get(typeof(TSetting)))
            {
                var type = field.IsExpression ? "string([ExpressionField])" : field.IsOutput ? "string([VariablePathField])" : field.Property.PropertyType.Name;
                var value = Convert.ToString(field.Property.GetValue(defaults), CultureInfo.InvariantCulture);
                var backend = field.Metadata.Backend.Length > 0 ? $"；仅 {field.Metadata.Backend}" : "";
                text.AppendLine($"| {field.Property.Name} | {type} | {(field.Metadata.Required ? "是" : "否")} | {Escape(string.IsNullOrEmpty(value) ? "—" : value)} | {Escape(field.Metadata.Description + backend)} |");
            }
            text.AppendLine("\n## 行为\n");
            text.AppendLine(Definition.Behavior);
            text.AppendLine("\n- 正常执行返回 Completed；参数、通信、设备状态问题及超时返回 Error；用户取消返回 Aborted。");
            text.AppendLine("- 仅写入用户已声明并明确配置的完整变量路径，空输出路径不写入。");
            text.AppendLine("- 周期目标由测试序列循环编排，属于非实时周期控制，不保证确定性周期；不启动后台周期任务或隐式发送 SYNC。");
            text.AppendLine("- CANopen 轴占用期间不要混用同节点原始 SDO/PDO、CAN 接收或后台周期写入。关闭 CAN 前先解除轴绑定。");
            text.AppendLine("\n## 相关插件\n\n- CiA402_BindAxis、CiA402_SetMode、CiA402_Enable\n- CiA402_ReadState、CiA402_StopMotion、CiA402_Disable、CiA402_UnbindAxis\n- CAN_Open、CANopen_SyncSend");
            return text.ToString();
        }
    }
    private static string Escape(string value) => value.Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
}

public static class StepCatalog
{
    public static IReadOnlyList<ICia402Plugin> All { get; } = typeof(StepCatalog).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && t.GetCustomAttribute<StepDefinitionAttribute>() != null)
        .Select(t => (ICia402Plugin)Activator.CreateInstance(t)!).OrderBy(p => p.StepTypeId).ToArray();
    public static ICia402Plugin ForSetting(Type type) => All.Single(p => p.SettingType == type);
    public static ICia402Plugin ForId(string id) => All.Single(p => p.StepTypeId == id);
}
