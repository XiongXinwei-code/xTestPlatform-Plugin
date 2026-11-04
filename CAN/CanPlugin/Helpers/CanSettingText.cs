using System.Text.Json;

namespace CAN.Helpers;

/// <summary>
/// 兼容把复杂参数按 ToString 拼接的工具。只格式化当前配置，不执行表达式，
/// 不截断字段，也不创建另一份持久化配置。平台仍负责数组边界、分页及局部更新。
/// </summary>
internal static class CanSettingText
{
    internal static string Format(object value) => JsonSerializer.Serialize(value, value.GetType());
}
