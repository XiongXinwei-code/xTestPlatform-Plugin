using System.Net.Sockets;
using System.Text;

namespace LXI.Helpers;

public static class LxiHelper
{
	private const string ConnectionKeyPrefix = "__LXI_";

	public static string GetConnectionKey(string ipAddress) => $"{ConnectionKeyPrefix}{ipAddress}";

	public static async Task<TcpClient> ConnectAsync(string ipAddress, int port, int timeoutMs, CancellationToken ct)
	{
		var client = new TcpClient();
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		cts.CancelAfter(timeoutMs);
		await client.ConnectAsync(ipAddress, port, cts.Token);
		return client;
	}

	public static async Task WriteCommandAsync(TcpClient client, string command, string terminator, CancellationToken ct)
	{
		var data = Encoding.ASCII.GetBytes(command + terminator);
		await client.GetStream().WriteAsync(data, 0, data.Length, ct);
	}

	public static async Task<string> ReadResponseAsync(TcpClient client, string terminator, int timeoutMs, CancellationToken ct)
	{
		var stream = client.GetStream();
		var buffer = new byte[4096];
		var sb = new StringBuilder();
		using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		cts.CancelAfter(timeoutMs);

		while (true)
		{
			int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
			if (bytesRead == 0) break;

			sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
			var current = sb.ToString();
			if (current.EndsWith(terminator))
			{
				return current.TrimEnd(terminator.ToCharArray());
			}
		}

		return sb.ToString().TrimEnd(terminator.ToCharArray());
	}
}