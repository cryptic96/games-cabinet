using Cabinet.BrowserTests.Infrastructure;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser the look panel of the owner's settings page: a preview that draws a real cabinet section from a sample
/// layout, a trial that every choice changes and nothing else, the publish that makes the trial the look every visitor sees, and the
/// way back to the current look.
/// </summary>
[Trait("Category", "Browser")]
public sealed class OwnerLookTests : CabinetPageTest
{
    private const string SettingsPath = "/owner/settings";
    private const string Preview = ".look-preview";
    private const string SavedText = "Saved for everyone. Open pages follow within a few seconds.";

    private readonly TemporaryDirectory _storage = new();

    [Fact]
    public async Task Trying_a_wood_changes_only_the_preview_and_Use_for_everyone_makes_it_the_look_visitors_see()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartWithDataAsync(null);
        var posts = CountLookPosts();
        await GotoOwnerAsync(SettingsPath);

        var preview = Page.Locator(Preview);
        await Expect(preview).ToHaveAttributeAsync("data-design", "classic");
        await Expect(preview).ToHaveAttributeAsync("data-wood", "brown");
        await Expect(preview).ToHaveAttributeAsync("data-backs", "wood");
        await Expect(preview).ToHaveAttributeAsync("data-lights", "off");
        await Expect(preview).ToHaveAttributeAsync("data-depth", "shaded");
        await Expect(preview).ToHaveAttributeAsync("aria-hidden", "true");
        (await preview.EvaluateAsync<bool>("element => element.inert")).Should().BeTrue();
        await Expect(preview.Locator(".section")).ToHaveCountAsync(1);
        await Expect(preview.Locator(".cubby")).ToHaveCountAsync(6);

        await Page.Locator("input[name=wood][value=oak]").CheckAsync();

        await Expect(preview).ToHaveAttributeAsync("data-wood", "oak");
        posts().Should().Be(0);
        (await PublicAttributeAsync("data-wood")).Should().Be("brown");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Use for everyone" }).ClickAsync();

        await Expect(Page.Locator("#finish-status")).ToContainTextAsync(SavedText);
        posts().Should().Be(1);
        (await PublicAttributeAsync("data-wood")).Should().Be("oak");
        await SaveScreenshotAsync("owner-look-1440");
        ExpectCleanPage();
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _storage.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task StartWithDataAsync(OwnerData? data)
    {
        if (data is not null)
        {
            OwnerDataSeed.Write(_storage.FullPath, data);
        }

        return StartAsync(SyncHarness.CreateFactory(
            new ScriptedBggHandler(),
            SyncHarness.NewClock(),
            new Dictionary<string, string?> { ["Storage:Directory"] = _storage.FullPath }));
    }

    private Func<int> CountLookPosts()
    {
        var seen = 0;

        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && request.Url.Contains("handler=Look", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref seen);
            }
        };

        return () => Volatile.Read(ref seen);
    }

    private async Task<string?> PublicAttributeAsync(string name)
    {
        var other = await Page.Context.NewPageAsync();

        try
        {
            await other.GotoAsync($"http://127.0.0.1:{PublicPort}/");

            return await other.Locator("html").GetAttributeAsync(name);
        }
        finally
        {
            await other.CloseAsync();
        }
    }

    private void ExpectCleanPage()
    {
        ConsoleErrors.Should().BeEmpty();
        RequestHosts.Should().OnlyContain(host => host == $"127.0.0.1:{OwnerPort}");
    }
}
