using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>
/// Sends a request line exactly as given over a plain socket. An HttpClient rewrites dot segments and percent-encoding
/// before it sends anything, which would hide the path tricks these requests are meant to try.
/// </summary>
internal static class RawHttp
{
    /// <summary>Sends one request to a loopback port and returns the status code and the body of the answer.</summary>
    /// <param name="port">The loopback port to connect to.</param>
    /// <param name="requestTarget">The request target, written verbatim after the method.</param>
    /// <param name="headers">Extra headers; a Host header given here replaces the default one naming the port.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    public static async Task<(int Status, string Body)> SendAsync(
        int port,
        string requestTarget,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(requestTarget);

        var lines = new List<string> { $"GET {requestTarget} HTTP/1.1" };
        var extra = headers ?? new Dictionary<string, string>();
        if (!extra.Keys.Any(name => name.Equals("Host", StringComparison.OrdinalIgnoreCase)))
        {
            lines.Add($"Host: 127.0.0.1:{port}");
        }

        lines.AddRange(extra.Select(header => $"{header.Key}: {header.Value}"));
        lines.Add("Connection: close");

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
        var stream = client.GetStream();

        var request = string.Join("\r\n", lines) + "\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), cancellationToken);

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var answer = Encoding.UTF8.GetString(buffer.ToArray());

        var statusLine = answer[..answer.IndexOf("\r\n", StringComparison.Ordinal)];
        var status = int.Parse(statusLine.Split(' ')[1], CultureInfo.InvariantCulture);
        var split = answer.IndexOf("\r\n\r\n", StringComparison.Ordinal);

        return (status, split < 0 ? string.Empty : answer[(split + 4)..]);
    }
}
