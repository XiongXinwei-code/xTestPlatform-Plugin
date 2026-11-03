using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PluginCommon.Serialization;

/// <summary>
/// Activated by each settings object's attribute, including when the host uses
/// permissive JSON options. No global serializer mutation and no second DTO.
/// </summary>
internal sealed class StrictSettingJsonConverter : JsonConverterFactory
{
    public StrictSettingJsonConverter() { }
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsClass && typeToConvert != typeof(string);
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(ObjectConverter<>).MakeGenericType(typeToConvert))!;

    private sealed class ObjectConverter<T> : JsonConverter<T> where T : class, new()
    {
        public ObjectConverter() { }
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw StrictSettingContract.Error("$", "此参数需要 JSON 对象");
            var members = StrictSettingContract.Members(typeof(T)).ToDictionary(m => m.Name, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var writes = new List<(StrictSettingContract.Member Member, object? Value, byte[] Expected)>();
            // Convert and validate every supplied field before invoking any setter.
            foreach (var field in document.RootElement.EnumerateObject())
            {
                string path = StrictSettingContract.FieldPath(field.Name);
                if (!seen.Add(field.Name)) throw StrictSettingContract.Error(path, "字段重复，不能确定要保存哪个值");
                if (!members.TryGetValue(field.Name, out var member))
                    throw StrictSettingContract.Error(path, "字段不存在，请使用步骤 schema 中的准确名称");
                if (!member.Writable) throw StrictSettingContract.Error(path, "字段只读，不能写入");
                object? converted;
                try
                {
                    converted = field.Value.Deserialize(member.Type, options);
                    StrictSettingContract.CheckValue(converted, member, path);
                }
                catch (JsonException error)
                {
                    // An outer array reader adds its own row index. Keep the nested
                    // member path in the message as well, without including payloads.
                    throw StrictSettingContract.Error(path, error.Message);
                }
                writes.Add((member, converted, JsonSerializer.SerializeToUtf8Bytes(converted, member.Type, options)));
            }
            var result = new T();
            foreach (var write in writes)
            {
                try { write.Member.Property.SetValue(result, write.Value); }
                catch (Exception error) when (error is TargetInvocationException or ArgumentException)
                {
                    throw StrictSettingContract.Error(StrictSettingContract.FieldPath(write.Member.Name), "字段赋值失败（" + error.GetBaseException().GetType().Name + "）");
                }
            }
            // Check after ALL setters, so one field cannot silently reset an earlier field.
            foreach (var write in writes)
            {
                var actual = JsonSerializer.SerializeToUtf8Bytes(write.Member.Property.GetValue(result), write.Member.Type, options);
                if (!write.Expected.AsSpan().SequenceEqual(actual))
                    throw StrictSettingContract.Error(StrictSettingContract.FieldPath(write.Member.Name), "写入值被修改或忽略，未保存此配置");
            }
            StrictSettingContract.Validate(result, typeof(T));
            return result;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var member in StrictSettingContract.Members(typeof(T)).Where(m => m.Writable))
            {
                writer.WritePropertyName(member.Name);
                JsonSerializer.Serialize(writer, member.Property.GetValue(value), member.Type, options);
            }
            writer.WriteEndObject();
        }
    }
}
