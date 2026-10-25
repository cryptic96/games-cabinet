using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Hosting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Verifies the Kestrel limits for a host that faces the internet: the configured values reach the server, a connection that
/// never finishes its headers is closed, a connection past the ceiling is refused while the ops listener keeps answering, an
/// oversized body is refused, and every page handler that takes an upload carries its own size limit.
/// </summary>
public partial class KestrelLimitsTests
{
    private const string ConnectionsKey = "HostLimits:MaxConcurrentConnections";
    private const string BodyKey = "HostLimits:MaxRequestBodyBytes";
    private const string TimeoutKey = "HostLimits:RequestHeadersTimeoutSeconds";
    private const int SeventyKilobytes = 70 * 1024;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_defaults_reach_kestrel_and_the_keep_alive_stays_above_the_proxy_idle_timeout()
    {
        await using var factory = new CabinetWebApplicationFactory();

        var limits = LimitsOf(factory);

        limits.MaxConcurrentConnections.Should().Be(512);
        limits.MaxRequestBodySize.Should().Be(65536);
        limits.RequestHeadersTimeout.Should().Be(TimeSpan.FromSeconds(10));
        limits.KeepAliveTimeout.Should().Be(TimeSpan.FromSeconds(130));
    }

    [Fact]
    public async Task The_settings_change_the_connection_ceiling_the_body_limit_and_the_header_timeout()
    {
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?>
        {
            [ConnectionsKey] = "64",
            [BodyKey] = "2048",
            [TimeoutKey] = "5",
        });

        var limits = LimitsOf(factory);

