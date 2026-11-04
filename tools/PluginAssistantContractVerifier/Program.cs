using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

// Calls the installed host's pure formatter/converter/schema helpers against
// detached plugin settings. Never constructs a host, executor or live project.
Console.OutputEncoding = new UTF8Encoding(false);
bool negative = args.Contains("--negative", StringComparer.Ordinal) || args.Contains("--strict-negative", StringComparer.Ordinal);
bool strictNegative = args.Contains("--strict-negative", StringComparer.Ordinal);
bool hostIl = args.Contains("--host-il", StringComparer.Ordinal);
args = args.Where(a => a is not ("--negative" or "--strict-negative" or "--host-il")).ToArray();
if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("用法: PluginAssistantContractVerifier <插件文件夹> <xTestPlatform安装目录> [报告.json] [--negative|--strict-negative]");
    return 1;
}
string package = Path.GetFullPath(args[0]), hostDirectory = Path.GetFullPath(args[1]);
var findings = new List<Finding>(); var steps = new List<StepResult>();
object? hostIdentity = null, pluginIdentity = null;
try
{
    AssemblyLoadContext.Default.Resolving += (_, name) =>
    {
        foreach (string root in new[] { package, hostDirectory })
        {
            string file = Path.Combine(root, name.Name + ".dll");
            if (File.Exists(file)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
        }
        return null;
    };
    string hostPath = Path.Combine(hostDirectory, "xTestAgent.Platform.dll");
    var hostAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(hostPath);
    hostIdentity = Identity(hostAssembly, hostPath);
    Type hostType = hostAssembly.GetType("xTestAgent.Platform.PlatformAgentToolHost", true)!;
    if (hostIl)
    {
        string dump = HostIl.Dump(hostType);
        if (args.Length == 3) File.WriteAllText(Path.GetFullPath(args[2]), dump, new UTF8Encoding(false));
        else Console.WriteLine(dump);
        return 0;
    }
    MethodInfo HostMethod(string name) => hostType.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(hostType.FullName, name);
    var format = HostMethod("FormatValue"); var convert = HostMethod("ConvertText"); var describe = HostMethod("DescribeFields");
    string primary = Path.Combine(package, new DirectoryInfo(package).Name + ".StepPlugin.dll");
    if (!File.Exists(primary)) throw new FileNotFoundException("请传入按插件名命名的部署目录。", primary);
    var pluginAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(primary);
    pluginIdentity = Identity(pluginAssembly, primary);
    var json = new JsonSerializerOptions();
    foreach (var type in pluginAssembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i.Name == "IStepPlugin")).OrderBy(t => t.FullName))
    {
        var result = new StepResult { PluginType = type.FullName! }; steps.Add(result);
        try
        {
            object plugin = Activator.CreateInstance(type)!;
            result.StepTypeId = (string)type.GetProperty("StepTypeId")!.GetValue(plugin)!;
            object serializer = type.GetMethod("CreateSerializer")!.Invoke(plugin, null)!;
            Type serializerType = serializer.GetType().GetInterfaces().Single(i => i.Name == "IStepSettingSerializer");
            object initial = serializerType.GetMethod("CreateDefault")!.Invoke(serializer, null)!;
            var serialize = serializerType.GetMethod("Serialize")!; var deserialize = serializerType.GetMethod("Deserialize")!;
            int version = (int)serializerType.GetProperty("SettingVersion")!.GetValue(serializer)!;
            object schema = describe.Invoke(null, [initial.GetType(), 0, new HashSet<Type> { initial.GetType() }])!;
            result.Schema = JsonSerializer.SerializeToNode(schema, schema.GetType(), json);
            CheckSchema(initial.GetType(), result.Schema as JsonArray, "", result, findings, []);
            CheckIgnoredSchemaFields(initial.GetType(), "", result, findings, new HashSet<Type>());
            if (DbcBusinessProbe.Supports(result.StepTypeId))
                result.BusinessScenario = DbcBusinessProbe.Verify(result.StepTypeId, serializer, serializerType, convert, format);
            foreach (bool populated in new[] { false, true })
            {
                object source = populated ? Populate(Activator.CreateInstance(initial.GetType())!, 1) : initial;
                string scenario = populated ? "非默认值/三行集合" : "默认值";
                byte[] bytes = (byte[])serialize.Invoke(serializer, [source])!;
                string generated = (string?)type.GetMethod("GenerateDescription")!.Invoke(plugin, [bytes])
                    ?? throw new InvalidOperationException("当前设置无法生成步骤描述。");
                result.GeneratedDescriptions.Add(scenario, generated);
                // Start with a different target, so a no-op property setter cannot
                // pass simply because the target already contains the desired value.
                object target = populated ? serializerType.GetMethod("CreateDefault")!.Invoke(serializer, null)!
                    : Populate(Activator.CreateInstance(initial.GetType())!, 100);
                object clone = deserialize.Invoke(serializer, [(byte[])serialize.Invoke(serializer, [target])!, version])!;
                foreach (PropertyInfo field in Fields(source.GetType()))
                {
                    object? expected = field.GetValue(source);
                    string read = (string)format.Invoke(null, [field, source])!;
                    string? submitted = read;
                    string normalization = "无";
                    if (expected is null) { submitted = null; normalization = "宿主空值显示文本还原为 null"; }
                    else if (expected is IEnumerable collection && expected is not string)
                    {
                        Type? element = Element(field.PropertyType);
                        if (element == typeof(string) || element == typeof(char) || element == typeof(byte))
                        {
                            findings.Add(new(result.StepTypeId, field.Name, "Error", "顶层基础元素集合被宿主以无转义逗号拼接，不能保证完整读写；不伪造读取内容。"));
                            result.Parameters.Add(new(scenario, field.Name, field.PropertyType.FullName!, "不支持无损还原", read.Length, false));
                            continue;
                        }
                        submitted = "[" + read + "]";
                        normalization = "宿主集合无外层方括号：保留原项文本，仅补 []";
                    }
                    object? converted = convert.Invoke(null, [submitted, field.PropertyType]);
                    Equal(expected, converted, field.PropertyType, "宿主文本转换不一致：" + field.Name);
                    field.SetValue(clone, converted);
                    result.Parameters.Add(new(scenario, field.Name, field.PropertyType.FullName!, normalization, read.Length, true));
                }
                object stored = deserialize.Invoke(serializer, [(byte[])serialize.Invoke(serializer, [clone])!, version])!;
                Equal(source, stored, source.GetType(), scenario + "助手读写后 SDK 存储不一致");
                if (!bytes.SequenceEqual((byte[])serialize.Invoke(serializer, [source])!)) throw new InvalidOperationException("读取参数或 schema 改变了源设置。");
                if (negative && populated)
                    NegativeVerifier.Check(source, convert, serialize, serializer, result);
            }
            result.Passed = !findings.Any(f => f.StepTypeId == result.StepTypeId && f.Severity == "Error");
        }
        catch (Exception ex) { result.Error = Unwrap(ex).ToString(); findings.Add(new(result.StepTypeId, "$", "Error", result.Error)); }
    }
}
catch (Exception ex) { findings.Add(new("Assembly", "$", "Error", Unwrap(ex).ToString())); }
var report = new
{
    Kind = "InstalledHostPureHelperContractVerification", PluginDirectory = package, HostDirectory = hostDirectory,
    HostAssembly = hostIdentity, PluginAssembly = pluginIdentity,
    JsonAssembly = Identity(typeof(JsonSerializer).Assembly, typeof(JsonSerializer).Assembly.Location),
    CurrentCulture = System.Globalization.CultureInfo.CurrentCulture.Name,
    Total = steps.Count, Passed = steps.Count(s => s.Passed), Failed = steps.Count(s => !s.Passed), Steps = steps, Findings = findings,
    NegativeMode = negative ? strictNegative ? "Strict" : "Observe" : "Disabled",
    NegativeChecks = new { Total = steps.Sum(s => s.NegativeChecks.Count), Rejected = steps.Sum(s => s.NegativeChecks.Count(c => c.Rejected)),
        Accepted = steps.Sum(s => s.NegativeChecks.Count(c => !c.Rejected)), SourceChanged = steps.Sum(s => s.NegativeChecks.Count(c => !c.SourceUnchanged)),
        Categories = steps.SelectMany(s => s.NegativeChecks).GroupBy(c => new { c.Category, c.Boundary }).Select(g => new { g.Key.Category, g.Key.Boundary, Total = g.Count(), Rejected = g.Count(c => c.Rejected), Accepted = g.Count(c => !c.Rejected) }).ToArray() },
    Checks = new[] { "实际宿主 FormatValue/ConvertText/DescribeFields", "schema 完整字段/表达式/输出路径/枚举覆盖", "默认及三行非默认配置", "逐顶层参数读出/转换/替换不同初值", "插件 SDK 持久化完整往返", "当前设置生成步骤描述", "schema 忽略字段别名风险" },
    Limitations = new[] { "未调用 live get/set_step_parameters，不修改打开工程", "未创建执行器、求值表达式、连接设备", "不验证助手模型输出截断、工具网关大小上限或现场 UI；接口是安装二进制私有纯函数，版本变化时失败须重新核实" }
};
string reportText = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
if (args.Length == 3) File.WriteAllText(Path.GetFullPath(args[2]), reportText, new UTF8Encoding(false)); else Console.WriteLine(reportText);
Console.Error.WriteLine($"助手契约：{report.Passed}/{report.Total} 通过，Error={findings.Count(f => f.Severity == "Error")}，Warning={findings.Count(f => f.Severity == "Warning")}。");
if (negative) Console.Error.WriteLine($"非法输入：{report.NegativeChecks.Rejected}/{report.NegativeChecks.Total} 已拒绝，接受 {report.NegativeChecks.Accepted}，源设置变化 {report.NegativeChecks.SourceChanged}。");
return steps.Count > 0 && !findings.Any(f => f.Severity == "Error") &&
    (!strictNegative || report.NegativeChecks.Accepted == 0) && report.NegativeChecks.SourceChanged == 0 ? 0 : 2;

