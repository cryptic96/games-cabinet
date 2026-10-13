using System.Threading.Channels;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.Repository.Images;
using Cabinet.Service.Sync;

namespace Cabinet.Service.Owner;

/// <summary>
/// Keeps the pictures the owner linked alive in the background. When the host starts, once an hour, when something asks for
/// it and after every owner change, it looks at every stored link and fetches again each one that is due. A link with no
/// usable processed copy is due at once: one that was never fetched, one that came back with a restore or an undo, one whose
/// files are missing from the art directory and one measured by an older analysis than the current one. A link with a usable
/// copy is checked again <see cref="RecheckEvery"/> after the host started or after its last check, so a restart never
/// re-checks everything at once. It fetches one link at a time through <see cref="OwnerLinkFetcher"/>, writes the files first
/// and only then stores the record through the single owner-data writer, as a system write that adds no log line, counts as
/// no change and does not move the revision, but is announced so every visitor's card shows the picture. A record is stored
/// only when the link's address is still the one that was fetched, so an address the owner changed in the meantime is never
/// overwritten.
/// A link that fails to fetch keeps its stored record and its files, so the copy keeps showing; the failure is recorded
/// through <see cref="LinkHealth"/>, which alone says when a link is dead. A link that has failed is tried again a day later,
/// so the second failed check can mark it dead, and once it is dead it is tried once a week; a link with no copy is also tried
/// again when something asks for it. A picture that was fetched but could not be stored counts as a failed check, so a full disk
/// does not make every pass fetch it again. One success clears every failure. It runs only when the background switch the hourly sync
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

    /// <summary>How long a link with a usable copy, or a dead link, waits between two checks.</summary>
    public static readonly TimeSpan RecheckEvery = TimeSpan.FromDays(7);

    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    private readonly Dictionary<int, DateTimeOffset> _lastChecked = [];
    private DateTimeOffset _startedAt;
    private int _asked;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.BackgroundEnabled)
        {
            return;
        }

        _startedAt = time.GetUtcNow();
        owner.LinkWorkRequested += Ask;
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
            owner.LinkWorkRequested -= Ask;
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

    private void Ask()
    {
        Volatile.Write(ref _asked, 1);
        Wake();
    }

    private async Task RunPassAsync(CancellationToken stoppingToken)
    {
        try
        {
            var stored = 0;
            var failed = 0;
            var asked = Interlocked.Exchange(ref _asked, 0) == 1;

            foreach (var (gameId, link) in LinksDue(asked))
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

    private List<(int GameId, PictureLink Link)> LinksDue(bool asked)
    {
        if (owner.State is not OwnerDataState.Ready ready)
        {
            return [];
        }

        var now = time.GetUtcNow();

        return [.. ready.Data.Games
            .Where(game => game.Picture?.Link is { } link && IsDue(game.GameId, link, asked, now))
            .OrderBy(game => game.GameId)
            .Select(game => (game.GameId, game.Picture!.Link!))];
    }

    private bool IsDue(int gameId, PictureLink link, bool asked, DateTimeOffset now)
    {
        var usable = HasUsableCopy(link);
        DateTimeOffset? lastChecked = _lastChecked.TryGetValue(gameId, out var checkedAt) ? checkedAt : null;

        if (link.FailedChecks == 0)
        {
            return !usable || now - (lastChecked ?? _startedAt) >= RecheckEvery;
        }

        if (asked && !usable)
        {
            return true;
        }

        var wait = LinkHealth.IsDead(link, now) ? RecheckEvery : LinkHealth.DeadAfterSpan;
        var since = lastChecked ?? (usable ? _startedAt : null);

        return since is not { } moment || now - moment >= wait;
    }

    private bool HasUsableCopy(PictureLink link) =>
        link.Record is { Status: ImageStatus.Ok, Files: { Count: > 0 } files, Features: not null } record
        && record.AnalysisVersion == ArtProcessor.AnalysisVersion
        && files.All(file => cache.Has(file.Name));

    private async Task<bool> FetchOneAsync(int gameId, PictureLink link, CancellationToken stoppingToken)
    {
        var attemptedAt = time.GetUtcNow();

        if (await fetcher.CheckAsync(link.Url, stoppingToken) is not LinkCheck.Ready ready)
        {
            _lastChecked[gameId] = attemptedAt;
            await owner.MutateSystemAsync(data => WithLink(data, gameId, link.Url, current => LinkHealth.AfterFailure(current, attemptedAt)), stoppingToken);

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
            _lastChecked[gameId] = attemptedAt;
            await owner.MutateSystemAsync(data => WithLink(data, gameId, link.Url, current => LinkHealth.AfterFailure(current, attemptedAt)), stoppingToken);

            return false;
        }

        _lastChecked[gameId] = attemptedAt;
        await owner.MutateSystemAsync(
            data => WithLink(data, gameId, link.Url, current => LinkHealth.AfterSuccess(current, ready.Link.Record)),
            stoppingToken,
            announce: true);

        return true;
    }

    private static OwnerData? WithLink(OwnerData data, int gameId, string url, Func<PictureLink, PictureLink> change)
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
                    ? game with { Picture = game.Picture with { Link = change(game.Picture.Link) } }
                    : game),
            ],
        };
    }
}
