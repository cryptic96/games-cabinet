using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.Repository.Images;
using Cabinet.Service.Owner;
using Cabinet.Service.Sync;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves that an address the owner entered is fetched through the picture source under the sync's rules and comes back
/// either as a processed picture with its record or as one category word that says why not, and that nothing is ever asked
/// of the source for an address that is not an address.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerLinkFetcherTests
{
    private const string Address = "https://cf.example.org/owner/linked.png";

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
        ready.Link.Variants.Select(variant => variant.Name).Should().BeEquivalentTo(done.Variants.Select(variant => variant.Name));
        ready.Link.Record.Should().BeEquivalentTo(ArtRecords.FromProcessed(Address, Start, done));
        source.Requested.Should().Equal(Address);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not an address")]
    [InlineData("ftp://cf.example.org/linked.png")]
    public async Task Text_that_is_not_a_web_address_fails_as_an_address_and_nothing_is_requested(string? text)
    {
        var source = new ScriptedSource(_ => ArtDownload.Downloaded([]));

        var check = await Fetcher(source).CheckAsync(text, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed("address"));
        source.Requested.Should().BeEmpty();
    }

    [Theory]
    [InlineData("host")]
    [InlineData("redirect")]
    [InlineData("type")]
    [InlineData("size")]
    public async Task A_refusal_of_the_source_comes_back_as_its_category_word(string reason)
    {
        var source = new ScriptedSource(_ => ArtDownload.Refused(reason));

        var check = await Fetcher(source).CheckAsync(Address, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(reason));
    }

    [Theory]
    [InlineData("status")]
    [InlineData("unavailable")]
    [InlineData("timeout")]
    public async Task A_miss_of_the_source_comes_back_as_its_category_word(string reason)
    {
        var source = new ScriptedSource(_ => ArtDownload.Failed(reason));

        var check = await Fetcher(source).CheckAsync(Address, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed(reason));
    }

    [Fact]
    public async Task Bytes_that_are_not_a_picture_fail_as_unreadable()
    {
        var source = new ScriptedSource(_ => ArtDownload.Downloaded(SyntheticArt.Encode(SyntheticArtKind.Undecodable)));

        var check = await Fetcher(source).CheckAsync(Address, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed("unreadable"));
    }

    [Fact]
    public async Task A_source_that_throws_fails_the_check_and_does_not_throw()
    {
        var source = new ScriptedSource(_ => throw new IOException("The connection was reset."));

        var check = await Fetcher(source).CheckAsync(Address, TestContext.Current.CancellationToken);

        check.Should().Be(new LinkCheck.Failed("failed"));
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

    private static OwnerLinkFetcher Fetcher(IArtSource source) =>
        new(() => source, Options(), new FakeTimeProvider(Start));

    private sealed class ScriptedSource(Func<Uri, ArtDownload> answer) : IArtSource
    {
        private readonly List<string> _requested = [];

        public IReadOnlyList<string> Requested => _requested;

        public async Task<ArtDownload> DownloadAsync(Uri uri, CancellationToken cancellationToken)
        {
            _requested.Add(uri.AbsoluteUri);
            await Task.Yield();

            return answer(uri);
        }
    }
}