static object Identity(Assembly assembly, string path) => new { Path = path, Assembly = assembly.FullName, Mvid = assembly.ManifestModule.ModuleVersionId, Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) };
static IEnumerable<PropertyInfo> Fields(Type type) => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
    .Where(p => p.GetMethod?.IsPublic == true && p.SetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0 && !p.CustomAttributes.Any(a => a.AttributeType.Name is "JsonIgnoreAttribute" or "IgnoreMemberAttribute"));
static Type? Element(Type type) => type.IsArray ? type.GetElementType() : type.GetInterfaces().Append(type).FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0];
static bool Scalar(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime);
static object Populate(object value, int seed, int depth = 0)
{
    if (depth > 10) throw new InvalidOperationException("设置嵌套超过 10 层。");
    foreach (var field in Fields(value.GetType()))
    {
        Type type = Nullable.GetUnderlyingType(field.PropertyType) ?? field.PropertyType;
        object replacement;
        if (Element(type) is Type element && type != typeof(string))
        {
            var entries = Array.CreateInstance(element, 3);
            for (int i = 0; i < 3; i++) entries.SetValue(Sample(element, seed + i + 1, depth + 1), i);
            if (type.IsArray) replacement = entries;
            else { var list = (IList)Activator.CreateInstance(type)!; foreach (var entry in entries) list.Add(entry); replacement = list; }
        }
        else if (type == typeof(bool)) replacement = !(bool)(field.GetValue(value) ?? false);
        else if (type.IsEnum)
        {
            Array values = Enum.GetValues(type);
            object? current = field.GetValue(value);
            replacement = values.Cast<object>().FirstOrDefault(candidate => !Equals(candidate, current)) ?? values.GetValue(0)!;
        }
        else
        {
            replacement = Sample(type, seed, depth + 1);
            if (Scalar(type) && Equals(replacement, field.GetValue(value))) replacement = Sample(type, seed + 17, depth + 1);
        }
        field.SetValue(value, replacement); seed++;
    }
    return value;
}
static object Sample(Type type, int seed, int depth)
{
    if (type == typeof(string)) return "Locals.参数,\"带引号\"_" + seed;
    if (type.IsEnum) return Enum.GetValues(type).GetValue(seed % Enum.GetValues(type).Length)!;
    if (type == typeof(bool)) return seed % 2 == 1;
    if (type.IsPrimitive || type == typeof(decimal)) return Convert.ChangeType(seed + 1, type, System.Globalization.CultureInfo.InvariantCulture);
    if (type == typeof(DateTime)) return new DateTime(2026, 11, 2, 3, 4, 5, DateTimeKind.Utc);
    return Populate(Activator.CreateInstance(type)!, seed, depth);
}
static void Equal(object? expected, object? actual, Type type, string message)
{
    if (!JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected, type), JsonSerializer.SerializeToNode(actual, type)))
        throw new InvalidOperationException(message);
}
static void CheckSchema(Type type, JsonArray? schema, string prefix, StepResult result, List<Finding> findings, HashSet<Type> ancestors)
{
    if (!ancestors.Add(type)) return;
    foreach (PropertyInfo p in Fields(type))
    {
        string path = prefix + p.Name;
        JsonObject? field = schema?.OfType<JsonObject>().SingleOrDefault(f => f["Name"]?.GetValue<string>() == p.Name);
        if (field is null)
        {
            findings.Add(new(result.StepTypeId, path, "Error", "实际宿主 schema 未暴露可读写字段（可能超过展开深度或类型不支持）。"));
            continue;
        }
        result.SchemaFieldsChecked++;
        string expectedKind = p.CustomAttributes.Any(a => a.AttributeType.Name == "ExpressionFieldAttribute") ? "Expression"
            : p.CustomAttributes.Any(a => a.AttributeType.Name == "VariablePathFieldAttribute") ? "VariablePath" : "Literal";
        if (field["Kind"]?.GetValue<string>() != expectedKind)
            findings.Add(new(result.StepTypeId, path, "Error", "实际宿主 schema 的参数用途与设置特性不一致。"));
        Type effective = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        if (effective.IsEnum)
        {
            string declared = field["Type"]?.GetValue<string>() ?? "";
            foreach (string name in Enum.GetNames(effective))
                if (!declared.Contains(name + "=", StringComparison.Ordinal))
                    findings.Add(new(result.StepTypeId, path, "Error", "实际宿主 schema 缺少枚举值：" + name));
        }
        Type? element = effective == typeof(string) ? null : Element(effective);
        Type child = element ?? effective;
        if (!Scalar(child)) CheckSchema(child, field[element is null ? "Fields" : "ItemFields"] as JsonArray,
            path + (element is null ? "." : "[]."), result, findings, new HashSet<Type>(ancestors));
    }
}
static void CheckIgnoredSchemaFields(Type type, string prefix, StepResult result, List<Finding> findings, HashSet<Type> ancestors)
{
    if (!ancestors.Add(type)) return;
    foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0))
    {
        string path = prefix + p.Name;
        if (p.CustomAttributes.Any(a => a.AttributeType.Name is "JsonIgnoreAttribute" or "IgnoreMemberAttribute"))
            findings.Add(new(result.StepTypeId, path, "Warning", "宿主 schema 不过滤此 UI/存储忽略别名；嵌套 JSON 写入将忽略它，必须使用真实字段。"));
        Type child = Element(p.PropertyType) ?? Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        if (!Scalar(child)) CheckIgnoredSchemaFields(child, path + (Element(p.PropertyType) != null ? "[]." : "."), result, findings, new HashSet<Type>(ancestors));
    }
}
static Exception Unwrap(Exception ex) => ex is TargetInvocationException { InnerException: { } inner } ? Unwrap(inner) : ex;
internal sealed record Finding(string StepTypeId, string Path, string Severity, string Message);
internal sealed record ParameterResult(string Scenario, string Name, string Type, string Normalization, int ReadTextLength, bool Passed);
internal sealed class StepResult
{
    public string PluginType { get; init; } = "";
    public string StepTypeId { get; set; } = "";
    public JsonNode? Schema { get; set; }
    public List<ParameterResult> Parameters { get; } = [];
    public Dictionary<string, string> GeneratedDescriptions { get; } = [];
    public List<NegativeResult> NegativeChecks { get; } = [];
    public int SchemaFieldsChecked { get; set; }
    public string? BusinessScenario { get; set; }
    public bool Passed { get; set; }
    public string? Error { get; set; }
}

