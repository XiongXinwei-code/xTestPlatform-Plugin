using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

Console.OutputEncoding = Encoding.UTF8;
if (args.Length < 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("用法: WidgetInfoExtractor <插件产物目录> [--strict]");
    return 1;
}

var pluginDir = Path.GetFullPath(args[0]);
var strict = args.Skip(1).Contains("--strict", StringComparer.Ordinal);
var invalid = false;
var widgets = new List<WidgetInfo>();
var dlls = Directory.GetFiles(pluginDir, "*.WidgetPlugin.dll");
if (dlls.Length == 0)
{
    Console.Error.WriteLine($"警告: {pluginDir} 中未找到 *.WidgetPlugin.dll");
    return strict ? 2 : 0;
}

foreach (var dll in dlls)
{
    var context = new PluginLoadContext(dll);
    try
    {
        var assembly = context.LoadFromAssemblyPath(dll);
        foreach (var type in GetLoadableTypes(assembly))
        {
            if (!type.IsClass || type.IsAbstract || !type.IsPublic ||
                !type.GetInterfaces().Any(i => i.FullName == "DashboardWidgets.Contracts.IDashboardWidgetPlugin"))
                continue;

            try
            {
                var instance = Activator.CreateInstance(type) ?? throw new InvalidOperationException("无参构造返回 null");
                var id = GetString(type, instance, "WidgetTypeId");
                var displayName = GetString(type, instance, "DisplayName");
                var category = GetString(type, instance, "Category");
                var description = GetString(type, instance, "Description");
                var capabilities = GetEnumNames(type, instance, "Capabilities");
                var dataTypes = GetEnumerableNames(type, instance, "SupportedDataTypes");
                var defaultSize = GetDefaultSize(type, instance);

                if (strict)
                {
                    if (!Regex.IsMatch(id, @"^[A-Za-z][A-Za-z0-9_]*\.[A-Za-z][A-Za-z0-9_]*$") ||
                        id.StartsWith("xTest.", StringComparison.OrdinalIgnoreCase))
                        Fail($"WidgetTypeId 不符合 <Vendor>.<WidgetName> 或使用保留前缀: {id}");
                    if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(description))
                        Fail($"控件元数据不能为空: {type.FullName}");
                    if (!Regex.IsMatch(displayName, @"^[A-Za-z][A-Za-z0-9_ +()/.-]*$") ||
                        !Regex.IsMatch(category, @"^[A-Za-z][A-Za-z0-9_ +()/.-]*$"))
                        Fail($"控件 DisplayName 和 Category 必须使用英文工具箱名称: {id} / {category} / {displayName}");
                    if (!HasStandardDescription(description))
                        Fail($"控件 Description 必须按顺序包含 ## 功能、## 参数、## 行为、## 示例: {type.FullName}");
                    if (defaultSize is null)
                        Fail($"默认尺寸必须为有限正数: {type.FullName}");
                }

                widgets.Add(new WidgetInfo(id, displayName, category, description, capabilities, dataTypes, defaultSize ?? string.Empty));
            }
            catch (Exception ex)
            {
                Fail($"无法提取控件 {type.FullName}: {ex.GetBaseException().Message}");
            }
        }
    }
    catch (Exception ex)
    {
        Fail($"无法加载 {Path.GetFileName(dll)}: {ex.GetBaseException().Message}");
    }
}

if (widgets.Count == 0)
    Fail("未发现 IDashboardWidgetPlugin 实现");
if (widgets.GroupBy(w => w.widgetTypeId, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
    Fail("WidgetTypeId 存在重复");

Console.WriteLine(JsonSerializer.Serialize(widgets.OrderBy(w => w.displayName, StringComparer.Ordinal), new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default
}));
return strict && invalid ? 2 : 0;

void Fail(string message)
{
    invalid = true;
    Console.Error.WriteLine(message);
}

static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
{
    try { return assembly.GetTypes(); }
    catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); }
}

static string GetString(Type type, object instance, string name)
    => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance) as string ?? string.Empty;

static bool HasStandardDescription(string description)
{
    var required = new[] { "## 功能", "## 参数", "## 行为", "## 示例" };
    var position = -1;
    foreach (var heading in required)
    {
        var next = description.IndexOf(heading, position + 1, StringComparison.Ordinal);
        if (next < 0)
            return false;
        position = next;
    }
    return true;
}

static List<string> GetEnumNames(Type type, object instance, string name)
{
    var value = type.GetProperty(name)?.GetValue(instance);
    if (value is not Enum enumValue || Convert.ToInt64(enumValue) == 0)
        return [];
    return enumValue.ToString().Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList();
}

static List<string> GetEnumerableNames(Type type, object instance, string name)
{
    if (type.GetProperty(name)?.GetValue(instance) is not IEnumerable values)
        return [];
    return values.Cast<object?>().Where(v => v is not null).Select(v => v!.ToString()!).ToList();
}

static string? GetDefaultSize(Type type, object instance)
{
    var size = type.GetProperty("DefaultSize")?.GetValue(instance);
    if (size is null)
        return null;
    var sizeType = size.GetType();
    var width = Convert.ToDouble(sizeType.GetProperty("Width")?.GetValue(size));
    var height = Convert.ToDouble(sizeType.GetProperty("Height")?.GetValue(size));
    return double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > 0
        ? $"{width:0.##}x{height:0.##}"
        : null;
}

internal sealed record WidgetInfo(
    string widgetTypeId,
    string displayName,
    string category,
    string description,
    List<string> capabilities,
    List<string> supportedDataTypes,
    string defaultSize);

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;

    public PluginLoadContext(string assemblyPath) : base(isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(assemblyPath);
        _pluginDirectory = Path.GetDirectoryName(assemblyPath)!;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is null)
        {
            var candidate = Path.Combine(_pluginDirectory, assemblyName.Name + ".dll");
            if (File.Exists(candidate)) path = candidate;
        }
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }
}
