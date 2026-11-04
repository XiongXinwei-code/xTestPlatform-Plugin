using System.Collections;
using System.IO;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

// Offline contract check: instantiate only plugin descriptors, serializers and setting models.
// Never instantiate an executor/editor, evaluate an expression, or connect to a device.
Console.OutputEncoding = Encoding.UTF8;
if (args.Length != 1 || (!Directory.Exists(args[0]) && !File.Exists(args[0])))
{
    Console.Error.WriteLine("用法: PluginParameterInspector <单个插件产物目录或主 *.StepPlugin.dll 路径>");
    return 1;
}

var input = Path.GetFullPath(args[0]);
var directory = Directory.Exists(input) ? input : Path.GetDirectoryName(input)!;
var primary = Directory.Exists(input) ? Path.Combine(directory, new DirectoryInfo(directory).Name + ".StepPlugin.dll") : input;
if (!File.Exists(primary) && Directory.Exists(input))
{
    var candidates = Directory.GetFiles(directory, "*.StepPlugin.dll");
    if (candidates.Length == 1) primary = candidates[0];
}
if (!File.Exists(primary) || !primary.EndsWith(".StepPlugin.dll", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"无法唯一确定主插件程序集，请直接传入需要审查的 *.StepPlugin.dll 路径: {primary}");
    return 1;
}

var findings = new List<Finding>();
var steps = new List<StepContract>();
AssemblyIdentity? identity = null;
try
{
    var assembly = new PluginLoadContext(primary).LoadFromAssemblyPath(primary);
    identity = new(assembly.FullName!, assembly.ManifestModule.ModuleVersionId.ToString(),
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(primary))));
    foreach (var type in assembly.GetTypes().Where(t => !t.IsAbstract && t.IsClass &&
                 t.GetInterfaces().Any(i => i.FullName == "xTestPlatform.Core.Plugins.Contracts.IStepPlugin")).OrderBy(t => t.FullName))
    {
        string id = type.FullName!;
        try
        {
            var descriptor = Activator.CreateInstance(type)!;
            id = (string)type.GetProperty("StepTypeId")!.GetValue(descriptor)!;
            var description = (string)type.GetProperty("Description")!.GetValue(descriptor)!;
            var serializer = type.GetMethod("CreateSerializer")!.Invoke(descriptor, null)!;
            var serializerInterface = serializer.GetType().GetInterfaces().Single(i =>
                i.FullName == "xTestPlatform.Core.Plugins.Contracts.IStepSettingSerializer");
            var defaults = serializerInterface.GetMethod("CreateDefault")!.Invoke(serializer, null)!;
            var inspector = new Inspector(id, description, findings);
            var properties = inspector.Inspect(defaults);
            inspector.CheckRoundTrip(defaults, serializer, serializerInterface, "默认值");
            var populated = inspector.Populate(Activator.CreateInstance(defaults.GetType())!, 1);
            inspector.CheckRoundTrip(populated, serializer, serializerInterface, "全部可写字段及三行集合");
            inspector.CheckTextValues(populated, "$", false);
            steps.Add(new(id, defaults.GetType().FullName!, properties));
        }
        catch (Exception ex)
        {
            findings.Add(new(id, "$", "Error", ex.GetBaseException().Message));
        }
    }
}
catch (Exception ex)
{
    findings.Add(new("Assembly", "$", "Error", ex.ToString()));
}
if (steps.Count == 0) findings.Add(new("Assembly", "$", "Error", "未发现可审查步骤"));
var report = new { PluginDirectory = directory, AssemblyPath = primary, Assembly = identity, Steps = steps, Findings = findings,
    Checks = new[] { "公开读写字段树与特性", "枚举字符串契约", "默认/非默认字段 JSON 与 SDK 存储往返", "集合项完整 JSON 与通知/存储无副作用", "Description 参数覆盖" },
    Limitations = new[] { "未调用平台助手或编辑页面", "未执行表达式、步骤或真实设备", "不能证明平台工具自身的分页/截断和变量类型转换行为" } };
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
return findings.Any(f => f.Severity == "Error") ? 2 : 0;

internal sealed class Inspector(string step, string description, List<Finding> findings)
{
    private readonly JsonSerializerOptions _json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private readonly HashSet<Type> _checkedTypes = [];

    internal List<ParameterContract> Inspect(object defaults)
    {
        if (!description.Contains("## 功能") || !description.Contains("## 参数"))
            Error("$", "Description 缺少手册规定的功能/参数章节");
        return InspectType(defaults.GetType(), defaults, "", 0);
    }

