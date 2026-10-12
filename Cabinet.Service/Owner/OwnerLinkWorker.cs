using System.Threading.Channels;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.Repository.Images;
using Cabinet.Service.Sync;

namespace Cabinet.Service.Owner;

/// <summary>
/// Keeps the pictures the owner linked alive in the background. When the host starts, once an hour, when something asks for
/// it and after every owner change, it looks at every stored link and fetches again each one that has no usable processed
/// copy: a link that was never fetched, one that came back with a restore or an undo, and one whose files are missing from the
/// art directory. It fetches one link at a time through <see cref="OwnerLinkFetcher"/>, writes the files first and only then
/// stores the record through the single owner-data writer, as a system write that adds no log line, counts as no change and
/// does not move the revision, but is announced so every visitor's card shows the picture. A record is stored only when the
/// link's address is still the one that was fetched, so an address the owner changed in the meantime is never overwritten. A
/// link that fails is left as it is and tried again at the next turn. It runs only when the background switch the hourly sync
/// obeys is on, and it logs counts only, never an address or a title.
/// </summary>
/// <param name="owner">The single writer of the owner data.</param>
/// <param name="fetcher">Fetches and processes a linked picture.</param>
/// <param name="cache">Where the resized pictures are stored.</param>
/// <param name="options">The sync's timing rules, whose background switch decides whether the worker runs.</param>
/// <param name="time">The clock the hourly turn is measured on.</param>
/// <param name="logger">Receives counts and the exception type of an unforeseen failure.</param>
public sealed class OwnerLinkWorker(
    OwnerDataService owner,
    OwnerLinkFetcher fetcher,
    ArtCache cache,
    SyncOptions options,
    TimeProvider time,
    ILogger<OwnerLinkWorker> logger) : BackgroundService
{
    /// <summary>How long the worker waits between two looks at the stored links when nothing wakes it.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.BackgroundEnabled)
        {
            return;
        }

        owner.LinkWorkRequested += Wake;
        owner.Changed += Wake;

        try
        {
            await RunPassAsync(stoppingToken);
            await WaitAndRunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            owner.LinkWorkRequested -= Wake;
            owner.Changed -= Wake;
        }
    }

    private async Task WaitAndRunAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        var tick = timer.WaitForNextTickAsync(stoppingToken).AsTask();
        var woken = _wake.Reader.WaitToReadAsync(stoppingToken).AsTask();

        while (true)
        {
            var finished = await Task.WhenAny(tick, woken);

            if (finished == tick)
            {
                if (!await tick)
                {
                    return;
                }

                tick = timer.WaitForNextTickAsync(stoppingToken).AsTask();
            }
            else
            {
                if (!await woken)
                {
                    return;
                }

                while (_wake.Reader.TryRead(out _))
                {
                }

                woken = _wake.Reader.WaitToReadAsync(stoppingToken).AsTask();
            }

            await RunPassAsync(stoppingToken);
        }
    }

    private void Wake() => _wake.Writer.TryWrite(true);

    private async Task RunPassAsync(CancellationToken stoppingToken)
    {
        try
        {
            var stored = 0;
            var failed = 0;

            foreach (var (gameId, link) in LinksNeedingACopy())
            {
                if (await FetchOneAsync(gameId, link, stoppingToken))
                {
                    stored++;
                }
                else
                {
                    failed++;
                }
            }

            if (stored + failed > 0)
            {
                logger.LogInformation("Linked pictures: {Stored} stored, {Failed} not fetched.", stored, failed);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning("Linked pictures stopped early: {ExceptionType}", exception.GetType().Name);
        }
    }

    private List<(int GameId, PictureLink Link)> LinksNeedingACopy() =>
        owner.State is OwnerDataState.Ready ready
            ? [.. ready.Data.Games
                .Where(game => game.Picture?.Link is { } link && !HasUsableCopy(link))
                .OrderBy(game => game.GameId)
                .Select(game => (game.GameId, game.Picture!.Link!))]
            : [];

    private bool HasUsableCopy(PictureLink link) =>
        link.Record is { Status: ImageStatus.Ok, Files: { Count: > 0 } files, Features: not null } record
        && record.AnalysisVersion == ArtProcessor.AnalysisVersion
        && files.All(file => cache.Has(file.Name));

    private async Task<bool> FetchOneAsync(int gameId, PictureLink link, CancellationToken stoppingToken)
    {
        if (await fetcher.CheckAsync(link.Url, stoppingToken) is not LinkCheck.Ready ready)
        {
            return false;
        }

        try
        {
            foreach (var variant in ready.Link.Variants)
            {
                cache.Write(variant);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("A linked picture could not be stored: {ExceptionType}", exception.GetType().Name);

            return false;
        }

        await owner.MutateSystemAsync(data => WithRecord(data, gameId, link.Url, ready.Link.Record), stoppingToken, announce: true);

        return true;
    }

    private static OwnerData? WithRecord(OwnerData data, int gameId, string url, ImageRecord record)
    {
        var stillThere = data.Games.Any(game => game.GameId == gameId && game.Picture?.Link?.Url == url);

        if (!stillThere)
        {
            return null;
        }

        return data with
        {
            Games =
            [
                .. data.Games.Select(game => game.GameId == gameId && game.Picture?.Link?.Url == url
                    ? game with { Picture = game.Picture with { Link = game.Picture.Link with { Record = record, FailedChecks = 0, FailedSinceUtc = null } } }
                    : game),
            ],
        };
    }
}
