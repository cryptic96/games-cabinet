using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.FakeBgg;
using Cabinet.Repository.Images;
using Cabinet.Repository.Storage;
using Cabinet.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.UnitTests.Review;

/// <summary>Builds a stored collection of invented games with processed synthetic pictures in a temporary state directory.</summary>
internal sealed class ReviewFixture : IDisposable
{
    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly ArtLimits Limits = new(12_000_000, 36_000_000);

    private readonly TemporaryDirectory _state = new();
    private readonly ArtCache _cache;
    private readonly List<SnapshotItem> _items = [];
    private readonly Dictionary<string, ImageRecord> _images = [];
    private readonly Dictionary<int, GameDetails> _games = [];

    public ReviewFixture() => _cache = new ArtCache(_state.FullPath);

    public string StatePath => _state.FullPath;

    public string ArtPath => _cache.Path;

    public void Dispose() => _state.Dispose();

    public string Picture(string name, SyntheticArtKind kind)
    {
        var url = $"https://cf.example.org/{name}.jpg";

        if (kind == SyntheticArtKind.Undecodable)
        {
            _images[url] = new ImageRecord(url, ImageStatus.Undecodable, Moment);

            return url;
        }

        var done = (ArtProcessing.Done)ArtProcessor.Process(SyntheticArt.Encode(kind), Limits);

        foreach (var variant in done.Variants)
        {
            _cache.Write(variant);
        }

        _images[url] = new ImageRecord(
            url,
            ImageStatus.Ok,
            Moment,
            [.. done.Variants.Select(variant => new ArtFile(variant.Width, variant.Height, variant.Name))],
            done.Facts.Features,
            done.Facts.Main.ToHex(),
            SpineColour.PairFor(done.Facts.Main),
            new ArtEdges(done.Facts.Top.ToHex(), done.Facts.Right.ToHex(), done.Facts.Bottom.ToHex(), done.Facts.Left.ToHex()),
            ArtProcessor.AnalysisVersion);

        return url;
    }

    public string MeasuredPicture(string name, SyntheticArtKind kind, ArtFeatures features)
    {
        var url = Picture(name, kind);
        _images[url] = _images[url] with { Features = features };

        return url;
    }

    public string FailedPicture(string name)
    {
        var url = $"https://cf.example.org/{name}.jpg";
        _images[url] = new ImageRecord(url, ImageStatus.Failed, Moment);

        return url;
    }

    public void Game(
        int id,
        string title,
        string? versionUrl,
        string? mainUrl,
        VersionDimensions? dimensions = null,
        bool withDetails = true)
    {
        _items.Add(new SnapshotItem(id, id, title, ItemKind.Base, 2020, dimensions, null, versionUrl, mainUrl));

        if (withDetails)
        {
            _games[id] = new GameDetails(Moment, 2, 4, 60, null, null, 10, 2.5, null, null, [], [], [], mainUrl);
        }
    }

    public CollectionSnapshot Snapshot() =>
        new(CollectionSnapshot.CurrentSchemaVersion, Moment, [.. _items], new Dictionary<string, ImageRecord>(_images), new Dictionary<int, GameDetails>(_games));

    public void Save() => new SnapshotStore(_state.FullPath, NullLogger<SnapshotStore>.Instance).Save(Snapshot());
}