    private List<ParameterContract> InspectType(Type type, object? value, string prefix, int depth)
    {
        if (depth > 10) throw new InvalidOperationException($"设置树超过 10 层: {prefix}");
        var result = new List<ParameterContract>();
        foreach (var ignored in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 &&
                         p.CustomAttributes.Any(a => a.AttributeType.Name is "JsonIgnoreAttribute" or "IgnoreMemberAttribute")))
            Error(prefix + ignored.Name, "宿主 schema 会显示公开读写的忽略字段，但 JSON/SDK 写入无法保存；请移除 UI 别名并直接绑定真实字段");
        foreach (var property in Fields(type))
        {
            string path = prefix + property.Name;
            Type effective = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            string[] attributes = property.CustomAttributes.Select(a => a.AttributeType.Name).ToArray();
            bool expression = attributes.Contains("ExpressionFieldAttribute");
            bool variable = attributes.Contains("VariablePathFieldAttribute");
            if (!property.CanRead || !property.CanWrite || property.GetMethod?.IsPublic != true || property.SetMethod?.IsPublic != true)
                Error(path, "参数不是 public get/set；UI 别名应同时忽略 JSON 与 SDK 存储");
            if ((expression || variable) && effective != typeof(string)) Error(path, "表达式/变量路径特性只能用于 string");
            if (expression && variable) Error(path, "同一参数不能同时是表达式与变量路径");
            if (attributes.Contains("IgnoreMemberAttribute")) Error(path, "SDK 忽略的 UI 属性仍被 JSON 暴露");
            if (effective.IsEnum && _checkedTypes.Add(effective) && !effective.CustomAttributes.Any(a =>
                    a.AttributeType.Name == "JsonConverterAttribute" && a.ConstructorArguments.Any(c =>
                        c.Value is Type t && t.Name.StartsWith("JsonStringEnumConverter", StringComparison.Ordinal))))
                Error(path, "枚举缺少 JsonStringEnumConverter");
            bool documented = description.Contains(property.Name, StringComparison.Ordinal);
            if (!documented) findings.Add(new(step, path, "Warning", "Description 未逐名说明该参数；需人工核对集合说明是否充分"));
            object? member = value is null ? null : property.GetValue(value);
            var children = new List<ParameterContract>();
            var element = Element(effective);
            if (element is not null && !Scalar(element))
            {
                if (!typeof(INotifyPropertyChanged).IsAssignableFrom(element))
                    findings.Add(new(step, path, "Warning", "存储集合项缺少 INotifyPropertyChanged；须人工核对 UI 是否使用有通知的投影行、逐项保存及退出前 Flush（手册 §11.5.1 允许转发模式，但 §12.1 强制模型通知且禁止包装，两处条文不一致）"));
                if (!element.CustomAttributes.Any(a => a.AttributeType.Name == "MessagePackObjectAttribute")) Error(path, "自定义集合项缺少 MessagePackObject");
                children = InspectType(element, Activator.CreateInstance(element), path + "[].", depth + 1);
            }
            else if (!Scalar(effective) && element is null)
                children = InspectType(effective, member, path + ".", depth + 1);
            result.Add(new(path, property.PropertyType.FullName!, expression ? "Expression" : variable ? "VariablePath" : "Literal",
                member is null ? null : JsonSerializer.SerializeToNode(member, property.PropertyType, _json),
                effective.IsEnum ? Enum.GetNames(effective) : [], documented, children));
        }
        return result;
    }

    internal object Populate(object value, int seed, int depth = 0)
    {
        if (depth > 10) throw new InvalidOperationException("设置对象递归过深");
        foreach (var field in Fields(value.GetType()).Where(p => p.SetMethod?.IsPublic == true))
        {
            Type type = Nullable.GetUnderlyingType(field.PropertyType) ?? field.PropertyType;
            object? replacement;
            if (type == typeof(string)) replacement = "Locals.参数_" + seed;
            else if (type.IsEnum) replacement = Enum.GetValues(type).GetValue(seed % Enum.GetValues(type).Length);
            else if (type == typeof(bool)) replacement = seed % 2 == 1;
            else if (type.IsPrimitive || type == typeof(decimal)) replacement = Convert.ChangeType(seed + 1, type, System.Globalization.CultureInfo.InvariantCulture);
            else if (Element(type) is Type element)
            {
                var entries = Array.CreateInstance(element, 3);
                for (int i = 0; i < entries.Length; i++) entries.SetValue(Sample(element, seed + i + 1, depth + 1), i);
                if (type.IsArray) replacement = entries;
                else
                {
                    var list = (IList)Activator.CreateInstance(type)!;
                    foreach (var entry in entries) list.Add(entry);
                    replacement = list;
                }
            }
            else replacement = Populate(Activator.CreateInstance(type)!, seed + 1, depth + 1);
            field.SetValue(value, replacement);
            seed++;
        }
        return value;
    }

    private object Sample(Type type, int seed, int depth)
    {
        if (type == typeof(string)) return "Locals.值_" + seed;
        if (type.IsEnum) return Enum.GetValues(type).GetValue(seed % Enum.GetValues(type).Length)!;
        if (type == typeof(bool)) return seed % 2 == 1;
        if (type.IsPrimitive || type == typeof(decimal)) return Convert.ChangeType(seed + 1, type, System.Globalization.CultureInfo.InvariantCulture);
        return Populate(Activator.CreateInstance(type)!, seed, depth);
    }

    internal void CheckRoundTrip(object original, object serializer, Type contract, string scenario)
    {
        var type = original.GetType();
        var expected = JsonSerializer.SerializeToNode(original, type, _json)!;
        var jsonCopy = JsonSerializer.Deserialize(expected.ToJsonString(), type, _json)!;
        Equal(expected, JsonSerializer.SerializeToNode(jsonCopy, type, _json), "$", scenario + " JSON 往返丢字段");
        var serialize = contract.GetMethod("Serialize")!;
        var version = contract.GetProperty("SettingVersion")!.GetValue(serializer)!;
        var payload = (byte[])serialize.Invoke(serializer, [jsonCopy])!;
        var restored = contract.GetMethod("Deserialize")!.Invoke(serializer, [payload, version])!;
        Equal(expected, JsonSerializer.SerializeToNode(restored, type, _json), "$", scenario + " SDK 存储往返丢字段");
        CheckTextValues(restored, "$", false);
        if (!payload.SequenceEqual((byte[])serialize.Invoke(serializer, [restored])!))
            Error("$", scenario + " 读取参数文本后 SDK 存储变化");
        var unknown = (JsonObject)expected.DeepClone(); unknown["__unknown_parameter"] = true;
        try { _ = JsonSerializer.Deserialize(unknown.ToJsonString(), type, _json); Error("$", "严格 JSON 接受未知参数"); }
        catch (JsonException) { }
    }

    internal void CheckTextValues(object value, string path, bool item)
    {
        var type = value.GetType();
        if (Scalar(type)) return;
        if (value is IEnumerable collection)
        {
            int index = 0;
            foreach (object? entry in collection) if (entry is not null) CheckTextValues(entry, path + "[" + index++ + "]", true);
            return;
        }
        if (item)
        {
            int changes = 0;
            PropertyChangedEventHandler handler = (_, _) => changes++;
            if (value is INotifyPropertyChanged notifications) notifications.PropertyChanged += handler;
            try
            {
                var expected = JsonSerializer.SerializeToNode(value, type, _json)!;
                var text = value.ToString()!;
                Equal(expected, JsonNode.Parse(text), path, "集合项 ToString 未完整保留实际字段");
                var restored = JsonSerializer.Deserialize(text, type, _json)!;
                Equal(expected, JsonSerializer.SerializeToNode(restored, type, _json), path, "集合项文本不能原样写回");
                Equal(expected, JsonSerializer.SerializeToNode(value, type, _json), path, "集合项 ToString 修改了设置");
                if (changes != 0) Error(path, "读取集合项文本触发了设置变更通知");
            }
            catch (JsonException ex) { Error(path, "集合项 ToString 不是完整 JSON: " + ex.Message); }
            finally { if (value is INotifyPropertyChanged notifications2) notifications2.PropertyChanged -= handler; }
        }
        foreach (var property in Fields(type))
            if (property.GetValue(value) is object child) CheckTextValues(child, path + "." + property.Name, false);
    }

    private void Equal(JsonNode expected, JsonNode? actual, string path, string message)
    {
        if (!JsonNode.DeepEquals(expected, actual)) Error(path, message);
    }
    private void Error(string path, string message) => findings.Add(new(step, path, "Error", message));
    private static IEnumerable<PropertyInfo> Fields(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.GetIndexParameters().Length == 0 && !p.CustomAttributes.Any(a => a.AttributeType.Name == "JsonIgnoreAttribute"));
    private static Type? Element(Type type) => type.IsArray ? type.GetElementType() : type.IsGenericType &&
        typeof(IEnumerable).IsAssignableFrom(type) && type.GetGenericArguments().Length == 1 ? type.GetGenericArguments()[0] : null;
    private static bool Scalar(Type type) => type == typeof(string) || type.IsPrimitive || type.IsEnum ||
        type == typeof(decimal) || type == typeof(DateTime) || type == typeof(Guid) || Nullable.GetUnderlyingType(type) is Type inner && Scalar(inner);
}

internal sealed record Finding(string Step, string Path, string Severity, string Message);
internal sealed record AssemblyIdentity(string FullName, string Mvid, string Sha256);
internal sealed record ParameterContract(string Path, string Type, string Kind, JsonNode? Default,
    string[] EnumValues, bool MentionedInDescription, List<ParameterContract> Children);
internal sealed record StepContract(string StepTypeId, string SettingType, List<ParameterContract> Parameters);

internal sealed class PluginLoadContext(string mainAssemblyPath) : AssemblyLoadContext(isCollectible: false)
{
    private readonly AssemblyDependencyResolver _resolver = new(mainAssemblyPath);
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Use the shared BCL Json/ComponentModel types so reflection and attribute converters
        // behave exactly as JsonSerializer in this process; plugin SDK stays privately resolved.
        if (assemblyName.Name is "System.Text.Json" or "System.ComponentModel") return null;
        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is null)
        {
            var candidate = Path.Combine(Path.GetDirectoryName(mainAssemblyPath)!, assemblyName.Name + ".dll");
            if (File.Exists(candidate)) path = candidate;
        }
        return path is null ? null : LoadFromAssemblyPath(path);
    }
}
