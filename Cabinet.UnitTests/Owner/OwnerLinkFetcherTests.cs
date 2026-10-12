using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.Repository.Images;
using Cabinet.Service.Owner;
using Cabinet.Service.Sync;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using SkiaSharp;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves that an address the owner entered is classified before anything is fetched, that only a direct picture on the
/// allowlisted host is ever requested, and that every way the fetch or the picture can go wrong comes back as one precise
/// reason.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerLinkFetcherTests
{
    private const string Host = ImageSettings.DefaultHost;
    private const string Address = $"https://{Host}/owner/linked.png";

    private static readonly DateTimeOffset Start = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_good_picture_comes_back_processed_with_a_record_made_the_way_the_sync_makes_it()
    {
        var bytes = SyntheticArt.Encode(SyntheticArtKind.FlatCover);
        var source = new ScriptedSource(_ => ArtDownload.Downloaded(bytes));

        var check = await Fetcher(source).CheckAsync($"  {Address}  ", TestContext.Current.CancellationToken);

        var ready = check.Should().BeOfType<LinkCheck.Ready>().Subject;
        var done = (ArtProcessing.Done)ArtProcessor.Process(bytes, Options().Limits);
        ready.Link.Url.Should().Be(Address);
        ready.Link.Variants.Select(variant => variant.Width).Should().Equal(480, 240);
        ready.Link.Variants.Select(variant => variant.Name).Should().BeEquivalentTo(done.Variants.Select(variant => variant.Name));
        ready.Link.Record.AnalysisVersion.Should().Be(ArtProcessor.AnalysisVersion);
        ready.Link.Record.Should().BeEquivalentTo(ArtRecords.FromProcessed(Address, Start, done));
        source.Requested.Should().Equal(Address);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_text_is_blank_and_nothing_is_requested(string? text)
    {
        var source = new ScriptedSource(_ => ArtDownload.Downloaded([]));

        var check = await Fetcher(source).CheckAsync(text, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.Blank));
        source.Requested.Should().BeEmpty();
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://cf.geekdo-images.com/x.png")]
    public async Task Text_that_is_not_a_web_address_is_refused_and_nothing_is_requested(string text)
    {
        var source = new ScriptedSource(_ => ArtDownload.Downloaded([]));

        var check = await Fetcher(source).CheckAsync(text, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.NotAWebAddress));
        source.Requested.Should().BeEmpty();
    }

    [Fact]
    public async Task An_address_longer_than_the_limit_is_not_a_web_address_and_nothing_is_requested()
    {
        var source = new ScriptedSource(_ => ArtDownload.Downloaded([]));
        var prefix = $"https://{Host}/";
        var longAddress = prefix + new string('a', ArtUrl.MaxLength - prefix.Length + 1);

        var check = await Fetcher(source).CheckAsync(longAddress, TestContext.Current.CancellationToken);

        longAddress.Length.Should().Be(ArtUrl.MaxLength + 1);
        check.Should().Be(new LinkCheck.Failed(LinkProblem.NotAWebAddress));
        source.Requested.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://boardgamegeek.com/image/123/x")]
    [InlineData("https://www.boardgamegeek.com/image/123/x")]
    [InlineData("https://BoardGameGeek.com/image/123/x")]
    [InlineData("https://cf.boardgamegeek.com/a.png")]
    [InlineData("http://boardgamegeek.com/boardgame/1/x/images")]
    [InlineData("https://boardgamegeek.com./image/1")]
    public async Task A_link_to_a_page_of_the_game_site_is_a_page_link_and_nothing_is_requested(string text)
    {
        var source = new ScriptedSource(_ => ArtDownload.Downloaded([]));

        var check = await Fetcher(source).CheckAsync(text, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.PageLink));
        source.Requested.Should().BeEmpty();
    }

    [Fact]
    public async Task A_protocol_relative_link_to_a_page_of_the_game_site_is_a_page_link_and_nothing_is_requested()
    {
        var source = new ScriptedSource(_ => ArtDownload.Downloaded([]));

        var check = await Fetcher(source).CheckAsync(new string('/', 2) + "boardgamegeek.com/image/1", TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.PageLink));
        source.Requested.Should().BeEmpty();
    }

    [Fact]
    public async Task A_page_link_is_refused_even_when_the_game_site_is_listed_as_a_picture_host()
    {
        var source = new ScriptedSource(_ => ArtDownload.Downloaded([]));
        var options = Options() with { AllowedHosts = new HashSet<string> { "boardgamegeek.com" } };

        var check = await Fetcher(source, options).CheckAsync("https://boardgamegeek.com/image/9/x", TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.PageLink));
        source.Requested.Should().BeEmpty();
    }

    [Fact]
    public async Task Every_address_that_is_not_a_direct_picture_on_the_picture_host_is_another_host_and_nothing_is_requested()
    {
        var source = new ScriptedSource(_ => ArtDownload.Downloaded([]));
        var withUserInfo = "https://user" + "@" + Host + "/a.png";
        string[] refused =
        [
            "https://img.example.org/a.png",
            $"http://{Host}/a.png",
            $"https://{Host}:8443/a.png",
            withUserInfo,
            $"https://{Host}.evil.example/a.png",
            $"https://{Host}./a.png",
            $"https://not-{Host}/a.png",
        ];

        foreach (var text in refused)
        {
            var check = await Fetcher(source).CheckAsync(text, TestContext.Current.CancellationToken);

            check.Should().Be(new LinkCheck.Failed(LinkProblem.OtherHost), text);
        }

        source.Requested.Should().BeEmpty();
    }

    [Fact]
    public void The_name_of_the_picture_host_is_available_for_the_message()
    {
        Fetcher(new ScriptedSource(_ => ArtDownload.Downloaded([]))).AllowedHost.Should().Be(Host);
    }

    [Fact]
    public async Task An_address_in_another_letter_case_on_the_picture_host_is_fetched()
    {
        var bytes = SyntheticArt.Encode(SyntheticArtKind.FlatCover);
        var source = new ScriptedSource(_ => ArtDownload.Downloaded(bytes));

        var check = await Fetcher(source).CheckAsync($"https://{Host.ToUpperInvariant()}/owner/linked.png", TestContext.Current.CancellationToken);

        check.Should().BeOfType<LinkCheck.Ready>();
        source.Requested.Should().ContainSingle();
    }

    [Fact]
    public async Task A_404_answer_is_not_found()
    {
        var source = new ScriptedSource(_ => ArtDownload.Failed("status", 404));

        var check = await Fetcher(source).CheckAsync(Address, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.NotFound));
    }

    [Theory]
    [InlineData("status", 500)]
    [InlineData("status", null)]
    [InlineData("unavailable", null)]
    [InlineData("timeout", null)]
    public async Task Any_other_miss_is_a_failed_fetch(string reason, int? status)
    {
        var source = new ScriptedSource(_ => ArtDownload.Failed(reason, status));

        var check = await Fetcher(source).CheckAsync(Address, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.FetchFailed));
    }

    [Fact]
    public async Task A_redirect_the_source_refuses_is_a_failed_fetch()
    {
        var source = new ScriptedSource(_ => ArtDownload.Refused("redirect"));

        var check = await Fetcher(source).CheckAsync(Address, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.FetchFailed));
    }

    [Fact]
    public async Task A_refused_size_is_too_large_with_the_announced_size_and_the_limit()
    {
        var options = Options();
        var source = new ScriptedSource(_ => ArtDownload.Refused("size", 20_000_000));

        var check = await Fetcher(source, options).CheckAsync(Address, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.TooLarge, 20_000_000, options.MaxBytes));
    }

    [Fact]
    public async Task A_refused_size_that_was_not_announced_is_too_large_with_only_the_limit()
    {
        var options = Options();
        var source = new ScriptedSource(_ => ArtDownload.Refused("size"));

        var check = await Fetcher(source, options).CheckAsync(Address, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.TooLarge, null, options.MaxBytes));
    }

    [Fact]
    public async Task A_refused_content_type_is_not_a_picture()
    {
        var source = new ScriptedSource(_ => ArtDownload.Refused("type"));

        var check = await Fetcher(source).CheckAsync(Address, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.NotAPicture));
    }

    [Fact]
    public async Task Bytes_that_do_not_decode_are_not_a_picture()
    {
        var source = new ScriptedSource(_ => ArtDownload.Downloaded(SyntheticArt.Encode(SyntheticArtKind.Undecodable)));

        var check = await Fetcher(source).CheckAsync(Address, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.NotAPicture));
    }

    [Fact]
    public async Task A_picture_over_the_pixel_cap_has_too_many_pixels()
    {
        var bytes = SyntheticArt.Encode(SyntheticArtKind.FlatCover);
        var source = new ScriptedSource(_ => ArtDownload.Downloaded(bytes));

        var check = await Fetcher(source, Options() with { MaxPixels = 1000 }).CheckAsync(Address, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.TooManyPixels));
    }

    [Fact]
    public async Task A_one_by_twelve_picture_has_an_extreme_shape()
    {
        var source = new ScriptedSource(_ => ArtDownload.Downloaded(Png(1, 12)));

        var check = await Fetcher(source).CheckAsync(Address, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.ExtremeShape));
    }

    [Fact]
    public async Task A_source_that_never_answers_fails_the_fetch_once_the_fifteen_seconds_have_passed()
    {
        var clock = new FakeTimeProvider(Start);
        var source = new ScriptedSource(_ => ArtDownload.Downloaded([]), stall: true);
        var check = Fetcher(source, clock: clock).CheckAsync(Address, TestContext.Current.CancellationToken);
        await source.Reached.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        clock.Advance(OwnerLinkFetcher.CheckTimeout - TimeSpan.FromMilliseconds(1));
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        check.IsCompleted.Should().BeFalse("the limit has not passed yet");
        clock.Advance(TimeSpan.FromMilliseconds(1));

        (await check.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).Should().Be(new LinkCheck.Failed(LinkProblem.FetchFailed));
        OwnerLinkFetcher.CheckTimeout.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task A_source_that_throws_fails_the_fetch_and_does_not_throw()
    {
        var source = new ScriptedSource(_ => throw new IOException("The connection was reset."));

        var check = await Fetcher(source).CheckAsync(Address, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(LinkProblem.FetchFailed));
    }

    [Fact]
    public async Task Cancelling_stops_the_check_instead_of_failing_it()
    {
        using var run = new CancellationTokenSource();
        var source = new ScriptedSource(_ =>
        {
            run.Cancel();
            run.Token.ThrowIfCancellationRequested();

            return ArtDownload.Downloaded([]);
        });

        var act = () => Fetcher(source).CheckAsync(Address, run.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static ImageOptions Options() =>
        ImageSettings.FromConfiguration(new ConfigurationBuilder().Build(), new TestEnvironment("Production"));

    private static OwnerLinkFetcher Fetcher(IArtSource source, ImageOptions? options = null, FakeTimeProvider? clock = null) =>
        new(() => source, options ?? Options(), clock ?? new FakeTimeProvider(Start));

    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(170, 60, 40));
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }

    private sealed class ScriptedSource(Func<Uri, ArtDownload> answer, bool stall = false) : IArtSource
    {
        private readonly List<string> _requested = [];
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<string> Requested => _requested;

        public Task Reached => _reached.Task;

        public async Task<ArtDownload> DownloadAsync(Uri uri, CancellationToken cancellationToken)
        {
            _requested.Add(uri.AbsoluteUri);
            _reached.TrySetResult();

            if (stall)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            await Task.Yield();

            return answer(uri);
        }
    }
}
