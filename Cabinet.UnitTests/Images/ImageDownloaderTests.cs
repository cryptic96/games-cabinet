using System.Net;
using System.Net.Http.Headers;
using Cabinet.FakeBgg.Testing;
using Cabinet.Repository.Bgg;
using Cabinet.Repository.Images;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Images;

/// <summary>Proves downloads obey the address policy, the size and type limits, the redirect limit and the pacing, and carry no credentials.</summary>
[Trait("Category", "Images")]
public sealed class ImageDownloaderTests
{
    private const string Host = "cf.example.org";
    private const long MaxBytes = 1000;

    private static readonly ArtLimits Limits = new(MaxBytes, 1_000_000, TimeSpan.FromSeconds(30));
    private static readonly ArtSourcePolicy Policy = new(new HashSet<string>(StringComparer.Ordinal) { Host });
    private static readonly BggOptions Bgg = new(BggOptions.DefaultBaseUri, "sentinel-user-name", "sentinel-token-value", null, BggOptions.MinimumRequestGap, false, "0.0.0-test");

    [Fact]
    public async Task An_address_the_policy_refuses_sends_no_request()
    {
        var handler = new StubHandler(_ => Ok([1, 2, 3]));

        var result = await Downloader(handler).DownloadAsync(new Uri("https://evil.example/a.png"), TestContext.Current.CancellationToken);

        result.Should().Be(ArtDownload.Refused("host"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_picture_is_downloaded_with_the_user_agent_and_no_credentials()
    {
        var handler = new StubHandler(_ => Ok([1, 2, 3]));

        var result = await Downloader(handler).DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);

        result.Should().BeOfType<ArtDownload.Fetched>().Which.Bytes.Should().Equal(1, 2, 3);
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Authorization.Should().BeNull();
        handler.Requests[0].UserAgent.Should().StartWith("GamesCabinet/");
        handler.Requests[0].Accept.Should().Be("image/*");
    }

    [Fact]
    public async Task A_chain_of_three_redirects_is_followed_and_a_fourth_is_refused()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/1" => Redirect("/2"),
            "/2" => Redirect("/3"),
            "/3" => Redirect("/4"),
            "/4" => Ok([9]),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var longer = new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/1" => Redirect("/2"),
            "/2" => Redirect("/3"),
            "/3" => Redirect("/4"),
            "/4" => Redirect("/5"),
            _ => Ok([9]),
        });

        var followed = await Downloader(handler).DownloadAsync(Uri("1"), TestContext.Current.CancellationToken);
        var refused = await Downloader(longer).DownloadAsync(Uri("1"), TestContext.Current.CancellationToken);

