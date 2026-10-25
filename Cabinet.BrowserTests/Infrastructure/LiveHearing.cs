using Cabinet.Service.Live;
using Cabinet.Service.Owner;
using Cabinet.Service.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests.Infrastructure;

/// <summary>
/// Lets a browser test wait for the live channel by proving it works, never by waiting the time a fast machine needs. A page that
/// draws many games keeps its main thread busy for a long time on a slow machine, and its live connection can drop and come back in
/// that time; a server that still counts the connection does not prove the page can hear it. So before a test publishes the change
/// it is about to check, the server sends the page the status it already shows, again until the page has received it, which proves a
/// push gets through from end to end right then. The quiet period after an owner's save is then let pass on the host's clock, again
/// if the change has not arrived after a while, a bounded number of times, and the end state is read with retrying assertions.
/// </summary>
internal static class LiveHearing
{
    /// <summary>How long the page may take to hear one push before the same status is sent again.</summary>
    public const float HearingStepTimeoutMs = 5000;

    /// <summary>How many times the status the page shows is sent before the page must have heard one.</summary>
    public const int MostHearingRounds = 8;

    /// <summary>How long a change may take to arrive after one quiet period has passed on the host's clock.</summary>
    public const float FollowStepTimeoutMs = 5000;

    /// <summary>How many times the quiet period is let pass before the change must have arrived.</summary>
    public const int MostQuietPeriods = 4;

    /// <summary>
    /// Counts, in the page, the statuses the live channel delivers. It wraps the browser client of the live channel as the page loads
    /// it, adds one more listener to every connection the page builds, and changes nothing the page does.
    /// </summary>
    public const string Spy = """
        (() => {
          const rec = { built: 0, heard: 0 };
          window.__live = rec;
          let client;
          Object.defineProperty(window, 'signalR', {
            configurable: true,
            get() {
              return client;
            },
            set(value) {
              client = value;
              const build = value.HubConnectionBuilder.prototype.build;
              value.HubConnectionBuilder.prototype.build = function () {
                const connection = build.call(this);
                rec.built += 1;
                connection.on('statusChanged', () => {
                  rec.heard += 1;
                });
                return connection;
              };
            },
          });
        })();
        """;

    /// <summary>The script that reads how many statuses the page has heard so far.</summary>
    public const string HeardCountScript = "() => window.__live.heard";

    /// <summary>
    /// Sends the page the status it already shows, again each time the page has not heard it after a while, a bounded number of times,
    /// until the page has received one. The page shows nothing new for it: the look and the collection are the ones on screen.
    /// </summary>
    /// <param name="page">The visitor's page, with the live spy installed.</param>
    /// <param name="services">The services of the running host.</param>
    public static async Task WaitUntilThePageHearsAPushAsync(IPage page, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(services);

        var live = services.GetRequiredService<ILiveNotifier>();
        var status = services.GetRequiredService<SyncStatusService>();
        var heard = await page.EvaluateAsync<int>(HeardCountScript);

        for (var round = 1; round <= MostHearingRounds; round++)
        {
            await live.PublishAsync(status.Current(), TestContext.Current.CancellationToken);

            try
            {
                await page.WaitForFunctionAsync("(heard) => window.__live.heard > heard", heard, new PageWaitForFunctionOptions { Timeout = HearingStepTimeoutMs });

                return;
            }
            catch (TimeoutException) when (round < MostHearingRounds)
            {
            }
        }
    }

    /// <summary>
    /// Proves the page has heard a push and has then had two frames to act on it, which is the moment to assert that it chose to do
    /// nothing. Nothing here waits for an amount of time: the page's own push and its own frames are the events waited for.
    /// </summary>
    /// <param name="page">The visitor's page, with the live spy installed.</param>
    /// <param name="services">The services of the running host.</param>
    public static async Task WaitUntilThePageHasHeardAPushAndActedAsync(IPage page, IServiceProvider services)
    {
        await WaitUntilThePageHearsAPushAsync(page, services);
        await page.EvaluateAsync("() => new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    }

    /// <summary>
    /// Lets the quiet period after the owner's save pass on the host's clock until the condition holds on the page, a bounded number of
    /// times, so a host that has not yet scheduled its notice when the clock first moves still sends it.
    /// </summary>
    /// <param name="page">The visitor's page.</param>
    /// <param name="clock">The host's clock.</param>
    /// <param name="condition">A script that tells whether the change has arrived.</param>
    public static async Task LetTheQuietPeriodPassUntilAsync(IPage page, FakeTimeProvider clock, string condition)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(clock);

        for (var round = 1; round <= MostQuietPeriods; round++)
        {
            clock.Advance(OwnerChangeNotifier.QuietPeriod);

            try
            {
                await page.WaitForFunctionAsync(condition, null, new PageWaitForFunctionOptions { Timeout = FollowStepTimeoutMs });

                return;
            }
            catch (TimeoutException) when (round < MostQuietPeriods)
            {
            }
        }
    }
}
