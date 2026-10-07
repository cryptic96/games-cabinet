using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Proves the served page carries the sync button and note the page scripts drive, and that the scripts and the press answer fit together.</summary>
[Trait("Category", "Sync")]
public partial class SyncButtonTests
{
    [Fact]
    public async Task The_page_carries_one_hidden_button_that_is_never_natively_disabled()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var buttons = SyncButtonElement().Matches(html);

        buttons.Should().HaveCount(1);
        buttons[0].Value.Should().Contain(" hidden");
        buttons[0].Value.Should().Contain("aria-disabled=\"false\"");
        buttons[0].Value.Should().NotMatchRegex(@"\sdisabled[\s=>]");
    }

    [Fact]
    public async Task The_note_exists_empty_as_the_only_live_region_of_the_block()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var note = SyncNoteElement().Match(html);

        note.Success.Should().BeTrue();
        note.Groups["attributes"].Value.Should().Contain("role=\"status\"").And.Contain("aria-atomic=\"true\"");
        note.Groups["text"].Value.Should().BeEmpty();
        Regex.Matches(html, "role=\"status\"").Count.Should().Be(1);
    }

    [Fact]
    public async Task The_module_graph_from_the_page_reaches_the_sync_and_status_scripts()
    {
        await using var factory = SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var entry = PageModuleSource().Match(html).Groups["src"].Value;

        entry.Should().NotBeEmpty("the page loads its entry script as a module");

        var cabinet = await GetScript(client, entry);
        var sync = await GetScript(client, "/js/sync.js");

        cabinet.Should().Contain("./sync.js");
        sync.Should().Contain("./status.js");
        await GetScript(client, "/js/status.js");
    }

    [Fact]
    public async Task An_accepted_press_opens_a_window_ten_minutes_after_the_server_time_it_reports()
    {
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3));
        await using var factory = SyncHarness.CreateFactory(handler, SyncHarness.NewClock());
        using var client = factory.CreatePublicClient();

        using var press = await client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await press.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var status = body.RootElement.GetProperty("status");

        press.StatusCode.Should().Be(HttpStatusCode.Accepted);
        status.GetProperty("cooldownEndsUtc").GetDateTimeOffset()
            .Should().Be(status.GetProperty("serverTimeUtc").GetDateTimeOffset() + TimeSpan.FromMinutes(10));
        await SyncHarness.WaitForRunToEnd(client);
    }

    private static async Task<string> GetScript(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        response.ShouldHaveMediaType("text/javascript", path);

        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    [GeneratedRegex("<button[^>]*class=\"sync-button\"[^>]*>")]
    private static partial Regex SyncButtonElement();

    [GeneratedRegex("<p id=\"sync-note\"(?<attributes>[^>]*)>(?<text>[^<]*)</p>")]
    private static partial Regex SyncNoteElement();

    [GeneratedRegex("<script type=\"module\" src=\"(?<src>[^\"]+)\"")]
    private static partial Regex PageModuleSource();
}
