using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;
using Microsoft.Playwright;

namespace Cabinet.BrowserTests;

/// <summary>
/// Proves in a real browser that a visitor whose browser prefers Dutch gets the Dutch page, can switch both ways, keeps the choice
/// across reloads, never sees the language in the address, and never sees a game title changed.
/// </summary>
[Trait("Category", "Browser")]
public sealed class LanguageSwitchTests : CabinetPageTest
{
    private const string FieldSeparator = "\u001f";
    private const string DutchLongNote = "Collectie bijgewerkt. Het kan een paar minuten duren voordat BGG recente wijzigingen laat zien.";
    private const string DutchLongStale = "Je ziet de laatste synchronisatie van 9 oktober 2026 om 14:32 CEST. Een veel kleinere collectie van BGG wacht op bevestiging bij de volgende synchronisatie.";
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    /// <summary>Makes the browser ask for Dutch, as a visitor with Dutch language settings does.</summary>
    public override BrowserNewContextOptions ContextOptions()
    {
        var options = base.ContextOptions();
        options.Locale = "nl-NL";

        return options;
    }

    [Fact]
    public async Task A_browser_that_prefers_Dutch_gets_the_Dutch_page_with_Dutch_status_text()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);

        await GotoCabinetAsync("/");
        await SaveScreenshotAsync("language-desktop-nl");

        (await Page.Locator("html").GetAttributeAsync("lang")).Should().Be("nl");
        (await Page.Locator("h1").InnerTextAsync()).Should().Be("Spellenkast");
        (await Page.Locator(".sync-time-text").InnerTextAsync()).Should().Be("Nog niet gesynchroniseerd");
        (await Page.Locator(".sync-button").InnerTextAsync()).Should().Be("Nu synchroniseren");
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task Switching_to_English_reloads_in_English_and_the_choice_survives_a_reload_in_one_safe_cookie()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync("/");
        var scriptRequests = new List<string>();
        Page.Request += (_, request) =>
        {
            if (request.ResourceType == "fetch" && request.Url.EndsWith("/language/en", StringComparison.Ordinal))
            {
                scriptRequests.Add(request.Url);
            }
        };

        await Page.Locator(".lang-toggle a[lang=en]").ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "en");
        scriptRequests.Should().ContainSingle("the script asked for the switch itself instead of following the link");
        await Page.ReloadAsync();

        (await Page.Locator("html").GetAttributeAsync("lang")).Should().Be("en");
        (await Page.Locator("h1").InnerTextAsync()).Should().Be("Games Cabinet");
        (await Page.Locator(".sync-time-text").InnerTextAsync()).Should().Be("Not synced yet");

        var cookies = await Context.CookiesAsync();
        var language = cookies.Should().ContainSingle().Subject;
        language.Name.Should().Be("lang");
        language.HttpOnly.Should().BeTrue();
        language.SameSite.Should().Be(SameSiteAttribute.Lax);

        await Page.Locator(".lang-toggle a[lang=nl]").ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "nl");
        (await Page.Locator("h1").InnerTextAsync()).Should().Be("Spellenkast");
    }

    [Fact]
    public async Task Switching_keeps_the_address_free_of_the_language_and_keeps_a_chosen_sample()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync("/?sample=65");

        await Page.Locator(".lang-toggle a[lang=en]").ClickAsync();
        await Expect(Page.Locator("html")).ToHaveAttributeAsync("lang", "en");
        await Page.WaitForSelectorAsync("#cabinet .placement");

        Page.Url.Should().EndWith("/?sample=65");
        Page.Url.Should().NotContainEquivalentOf("lang");
    }

    [Fact]
    public async Task Game_titles_are_identical_on_the_English_and_the_Dutch_page()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);

        await GotoCabinetAsync("/?sample=65");
        var dutch = await ReadPlacementsAsync();

        await GotoCabinetAsync("/language/en?sample=65");
        var english = await ReadPlacementsAsync();

        (await Page.Locator("html").GetAttributeAsync("lang")).Should().Be("en");
        dutch.Should().NotBeEmpty();
        english.Select(entry => (entry.Id, entry.Label)).Should().Equal(dutch.Select(entry => (entry.Id, entry.Label)));

        var bareTitles = english.Where(entry => entry.Title == entry.Label).ToList();
        bareTitles.Should().NotBeEmpty();
        bareTitles.Should().OnlyContain(entry => dutch.Single(other => other.Id == entry.Id).Title == entry.Title);
        ConsoleErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task The_longer_Dutch_notes_wrap_on_a_phone_and_leave_the_sync_button_in_view_without_sideways_scrolling()
    {
        await Page.SetViewportSizeAsync(390, 844);
        await StartAsync(PrototypeOn);
        await GotoCabinetAsync("/");

        await Page.EvaluateAsync(
            "([note, stale]) => { document.getElementById('sync-note').textContent = note; const el = document.querySelector('.sync-stale'); el.textContent = stale; el.hidden = false; }",
            new[] { DutchLongNote, DutchLongStale });
        await SaveScreenshotAsync("language-phone-nl");

        var scrollWidth = await Page.EvaluateAsync<int>("document.documentElement.scrollWidth");
        var button = await Page.Locator(".sync-button").BoundingBoxAsync();

        scrollWidth.Should().BeLessThanOrEqualTo(390);
        button.Should().NotBeNull();
        button!.X.Should().BeGreaterThanOrEqualTo(0);
        (button.X + button.Width).Should().BeLessThanOrEqualTo(390);
        ConsoleErrors.Should().BeEmpty();
    }

    private async Task<List<PlacementText>> ReadPlacementsAsync()
    {
        var rows = await Page.Locator(".placement").EvaluateAllAsync<string[]>(
            $"els => els.map(e => [e.dataset.entryId, e.dataset.kind, e.querySelector('.placement-label')?.textContent ?? '', e.title].join('{FieldSeparator}'))");

        return rows
            .Select(row => row.Split(FieldSeparator))
            .Select(parts => new PlacementText($"{parts[0]}:{parts[1]}", parts[2], parts[3]))
            .ToList();
    }

    private sealed record PlacementText(string Id, string Label, string Title);
}
