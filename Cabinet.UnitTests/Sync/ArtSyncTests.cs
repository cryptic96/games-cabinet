using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.FakeBgg;
using Cabinet.Repository.Images;
using Cabinet.Service.Sync;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Sync;

/// <summary>
/// Proves that one picture going wrong in any way is recorded as failed for that picture alone, with the usual retry time,
/// and that the step goes on with the next picture; only cancelling the run stops it.
/// </summary>
[Trait("Category", "Images")]
public sealed class ArtSyncTests : IDisposable
{
    private const string Broken = "https://cf.example.org/broken.png";
    private const string Working = "https://cf.example.org/working.png";

    private static readonly DateTimeOffset Start = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly TemporaryDirectory _storage = new();
    private readonly FakeTimeProvider _clock = new(Start);

    [Theory]
    [InlineData("dropped")]
    [InlineData("io")]
    [InlineData("compressed")]
    [InlineData("address")]
    [InlineData("library")]
    [InlineData("state")]
    [InlineData("stray-cancellation")]
    public async Task A_picture_whose_fetch_throws_gets_a_failed_record_and_the_next_picture_is_still_fetched(string kind)
    {
        var source = new ScriptedSource(uri => uri.AbsoluteUri == Broken ? throw Failure(kind) : Picture());

        var result = await Sync(source).RunAsync(Snapshot(), Start.AddMinutes(6), _ => Task.CompletedTask, TestContext.Current.CancellationToken);

        source.Requested.Should().Equal(Broken, Working);
        result.Failed.Should().Be(1);
        result.Stored.Should().Be(1);
        result.Snapshot.Images![Broken].Should().Be(new ImageRecord(Broken, ImageStatus.Failed, Start));
        result.Snapshot.Images[Working].Status.Should().Be(ImageStatus.Ok);
    }

    [Fact]
    public async Task A_picture_that_threw_waits_for_the_retry_time_like_any_failed_picture()
    {
        var source = new ScriptedSource(uri => uri.AbsoluteUri == Broken ? throw new IOException("The connection was reset.") : Picture());
        var sync = Sync(source);
        var first = await sync.RunAsync(Snapshot(), Start.AddMinutes(6), _ => Task.CompletedTask, TestContext.Current.CancellationToken);

        _clock.Advance(TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1));
        await sync.RunAsync(first.Snapshot, _clock.GetUtcNow().AddMinutes(6), _ => Task.CompletedTask, TestContext.Current.CancellationToken);
        var beforeRetry = source.Requested.Count;
        _clock.Advance(TimeSpan.FromSeconds(1));
        await sync.RunAsync(first.Snapshot, _clock.GetUtcNow().AddMinutes(6), _ => Task.CompletedTask, TestContext.Current.CancellationToken);

        beforeRetry.Should().Be(2, "inside the retry time neither picture is asked for again");
        source.Requested.Should().Equal(Broken, Working, Broken);
    }

    [Fact]
    public async Task Cancelling_the_run_stops_the_step_instead_of_being_recorded()
    {
        using var run = new CancellationTokenSource();
        var source = new ScriptedSource(uri =>
        {
            run.Cancel();
            run.Token.ThrowIfCancellationRequested();

            return Picture();
        });

        var act = () => Sync(source).RunAsync(Snapshot(), Start.AddMinutes(6), _ => Task.CompletedTask, run.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        source.Requested.Should().Equal(Broken);
    }

    [Fact]
    public async Task The_record_the_sync_stores_is_the_one_the_shared_builder_gives_for_the_same_picture()
    {
        var options = ImageSettings.FromConfiguration(new ConfigurationBuilder().Build(), new TestEnvironment("Production"));
        var bytes = SyntheticArt.Encode(SyntheticArtKind.FlatCover);
        var done = (ArtProcessing.Done)ArtProcessor.Process(bytes, options.Limits);
        var expected = ArtRecords.FromProcessed(Working, Start, done);
        var source = new ScriptedSource(_ => ArtDownload.Downloaded(bytes));

        var result = await Sync(source).RunAsync(Snapshot(), Start.AddMinutes(6), _ => Task.CompletedTask, TestContext.Current.CancellationToken);

        result.Snapshot.Images![Working].Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void The_shared_builder_lists_the_files_widest_first_and_carries_every_measurement_of_the_picture()
    {
        var options = ImageSettings.FromConfiguration(new ConfigurationBuilder().Build(), new TestEnvironment("Production"));
        var done = (ArtProcessing.Done)ArtProcessor.Process(SyntheticArt.Encode(SyntheticArtKind.FlatCover), options.Limits);

        var record = ArtRecords.FromProcessed(Working, Start, done);

        record.SourceUrl.Should().Be(Working);
        record.Status.Should().Be(ImageStatus.Ok);
        record.AttemptedAtUtc.Should().Be(Start);
        record.Files!.Select(file => file.Width).Should().BeInDescendingOrder();
        record.Files!.Select(file => file.Name).Should().BeEquivalentTo(done.Variants.Select(variant => variant.Name));
        record.Features.Should().Be(done.Facts.Features);
        record.MainColour.Should().Be(done.Facts.Main.ToHex());
        record.Colour.Should().Be(SpineColour.PairFor(done.Facts.Main));
        record.Edges.Should().Be(new ArtEdges(done.Facts.Top.ToHex(), done.Facts.Right.ToHex(), done.Facts.Bottom.ToHex(), done.Facts.Left.ToHex()));
        record.AnalysisVersion.Should().Be(ArtProcessor.AnalysisVersion);
    }

    /// <inheritdoc />
    public void Dispose() => _storage.Dispose();

    private static Exception Failure(string kind) => kind switch
    {
        "dropped" => new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely."),
        "io" => new IOException("The connection was reset."),
        "compressed" => new InvalidDataException("The compressed body is broken."),
        "address" => new UriFormatException("The redirect target is not an address."),
        "library" => new Exception("Unable to allocate pixels for the bitmap."),
        "state" => new InvalidOperationException("Something unforeseen went wrong."),
        _ => new TaskCanceledException("A timeout that is not the run's own cancellation."),
    };

    private static ArtDownload Picture() => ArtDownload.Downloaded(SyntheticArt.Encode(SyntheticArtKind.FlatCover));

    private static CollectionSnapshot Snapshot() =>
        new(
            CollectionSnapshot.CurrentSchemaVersion,
            Start,
            [Item(1, Broken), Item(2, Working)]);

    private static SnapshotItem Item(long collectionId, string versionImage) =>
        new(collectionId, (int)collectionId + 100, $"Invented Game {collectionId}", ItemKind.Base, null, null, null, versionImage);

    private ArtSync Sync(IArtSource source) =>
        new(
            () => source,
            new ArtCache(_storage.FullPath),
            ImageSettings.FromConfiguration(new ConfigurationBuilder().Build(), new TestEnvironment("Production")),
            _clock,
            NullLogger<ArtSync>.Instance);

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
