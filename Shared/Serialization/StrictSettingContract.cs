using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PluginCommon.Serialization;

/// <summary>Settings-only shape validation; never evaluates expressions or performs device I/O.</summary>
internal static class StrictSettingContract
{
    private static readonly ConcurrentDictionary<Type, Member[]> Contracts = new();

    internal sealed record Member(PropertyInfo Property, NullabilityInfo Nullability)
    {
        internal string Name => Property.Name;
        internal Type Type => Property.PropertyType;
        internal bool Writable => Property.SetMethod?.IsPublic == true;
    }

    internal static Member[] Members(Type type) => Contracts.GetOrAdd(type, static current =>
    {
        var nullability = new NullabilityInfoContext();
        return current.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetMethod?.IsPublic == true && p.GetIndexParameters().Length == 0 &&
                !p.IsDefined(typeof(JsonIgnoreAttribute)) &&
                !p.CustomAttributes.Any(a => a.AttributeType.Name == "IgnoreMemberAttribute"))
            .Select(p => new Member(p, nullability.Create(p))).ToArray();
    });

    internal static void Validate(object value, Type expected)
    {
        if (value is null || !expected.IsInstanceOfType(value))
            throw Error("$", "设置对象为空或类型不匹配");
        Check(value, expected, null, "$", 0, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    internal static void CheckValue(object? value, Member member, string path) =>
        Check(value, member.Type, member.Nullability, path, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static void Check(object? value, Type declared, NullabilityInfo? nullability, string path, int depth, HashSet<object> ancestors)
    {
        if (depth > 64) throw Error(path, "参数嵌套超过 64 层");
        Type? nullableValue = Nullable.GetUnderlyingType(declared);
        if (value is null)
        {
            if ((declared.IsValueType && nullableValue is null) || nullability?.WriteState == NullabilityState.NotNull)
                throw Error(path, "字段不允许为 null；清空文本请使用空字符串，清空集合请使用空数组");
            return;
        }
        Type effective = nullableValue ?? declared;
        if (!effective.IsInstanceOfType(value)) throw Error(path, "字段值的类型不匹配");
        if (effective.IsEnum)
        {
            if (!Enum.IsDefined(effective, value)) throw Error(path, $"不是 {effective.Name} 已定义的枚举值");
            return;
        }
        if ((value is double number && !double.IsFinite(number)) ||
            (value is float single && !float.IsFinite(single)))
            throw Error(path, "数值必须有限，不能为 NaN 或正负无穷大");
        if (effective.IsPrimitive || effective == typeof(string) || effective == typeof(decimal)) return;
        if (!ancestors.Add(value)) throw Error(path, "设置中不允许循环引用");
        try
        {
            if (value is IEnumerable sequence)
            {
                Type? element = effective.IsArray ? effective.GetElementType() : effective.GetInterfaces().Append(effective)
                    .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0];
                if (element is null) throw Error(path, "集合必须声明元素类型");
                var elementNullability = effective.IsArray ? nullability?.ElementType : nullability?.GenericTypeArguments.FirstOrDefault();
                int index = 0;
                foreach (object? entry in sequence) Check(entry, element, elementNullability, $"{path}[{index++}]", depth + 1, ancestors);
                return;
            }
            foreach (var member in Members(effective).Where(m => m.Writable))
                Check(member.Property.GetValue(value), member.Type, member.Nullability, path + "." + member.Name, depth + 1, ancestors);
        }
        finally { ancestors.Remove(value); }
    }

    internal static JsonException Error(string path, string reason) => new($"参数 {path}：{reason}。");
    internal static string FieldPath(string name) => "$." + (name.Length <= 128 ? name : name[..128] + "…");
}
