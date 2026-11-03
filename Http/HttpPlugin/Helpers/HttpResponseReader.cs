using System.Text.Json;
using Http.Models;

namespace Http.Helpers;

/// <summary>有界读取正文；只有完整响应读取成功后才交给步骤发布变量。</summary>
internal static class HttpResponseReader
{
    internal static async Task<string> ReadAsync(HttpResponseMessage response, HttpResponseFormat format,
        int maximumBytes, bool head, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // HEAD 的 Content-Length 描述 GET 正文，不代表本次有正文。
        if (head || response.StatusCode is System.Net.HttpStatusCode.NoContent or System.Net.HttpStatusCode.NotModified)
            return string.Empty;
        if (response.Content.Headers.ContentLength is long length && length > maximumBytes)
            throw new InvalidOperationException($"HTTP 响应正文超过配置上限 {maximumBytes} 字节，未发布结果");
        using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[Math.Min(maximumBytes, 81920)];
        while (true)
        {
            int read = await input.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            if (read == 0) break;
            if (output.Length + read > maximumBytes)
                throw new InvalidOperationException($"HTTP 响应正文超过配置上限 {maximumBytes} 字节，未发布结果");
            output.Write(buffer, 0, read);
        }
        ct.ThrowIfCancellationRequested();
        byte[] bytes = output.ToArray();
        if (format == HttpResponseFormat.Hex) return Convert.ToHexString(bytes);
        if (format == HttpResponseFormat.Base64) return Convert.ToBase64String(bytes);
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = response.Content.Headers.ContentType;
        return await content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    internal static string GetHeadersJson(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in response.Headers.Concat(response.Content.Headers).Concat(response.TrailingHeaders))
            headers[item.Key] = headers.TryGetValue(item.Key, out var existing)
                ? existing.Concat(item.Value).ToArray() : item.Value.ToArray();
        return JsonSerializer.Serialize(headers);
    }

    internal static byte[] ParseHexBody(string text)
    {
        var compact = string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        if (compact.Length % 2 != 0 || compact.Any(c => !Uri.IsHexDigit(c)))
            throw new FormatException("HTTP Bytes 正文必须为完整十六进制字节，可使用空白分隔；正文值不记录");
        return Convert.FromHexString(compact);
    }
}