        limits.MaxConcurrentConnections.Should().Be(64);
        limits.MaxRequestBodySize.Should().Be(2048);
        limits.RequestHeadersTimeout.Should().Be(TimeSpan.FromSeconds(5));
        limits.KeepAliveTimeout.Should().Be(TimeSpan.FromSeconds(130));
    }

    [Fact]
    public async Task A_connection_that_never_finishes_its_headers_is_closed_by_the_server()
    {
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { [TimeoutKey] = "2" });
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, factory.PublicPort, Token);
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"GET / HTTP/1.1\r\nHost: 127.0.0.1:{factory.PublicPort}\r\n"), Token);

        var closedWithin = await ReadUntilClosedAsync(stream, TimeSpan.FromSeconds(6));

        closedWithin.Should().BeTrue("the server must drop a request whose headers never end");
    }

    [Fact]
    public async Task A_connection_past_the_ceiling_is_refused_while_health_on_the_ops_port_still_answers()
    {
        await using var factory = new CabinetWebApplicationFactory(new Dictionary<string, string?> { [ConnectionsKey] = "16" });
        var holders = new List<TcpClient>();

        try
        {
            for (var held = 0; held < 16; held++)
            {
                var holder = new TcpClient();
                holders.Add(holder);
                await holder.ConnectAsync(IPAddress.Loopback, factory.PublicPort, Token);
            }

            using var seventeenth = new TcpClient();
            await seventeenth.ConnectAsync(IPAddress.Loopback, factory.PublicPort, Token);
            var refused = await IsClosedWithoutAnswerAsync(seventeenth, TimeSpan.FromSeconds(5));

            using var ops = factory.CreateOpsClient();
            using var healthLimit = CancellationTokenSource.CreateLinkedTokenSource(Token);
            healthLimit.CancelAfter(TimeSpan.FromSeconds(2));
            using var health = await ops.GetAsync("/health", healthLimit.Token);

            refused.Should().BeTrue("a connection beyond the ceiling must get no HTTP answer");
            health.StatusCode.Should().Be(HttpStatusCode.OK, "the ops listener has its own connection budget");
        }
        finally
        {
            foreach (var holder in holders)
            {
                holder.Dispose();
            }
        }

        using var afterwards = factory.CreatePublicClient();
        await SyncHarness.WaitUntil(async () =>
        {
            try
            {
                using var response = await afterwards.GetAsync("/", Token);

                return response.StatusCode == HttpStatusCode.OK;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        });
    }

    [Fact]
    public async Task An_owner_form_of_seventy_kilobytes_is_refused_on_a_handler_with_no_limit_of_its_own()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);
        await session.GetAsync("/owner/locations");

        var reply = await session.PostFormAsync(
            "/owner/locations?handler=AddLocation",
            [
                new KeyValuePair<string, string>("name", new string('x', SeventyKilobytes)),
                new KeyValuePair<string, string>("revision", "0"),
            ]);

        reply.Status.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Each_host_start_logs_the_memory_the_runtime_sees_in_plain_words()
    {
        var logs = new CapturingLoggerProvider();
        await using var factory = new CabinetWebApplicationFactory(
            new Dictionary<string, string?>(),
            services => services.AddSingleton<ILoggerProvider>(logs));

        var report = logs.Lines.Where(line => line.StartsWith("The runtime sees ", StringComparison.Ordinal)).ToList();

        report.Should().NotBeEmpty();
        report.Should().OnlyContain(line => ReportShape().IsMatch(line));
    }

    [Fact]
    public async Task One_start_of_the_report_logs_exactly_one_information_line()
    {
        var logs = new RecordingLogger();
        var report = new HostLimitsReport(logs);

        await report.StartAsync(Token);
        await report.StopAsync(Token);

        logs.Entries.Should().ContainSingle();
        logs.Entries[0].Level.Should().Be(LogLevel.Information);
        ReportShape().IsMatch(logs.Entries[0].Message).Should().BeTrue();
    }

    [Fact]
    public void Every_page_handler_that_binds_an_upload_or_a_raw_body_carries_its_own_size_limit()
    {
        var handlers = UploadHandlers().ToList();

        handlers.Select(handler => handler.Name).Should().Equal("OnPostCheckFileAsync");
        handlers.Should().OnlyContain(
            handler => handler.DeclaringType!.IsDefined(typeof(RequestSizeLimitAttribute), inherit: true),
            "a handler that reads an upload must say how large it may be on its page model, because Razor Pages ignores the attribute on a handler method");
    }

    [GeneratedRegex(@"^The runtime sees \d+ MB of memory and \d+ processors; server GC: (True|False)$")]
    private static partial Regex ReportShape();

    private static KestrelServerLimits LimitsOf(CabinetWebApplicationFactory factory) =>
        factory.ServingServices.GetRequiredService<IOptions<KestrelServerOptions>>().Value.Limits;

    private static IEnumerable<MethodInfo> UploadHandlers()
    {
        var bodyTypes = new[] { typeof(IFormFile), typeof(IFormFileCollection), typeof(IEnumerable<IFormFile>), typeof(Stream) };

        return typeof(Program).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(PageModel)))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.Name.StartsWith("OnPost", StringComparison.Ordinal)
                || method.Name.StartsWith("OnPut", StringComparison.Ordinal))
            .Where(method => method.GetParameters().Any(parameter => bodyTypes.Contains(parameter.ParameterType)));
    }

    private static async Task<bool> ReadUntilClosedAsync(NetworkStream stream, TimeSpan limit)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(limit);
        var buffer = new byte[1024];

        try
        {
            while (await stream.ReadAsync(buffer, timeout.Token) > 0)
            {
            }

            return true;
        }
        catch (IOException)
        {
            return true;
        }
        catch (OperationCanceledException) when (!Token.IsCancellationRequested)
        {
            return false;
        }
    }

    private static async Task<bool> IsClosedWithoutAnswerAsync(TcpClient client, TimeSpan limit)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(limit);
        var stream = client.GetStream();
        var buffer = new byte[1024];

        try
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n"), timeout.Token);

            return await stream.ReadAsync(buffer, timeout.Token) == 0;
        }
        catch (IOException)
        {
            return true;
        }
        catch (SocketException)
        {
            return true;
        }
        catch (OperationCanceledException) when (!Token.IsCancellationRequested)
        {
            return false;
        }
    }

    private sealed class RecordingLogger : ILogger<HostLimitsReport>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