        followed.Should().BeOfType<ArtDownload.Fetched>();
        refused.Should().Be(ArtDownload.Refused("redirect"));
    }

    [Fact]
    public async Task A_redirect_to_another_host_is_refused_and_never_requested()
    {
        var handler = new StubHandler(_ => Redirect("https://evil.example/a.png"));

        var result = await Downloader(handler).DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);

        result.Should().Be(ArtDownload.Refused("redirect"));
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task A_redirect_without_a_location_is_a_failure()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Found));

        var result = await Downloader(handler).DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);

        result.Should().Be(ArtDownload.Failed("status"));
    }

    [Fact]
    public async Task A_content_length_over_the_cap_is_refused_without_reading_the_body()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new AnnouncedLengthContent(MaxBytes + 1) });

        var result = await Downloader(handler).DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);

        result.Should().Be(ArtDownload.Refused("size"));
    }

    [Fact]
    public async Task A_body_exactly_at_the_cap_is_kept_and_a_streamed_body_one_byte_over_is_refused()
    {
        var atCap = new StubHandler(_ => Chunked(new byte[MaxBytes]));
        var over = new StubHandler(_ => Chunked(new byte[MaxBytes + 1]));

        var kept = await Downloader(atCap).DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);
        var refused = await Downloader(over).DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);

        kept.Should().BeOfType<ArtDownload.Fetched>().Which.Bytes.Should().HaveCount((int)MaxBytes);
        refused.Should().Be(ArtDownload.Refused("size"));
    }

    [Fact]
    public async Task A_content_type_that_is_not_an_image_is_refused_and_a_missing_one_passes()
    {
        var html = new StubHandler(_ => Ok([1], "text/html"));
        var untyped = new StubHandler(_ =>
        {
            var response = Ok([1, 2]);
            response.Content.Headers.ContentType = null;

            return response;
        });

        var refused = await Downloader(html).DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);
        var passed = await Downloader(untyped).DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);

        refused.Should().Be(ArtDownload.Refused("type"));
        passed.Should().BeOfType<ArtDownload.Fetched>();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task An_error_status_is_a_failure_by_status(HttpStatusCode status)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status));

        var result = await Downloader(handler).DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);

        result.Should().Be(ArtDownload.Failed("status"));
    }

    [Fact]
    public async Task A_connection_error_is_a_failure_and_a_cancelled_caller_is_not_swallowed()
    {
        var broken = new StubHandler(_ => throw new HttpRequestException("connection refused"));
        var slow = new StubHandler(_ => throw new TaskCanceledException("timed out"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var unavailable = await Downloader(broken).DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);
        var timeout = await Downloader(slow).DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);
        var cancelling = () => Downloader(slow).DownloadAsync(Uri("a.png"), cancelled.Token);

        unavailable.Should().Be(ArtDownload.Failed("unavailable"));
        timeout.Should().Be(ArtDownload.Failed("timeout"));
        await cancelling.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("dropped")]
    [InlineData("io")]
    [InlineData("compressed")]
    public async Task A_body_that_breaks_off_part_way_is_a_failure_and_never_an_exception(string kind)
    {
        var handler = new StubHandler(_ => Streamed(new BreakingBodyStream(() => BodyFailure(kind))));

        var result = await Downloader(handler).DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);

        result.Should().Be(ArtDownload.Failed("unavailable"));
    }

    [Fact]
    public async Task A_redirect_whose_location_cannot_be_combined_with_the_address_is_a_failure_by_status()
    {
        var handler = new StubHandler(_ => Redirect("///"));

        var result = await Downloader(handler).DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);

        result.Should().Be(ArtDownload.Failed("status"));
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task A_body_that_stalls_fails_by_timeout_once_the_request_time_limit_has_passed()
    {
        var clock = new FakeTimeProvider();
        var body = new StallingBodyStream();
        var download = Downloader(new StubHandler(_ => Streamed(body)), time: clock)
            .DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);
        await body.Reached.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        clock.Advance(Limits.RequestTimeout - TimeSpan.FromMilliseconds(1));
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        download.IsCompleted.Should().BeFalse("the time limit has not passed yet");
        clock.Advance(TimeSpan.FromMilliseconds(1));

        (await download.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).Should().Be(ArtDownload.Failed("timeout"));
    }

    [Fact]
    public async Task Headers_that_never_come_fail_by_timeout_once_the_request_time_limit_has_passed()
    {
        var clock = new FakeTimeProvider();
        var handler = new SilentHandler();
        var download = Downloader(handler, time: clock).DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);
        await handler.Reached.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        clock.Advance(Limits.RequestTimeout);

        (await download.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).Should().Be(ArtDownload.Failed("timeout"));
    }

    [Fact]
    public async Task Cancelling_the_run_during_a_stalled_body_is_passed_on_and_not_called_a_timeout()
    {
        using var run = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var body = new StallingBodyStream();
        var download = Downloader(new StubHandler(_ => Streamed(body)), time: new FakeTimeProvider()).DownloadAsync(Uri("a.png"), run.Token);
        await body.Reached.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await run.CancelAsync();
        var waiting = () => download.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await waiting.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Two_downloads_through_a_one_second_pacer_start_at_least_a_second_apart()
    {
        var (fake, clock) = NewClock();
        var downloader = Downloader(new StubHandler(_ => Ok([1])), new ImagePacer(TimeSpan.FromSeconds(1), clock));
        await downloader.DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);

        var second = await Delayed(downloader, clock);
        fake.Advance(TimeSpan.FromMilliseconds(999));
        second.IsCompleted.Should().BeFalse();
        fake.Advance(TimeSpan.FromMilliseconds(1));

        await second.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        second.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task A_gap_configured_below_half_a_second_is_raised_to_half_a_second()
    {
        var (fake, clock) = NewClock();
        var downloader = Downloader(new StubHandler(_ => Ok([1])), new ImagePacer(TimeSpan.FromMilliseconds(100), clock));
        await downloader.DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);

        var second = await Delayed(downloader, clock);
        fake.Advance(TimeSpan.FromMilliseconds(499));
        second.IsCompleted.Should().BeFalse();
        fake.Advance(TimeSpan.FromMilliseconds(1));

        await second.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        second.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Every_hop_of_a_redirect_waits_for_its_own_turn()
    {
        var (fake, clock) = NewClock();
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath == "/1" ? Redirect("/2") : Ok([1]));
        var downloader = Downloader(handler, new ImagePacer(TimeSpan.FromSeconds(1), clock));

        var download = downloader.DownloadAsync(Uri("1"), TestContext.Current.CancellationToken);
        await clock.WaitForTimersAsync(1, TestContext.Current.CancellationToken);
        handler.Requests.Should().ContainSingle();
        fake.Advance(TimeSpan.FromSeconds(1));
        await download.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        handler.Requests.Should().HaveCount(2);
    }

    private static Uri Uri(string name) => new($"https://{Host}/{name}");

    private static ImageDownloader Downloader(HttpMessageHandler handler, IImagePacer? pacer = null, TimeProvider? time = null) =>
        new(new HttpClient(handler), Policy, Limits, pacer ?? new NoWaitPacer(), Bgg, time ?? TimeProvider.System);

    private static (FakeTimeProvider Fake, TimerCountingClock Clock) NewClock()
    {
        var fake = new FakeTimeProvider();

        return (fake, new TimerCountingClock(fake));
    }

    private static async Task<Task<ArtDownload>> Delayed(ImageDownloader downloader, TimerCountingClock clock)
    {
        var timersBefore = clock.TimerCount;
        var download = downloader.DownloadAsync(Uri("a.png"), TestContext.Current.CancellationToken);
        await clock.WaitForTimersAsync(timersBefore + 1, TestContext.Current.CancellationToken);

        return download;
    }

    private static HttpResponseMessage Ok(byte[] bytes, string contentType = "image/png")
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        return response;
    }

    private static HttpResponseMessage Chunked(byte[] bytes)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnannouncedLengthContent(bytes) };
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

        return response;
    }

    private static HttpResponseMessage Streamed(Stream body)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) };
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

        return response;
    }

    private static Exception BodyFailure(string kind) => kind switch
    {
        "dropped" => new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely."),
        "io" => new IOException("The connection was reset."),
        _ => new InvalidDataException("The compressed body is broken."),
    };

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);

        return response;
    }

    private sealed class NoWaitPacer : IImagePacer
    {
        public Task<IDisposable> WaitTurnAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IDisposable>(new NoLease());

        private sealed class NoLease : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed record Seen(Uri Uri, string? Authorization, string? UserAgent, string? Accept);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        private readonly List<Seen> _requests = [];

        public IReadOnlyList<Seen> Requests => _requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requests.Add(new Seen(
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Headers.UserAgent.ToString(),
                request.Headers.Accept.ToString()));

            return Task.FromResult(respond(request));
        }
    }

    private sealed class SilentHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Reached => _reached.Task;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _reached.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class AnnouncedLengthContent(long length) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new InvalidOperationException("The body must not be read.");

        protected override bool TryComputeLength(out long computed)
        {
            computed = length;

            return true;
        }
    }

    private sealed class UnannouncedLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long computed)
        {
            computed = 0;

            return false;
        }
    }
}