internal sealed record NegativeResult(string Category, string Boundary, string Parameter, string Path,
    bool Rejected, string? RejectionStage, string? ExceptionType, string? ErrorPath, string? Error, bool SourceUnchanged, bool? InputPreserved);

internal static class NegativeVerifier
{
    private static readonly NullabilityInfoContext Nullability = new();

    internal static void Check(object source, MethodInfo convert, MethodInfo serialize, object serializer, StepResult step)
    {
        byte[] before = (byte[])serialize.Invoke(serializer, [source])!;
        Type contract = serializer.GetType().GetInterfaces().Single(i => i.Name == "IStepSettingSerializer");
        MethodInfo deserialize = contract.GetMethod("Deserialize")!;
        object version = contract.GetProperty("SettingVersion")!.GetValue(serializer)!;
        foreach (PropertyInfo field in Writable(source.GetType()))
        {
            object? expected = field.GetValue(source);
            JsonNode? baseline = JsonSerializer.SerializeToNode(expected, field.PropertyType);
            string boundary = IsScalar(field.PropertyType) ? "HostScalar" : "PluginJson";
            void Test(string category, string path, string? input, bool topLevelNull = false)
            {
                bool rejected = false; string? type = null, errorPath = null, error = null; bool? preserved = null;
                object? converted = null, savedValue = null; string stage = "ConvertText";
                try
                {
                    converted = convert.Invoke(null, [input, field.PropertyType]);
                    object clone = deserialize.Invoke(serializer, [before, version])!;
                    stage = "PropertySet";
                    field.SetValue(clone, converted);
                    stage = "FinalSerialize";
                    byte[] written = (byte[])serialize.Invoke(serializer, [clone])!;
                    object stored = deserialize.Invoke(serializer, [written, version])!;
                    savedValue = field.GetValue(stored);
                }
                catch (Exception ex)
                {
                    while (ex is TargetInvocationException { InnerException: { } inner }) ex = inner;
                    rejected = true; type = ex.GetType().FullName; error = ex.Message;
                    errorPath = ex is JsonException json ? json.Path : null;
                }
                // This comparison is diagnostic only: a verifier parsing error is never
                // counted as rejection by the host. Duplicate properties cannot form a JsonObject.
                if (!rejected && topLevelNull) preserved = savedValue is null;
                else if (!rejected && boundary == "PluginJson" && input is not null)
                {
                    try { preserved = JsonNode.DeepEquals(JsonNode.Parse(input), JsonSerializer.SerializeToNode(savedValue, field.PropertyType)); }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or JsonException) { preserved = false; }
                }
                bool unchanged = before.SequenceEqual((byte[])serialize.Invoke(serializer, [source])!);
                step.NegativeChecks.Add(new(category, topLevelNull ? "HostTopLevelNull" : boundary, field.Name, field.Name + path[1..], rejected,
                    rejected ? stage : null, type, errorPath, error, unchanged, preserved));
            }

            if (!field.PropertyType.IsValueType && Nullability.Create(field).WriteState == NullabilityState.NotNull)
                Test("NonNullableNull", "$", null, topLevelNull: true);

            if (boundary == "HostScalar")
            {
                Type effective = Nullable.GetUnderlyingType(field.PropertyType) ?? field.PropertyType;
                if (effective != typeof(string)) Test("WrongType", "$", "{\"notScalar\":true}");
                if (effective.IsEnum)
                {
                    Test("UndefinedEnumNumber", "$", InvalidEnum(effective));
                    Test("UndefinedEnumName", "$", "__UndefinedEnumValue__");
                }
                if (effective == typeof(double) || effective == typeof(float))
                {
                    // Float/Double.Parse accepts these values; conversion success is not
                    // proof that a finite setting was written. Exercise the final serializer.
                    Test("NonFiniteNaN", "$", "NaN");
                    Test("NonFinitePositiveInfinity", "$", "+Infinity");
                    Test("NonFiniteNegativeInfinity", "$", "-Infinity");
                    Test("FloatingPointPositiveOverflow", "$", "1e1000");
                    Test("FloatingPointNegativeOverflow", "$", "-1e1000");
                }
                continue; // Host top-level null admission is not inside ConvertText.
            }

            void Mutate(string category, List<object> route, JsonNode? replacement)
            {
                JsonNode? input = Replace(baseline?.DeepClone(), route, replacement);
                Test(category, Path(route), input?.ToJsonString() ?? "null");
            }
            void Walk(Type declared, JsonNode? value, List<object> route, NullabilityInfo? nullability, HashSet<Type> ancestors)
            {
                Type effective = Nullable.GetUnderlyingType(declared) ?? declared;
                if (value is null || route.Count > 12) return;
                if (route.Count > 0)
                {
                    JsonNode wrong = value is JsonObject ? new JsonArray() : value is JsonArray ? new JsonObject() : new JsonObject { ["notScalar"] = true };
                    Mutate("WrongType", route, wrong);
                    if ((declared.IsValueType && Nullable.GetUnderlyingType(declared) is null) || nullability?.WriteState == NullabilityState.NotNull)
                        Mutate("NonNullableNull", route, null);
                }
                if (effective.IsEnum)
                {
                    Mutate("UndefinedEnumNumber", route, JsonNode.Parse(InvalidEnum(effective)));
                    Mutate("UndefinedEnumName", route, JsonValue.Create("__UndefinedEnumValue__"));
                    return;
                }
                if (value is JsonArray array)
                {
                    Type? element = Element(effective);
                    if (element is null || array.Count == 0) return;
                    var itemNullability = nullability?.ElementType ?? nullability?.GenericTypeArguments.FirstOrDefault();
                    Walk(element, array[0], Append(route, 0), itemNullability, ancestors);
                    if (array.Count > 1 && array[^1] is JsonObject last)
                    {
                        var changed = (JsonObject)last.DeepClone(); changed["__unknown_later_item__"] = true;
                        Mutate("UnknownLaterItem", Append(route, array.Count - 1), changed);
                    }
                    return;
                }
                if (value is not JsonObject obj || !ancestors.Add(effective)) return;
                var unknown = (JsonObject)obj.DeepClone(); unknown["__unknown_field__"] = true;
                Mutate("UnknownField", route, unknown);
                var first = obj.FirstOrDefault();
                if (first.Key is not null)
                    Test("DuplicateField", Path(Append(route, first.Key)), RenderDuplicate(baseline, route, first.Key));
                foreach (PropertyInfo property in effective.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0))
                {
                    bool ignored = property.CustomAttributes.Any(a => a.AttributeType.Name is "JsonIgnoreAttribute" or "IgnoreMemberAttribute");
                    if (property.SetMethod?.IsPublic != true || ignored)
                    {
                        var readOnly = (JsonObject)obj.DeepClone(); readOnly[property.Name] = 42;
                        Mutate("ReadOnlyField", route, readOnly);
                        continue;
                    }
                    if (obj.TryGetPropertyValue(property.Name, out var nested))
                        Walk(property.PropertyType, nested, Append(route, property.Name), Nullability.Create(property), new HashSet<Type>(ancestors));
                }
            }
            Walk(field.PropertyType, baseline, [], Nullability.Create(field), []);
        }
    }

    private static IEnumerable<PropertyInfo> Writable(Type type) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.GetMethod?.IsPublic == true && p.SetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0 && !p.CustomAttributes.Any(a => a.AttributeType.Name is "JsonIgnoreAttribute" or "IgnoreMemberAttribute"));
    private static bool IsScalar(Type type) => (Nullable.GetUnderlyingType(type) ?? type) is var effective &&
        (effective.IsPrimitive || effective.IsEnum || effective == typeof(string) || effective == typeof(decimal) || effective == typeof(DateTime));
    private static Type? Element(Type type) => type.IsArray ? type.GetElementType() : type.GetInterfaces().Append(type).FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0];
    private static string InvalidEnum(Type type)
    {
        for (int number = 127; number >= 0; number--)
            if (!Enum.IsDefined(type, Enum.ToObject(type, number))) return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        throw new InvalidOperationException("没有可用的未定义枚举测试值：" + type.FullName);
    }
    private static List<object> Append(List<object> path, object value) => [.. path, value];
    private static string Path(List<object> route) => "$" + string.Concat(route.Select(s => s is int index ? $"[{index}]" : "." + s));
    private static JsonNode? Replace(JsonNode? root, List<object> route, JsonNode? value)
    {
        if (route.Count == 0) return value;
        JsonNode node = root!;
        foreach (object segment in route.Take(route.Count - 1)) node = segment is int index ? node[index]! : node[(string)segment]!;
        if (route[^1] is int last) node[last] = value; else node[(string)route[^1]] = value;
        return root;
    }
    private static string RenderDuplicate(JsonNode? root, List<object> route, string field)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(root, []); writer.Flush();
            void Write(JsonNode? node, List<object> current)
            {
                if (node is JsonObject obj)
                {
                    writer.WriteStartObject();
                    foreach (var item in obj)
                    {
                        writer.WritePropertyName(item.Key); Write(item.Value, Append(current, item.Key));
                        if (current.SequenceEqual(route) && item.Key == field)
                        { writer.WritePropertyName(item.Key); item.Value?.WriteTo(writer); if (item.Value is null) writer.WriteNullValue(); }
                    }
                    writer.WriteEndObject();
                }
                else if (node is JsonArray array)
                { writer.WriteStartArray(); for (int i = 0; i < array.Count; i++) Write(array[i], Append(current, i)); writer.WriteEndArray(); }
                else if (node is null) writer.WriteNullValue(); else node.WriteTo(writer);
            }
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

