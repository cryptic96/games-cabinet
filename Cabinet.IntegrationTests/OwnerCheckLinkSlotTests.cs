using System.Globalization;
using System.Net;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Images;
using Cabinet.Service.Collection;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves that the owner's own check of a picture link has a fetch slot of its own: while the background work is held up on
/// a slow picture link, the check of another game's working link answers with its preview at once, the check sends no
/// credentials, and two checks in a row keep the picture gap on their own slot.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerCheckLinkSlotTests
{
    private const string CheckPath = "/owner/pictures?handler=CheckLink";
    private const string SlowUrl = "https://example.org/owner/slow-picture.png";
    private const string FineUrl = "https://example.org/owner/fine-picture.png";
    private const string SecondFineUrl = "https://example.org/owner/second-fine-picture.png";

    private static readonly TimeSpan AnswerWithin = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Gap = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task A_check_of_a_working_link_answers_with_its_preview_while_the_background_work_is_held_up_on_a_slow_link()
    {
        await using var host = await SlotHost.StartAsync();
        await host.WaitUntilHeldAsync();

        var check = host.CheckAsync(FineUrl);
        var finished = await Task.WhenAny(check, Task.Delay(AnswerWithin, TestContext.Current.CancellationToken));

        finished.Should().BeSameAs(check, "the check must not wait behind the background work");
        var reply = await check;
        reply.Status.Should().Be(HttpStatusCode.OK);
        reply.JsonText("outcome").Should().Be("ready");
        host.Images.Requests.Where(request => request.Uri.AbsoluteUri == FineUrl).Should().ContainSingle()
            .Which.Should().Match<RecordedImageRequest>(request => request.AuthorizationScheme == null && !request.HasCookie);

        host.SlowRequest.Release();
        await host.WaitUntilStoredAsync();
        host.SlowLinkRecord!.Files.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Two_checks_in_a_row_keep_the_picture_gap_on_the_slot_of_the_owner_check()
    {
        await using var host = await SlotHost.StartAsync(holdSlowLink: false);
        var first = await host.CheckAsync(FineUrl);

        var second = host.CheckAsync(SecondFineUrl);
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        var requestedBeforeGap = host.Images.Requests.Count(request => request.Uri.AbsoluteUri == SecondFineUrl);
        host.Clock.Advance(Gap);
        var reply = await second.WaitAsync(AnswerWithin, TestContext.Current.CancellationToken);

        first.JsonText("outcome").Should().Be("ready");
        requestedBeforeGap.Should().Be(0);
        reply.JsonText("outcome").Should().Be("ready");
        host.Images.Requests.Where(request => request.Uri.AbsoluteUri is FineUrl or SecondFineUrl).Should().HaveCount(2).And.OnlyContain(
            request => request.AuthorizationScheme == null && request.AuthorizationParameter == null && !request.HasCookie);
    }

    private sealed class SlotHost : IAsyncDisposable
    {
        private readonly TemporaryDirectory _storage;
        private readonly int _checkedGameId;
        private readonly int _slowGameId;

        private SlotHost(
            TemporaryDirectory storage,
            CabinetWebApplicationFactory factory,
            ScriptedImageHandler images,
            FakeTimeProvider clock,
            HeldRequest slowRequest,
            int slowGameId,
            int checkedGameId)
        {
            _storage = storage;
            Factory = factory;
            Images = images;
            Clock = clock;
            SlowRequest = slowRequest;
            _slowGameId = slowGameId;
            _checkedGameId = checkedGameId;
            Session = new OwnerSession(factory);
        }

        public CabinetWebApplicationFactory Factory { get; }

        public ScriptedImageHandler Images { get; }

        public FakeTimeProvider Clock { get; }

        public HeldRequest SlowRequest { get; }

        public OwnerSession Session { get; }

        public ImageRecord? SlowLinkRecord => Factory.ServingServices.GetRequiredService<OwnerDataService>().State is OwnerDataState.Ready ready
            ? ready.Data.Games.FirstOrDefault(game => game.GameId == _slowGameId)?.Picture?.Link?.Record
            : null;

        public static async Task<SlotHost> StartAsync(bool holdSlowLink = true)
        {
            var storage = new TemporaryDirectory();
            var items = SyntheticBggCollection.Create(5);
            var owned = items.Where(candidate => candidate.Owned && !candidate.IsExpansion).ToList();
            var slowGame = owned[0];
            var checkedGame = owned[1];
            var images = new ScriptedImageHandler()
                .Serve(SlowUrl, SyntheticArt.Encode(SyntheticArtKind.CoverColouredField))
                .Serve(FineUrl, SyntheticArt.Encode(SyntheticArtKind.FlatCover))
                .Serve(SecondFineUrl, SyntheticArt.Encode(SyntheticArtKind.CoverColouredField));
            var held = images.Hold(SlowUrl);

            if (!holdSlowLink)
            {
                held.Release();
            }

            var link = new PictureLink(SlowUrl, null, 0, null);
            var seeded = new OwnerGame(slowGame.ObjectId, null, null, new PictureSetting(PictureChoice.Link, true, null, null, link));
            OwnerDataSeed.Write(storage.FullPath, OwnerData.Empty with { Revision = 1, Games = [seeded] });

            var clock = SyncHarness.NewClock();
            var factory = SyncHarness.CreateFactory(
                ScriptedBggHandler.ForCollection(items),
                clock,
                new Dictionary<string, string?>
                {
                    ["Images:AllowedHosts"] = "example.org",
                    ["Images:MaxDownloadsPerRun"] = "1000",
                    ["Storage:Directory"] = storage.FullPath,
                    ["Sync:BackgroundEnabled"] = "true",
                },
                images,
                services =>
                {
                    services.AddSingleton<IOwnerCheckPacer>(new OwnerCheckPacer(Gap, clock));

                    if (holdSlowLink)
                    {
                        services.AddSingleton<IImagePacer>(new ImagePacer(Gap, clock));
                    }
                });

            var host = new SlotHost(storage, factory, images, clock, held, slowGame.ObjectId, checkedGame.ObjectId);
            using var publicClient = factory.CreatePublicClient();
            using var press = await publicClient.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
            press.StatusCode.Should().Be(HttpStatusCode.Accepted);
            await SyncHarness.WaitUntil(() => Task.FromResult(factory.ServingServices.GetRequiredService<CollectionStore>().Current.Snapshot is not null));

            return host;
        }

        private long CheckedEntry => Factory.ServingServices.GetRequiredService<CollectionStore>().Current.Snapshot!.Items
            .Where(item => item.GameId == _checkedGameId).Min(item => item.CollectionId);

        public Task<OwnerReply> CheckAsync(string link) =>
            Session.PostJsonAsync(
                CheckPath,
                [
                    new KeyValuePair<string, string>("gameId", _checkedGameId.ToString(CultureInfo.InvariantCulture)),
                    new KeyValuePair<string, string>("collectionId", CheckedEntry.ToString(CultureInfo.InvariantCulture)),
                    new KeyValuePair<string, string>("link", link),
                ]);

        public Task WaitUntilHeldAsync() => WaitAdvancingAsync(() => SlowRequest.Reached.IsCompleted);

        public Task WaitUntilStoredAsync() => WaitAdvancingAsync(() => SlowLinkRecord is not null);

        private async Task WaitAdvancingAsync(Func<bool> condition)
        {
            await SyncHarness.WaitUntil(() =>
            {
                if (condition())
                {
                    return Task.FromResult(true);
                }

                Clock.Advance(Gap);

                return Task.FromResult(false);
            });
        }

        public async ValueTask DisposeAsync()
        {
            SlowRequest.Release();
            Session.Dispose();
            await Factory.DisposeAsync();
            _storage.Dispose();
        }
    }
}