internal static class HostIl
{
    internal static string Dump(Type host)
    {
        var opcodes = typeof(System.Reflection.Emit.OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(System.Reflection.Emit.OpCode))
            .Select(f => (System.Reflection.Emit.OpCode)f.GetValue(null)!).ToDictionary(o => unchecked((ushort)o.Value));
        var result = new StringBuilder();
        foreach (Type type in host.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Prepend(host))
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                     .Where(m => m.Name.Contains("SetStepParameters", StringComparison.Ordinal) || type.Name.Contains("SetStepParameters", StringComparison.Ordinal)))
        {
            byte[]? body = method.GetMethodBody()?.GetILAsByteArray(); if (body is null) continue;
            result.AppendLine(type.FullName + "." + method.Name);
            int p = 0;
            while (p < body.Length)
            {
                int start = p; ushort value = body[p++]; if (value == 0xfe) value = (ushort)(0xfe00 | body[p++]);
                var op = opcodes[value]; string operand = "";
                switch (op.OperandType)
                {
                    case System.Reflection.Emit.OperandType.InlineNone: break;
                    case System.Reflection.Emit.OperandType.ShortInlineBrTarget: { sbyte x = unchecked((sbyte)body[p++]); operand = $"IL_{p + x:x4}"; break; }
                    case System.Reflection.Emit.OperandType.InlineBrTarget: { int x = BitConverter.ToInt32(body, p); p += 4; operand = $"IL_{p + x:x4}"; break; }
                    case System.Reflection.Emit.OperandType.ShortInlineI: operand = unchecked((sbyte)body[p++]).ToString(); break;
                    case System.Reflection.Emit.OperandType.ShortInlineVar: operand = body[p++].ToString(); break;
                    case System.Reflection.Emit.OperandType.InlineVar: operand = BitConverter.ToUInt16(body, p).ToString(); p += 2; break;
                    case System.Reflection.Emit.OperandType.InlineI: operand = BitConverter.ToInt32(body, p).ToString(); p += 4; break;
                    case System.Reflection.Emit.OperandType.InlineI8: operand = BitConverter.ToInt64(body, p).ToString(); p += 8; break;
                    case System.Reflection.Emit.OperandType.ShortInlineR: operand = BitConverter.ToSingle(body, p).ToString(); p += 4; break;
                    case System.Reflection.Emit.OperandType.InlineR: operand = BitConverter.ToDouble(body, p).ToString(); p += 8; break;
                    case System.Reflection.Emit.OperandType.InlineSwitch:
                        int count = BitConverter.ToInt32(body, p); p += 4; int end = p + count * 4;
                        var targets = new List<string>(); for (int i = 0; i < count; i++) { targets.Add($"IL_{end + BitConverter.ToInt32(body, p):x4}"); p += 4; }
                        operand = string.Join(",", targets); break;
                    default:
                        int token = BitConverter.ToInt32(body, p); p += 4;
                        try { operand = op.OperandType == System.Reflection.Emit.OperandType.InlineString ? "\"" + method.Module.ResolveString(token) + "\""
                            : method.Module.ResolveMember(token, type.IsGenericType ? type.GetGenericArguments() : null, method.IsGenericMethod ? method.GetGenericArguments() : null)?.ToString() ?? token.ToString("X8"); }
                        catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or TypeLoadException) { operand = token.ToString("X8") + " (" + ex.GetType().Name + ")"; }
                        break;
                }
                result.AppendLine($"  IL_{start:x4}: {op.Name} {operand}");
            }
        }
        return result.ToString();
    }
}
