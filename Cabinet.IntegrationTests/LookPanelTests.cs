using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Storage;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the settings page's cabinet look panel: a form that works without script, one group of choices per field with the stored
/// look checked, and a post that saves a look through the same owner handler shape as every other setting, refuses what is not
/// allowed, and needs the antiforgery token.
/// </summary>
[Trait("Category", "Owner")]
public sealed class LookPanelTests
{
    private const string SettingsPath = "/owner/settings";
    private const string LookPath = SettingsPath + "?handler=Look";
    private const string UndoPath = SettingsPath + "?handler=Undo";
    private static readonly CabinetLook EveningLook = new("legs", "oak", "painted", "evenings", "deep");

    [Fact]
    public async Task The_settings_page_offers_every_choice_of_every_field_with_the_default_look_checked()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);

        var settings = await session.GetAsync(SettingsPath);

        settings.Body.Should().Contain("Cabinet look").And.Contain("Use for everyone").And.Contain("Painted backs");
        Regex.Matches(settings.Body, "name=\"design\"").Should().HaveCount(7);
        Regex.Matches(settings.Body, "name=\"wood\"").Should().HaveCount(5);
        Regex.Matches(settings.Body, "name=\"backs\"").Should().HaveCount(1);
        Regex.Matches(settings.Body, "name=\"lights\"").Should().HaveCount(3);
        Regex.Matches(settings.Body, "name=\"depth\"").Should().HaveCount(3);
        settings.Body.Should().MatchRegex("<input[^>]*type=\"radio\"[^>]*name=\"design\"[^>]*value=\"classic\"[^>]*checked");
        settings.Body.Should().MatchRegex("<input[^>]*type=\"radio\"[^>]*name=\"wood\"[^>]*value=\"brown\"[^>]*checked");
        settings.Body.Should().MatchRegex("<input[^>]*type=\"radio\"[^>]*name=\"lights\"[^>]*value=\"off\"[^>]*checked");
        settings.Body.Should().MatchRegex("<input[^>]*type=\"radio\"[^>]*name=\"depth\"[^>]*value=\"shaded\"[^>]*checked");
        settings.Body.Should().NotMatchRegex("<input[^>]*name=\"backs\"[^>]*checked");
    }

    [Fact]
    public async Task Choosing_the_look_already_in_use_answers_that_nothing_changed_and_writes_nothing()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await session.GetAsync(SettingsPath);

        var answer = await session.PostJsonAsync(LookPath, DefaultFields(service.Revision));

        answer.Status.Should().Be(HttpStatusCode.OK);
        answer.JsonText("outcome").Should().Be("noChange");
        ((OwnerDataState.Ready)service.State).Data.Look.Should().Be(CabinetLook.Default);
        service.Revision.Should().Be(0);
    }

    [Fact]
    public async Task A_look_with_a_value_that_is_not_on_its_allow_list_is_refused_and_the_stored_look_stays_as_it_was()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await session.GetAsync(SettingsPath);

        var answer = await session.PostJsonAsync(LookPath, [Field("design", "classic"), Field("wood", "teak"), Field("backs", "wood"), Field("lights", "off"), Field("depth", "shaded"), Field("revision", "0")]);

        answer.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        answer.JsonText("outcome").Should().Be("refused");
        ((OwnerDataState.Ready)service.State).Data.Look.Should().Be(CabinetLook.Default);
        service.Revision.Should().Be(0);
    }

    [Fact]
    public async Task A_form_without_the_painted_backs_switch_stores_wood_backs()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await session.GetAsync(SettingsPath);

        var answer = await session.PostJsonAsync(LookPath, [Field("design", "library"), Field("wood", "mahogany"), Field("lights", "on"), Field("depth", "flat"), Field("revision", "0")]);

        answer.Status.Should().Be(HttpStatusCode.OK);
        ((OwnerDataState.Ready)service.State).Data.Look.Should().Be(new CabinetLook("library", "mahogany", "wood", "on", "flat"));
        ((OwnerDataState.Ready)service.State).Data.Changes[0].Text.Should().Be("Cabinet look set to Library · Mahogany · Wood backs · Lights: Always on · Depth: Flat");
    }

    [Fact]
    public async Task A_form_posted_without_script_goes_back_to_the_look_panel_with_the_words_for_the_result()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        await session.GetAsync(SettingsPath);

        var posted = await session.PostFormAsync(LookPath, DefaultFields(0));
        var landed = await session.GetAsync(posted.Location!);

        posted.Status.Should().Be(HttpStatusCode.Redirect);
        posted.Location.Should().Contain("/owner/settings").And.Contain("done=unchanged").And.EndWith("#finish");
        landed.Body.Should().MatchRegex("id=\"finish-status\"[^>]*>\\s*<span>Saved</span>");
    }

    [Fact]
    public async Task A_look_post_with_an_out_of_date_revision_saves_nothing()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await session.GetAsync(SettingsPath);
        await session.PostJsonAsync(LookPath, [Field("design", "modern"), Field("wood", "oak"), Field("backs", "wood"), Field("lights", "off"), Field("depth", "shaded"), Field("revision", "0")]);

        var stale = await session.PostJsonAsync(LookPath, [Field("design", "display"), Field("wood", "birch"), Field("backs", "wood"), Field("lights", "off"), Field("depth", "shaded"), Field("revision", "0")]);

        stale.Status.Should().Be(HttpStatusCode.Conflict);
        ((OwnerDataState.Ready)service.State).Data.Look.Design.Should().Be("modern");
    }

    [Fact]
    public async Task A_look_post_without_the_antiforgery_token_is_refused_and_nothing_is_stored()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        await session.GetAsync(SettingsPath);

        var answer = await session.PostJsonAsync(LookPath, DefaultFields(0), withToken: false);

        answer.Status.Should().Be(HttpStatusCode.BadRequest);
        factory.ServingServices.GetRequiredService<OwnerDataService>().Revision.Should().Be(0);
    }

    [Fact]
    public async Task A_published_look_adds_one_log_entry_and_the_page_says_it_was_saved_for_everyone_with_an_undo_for_that_change()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await session.GetAsync(SettingsPath);

        var posted = await session.PostFormAsync(LookPath, EveningFields(0));
        var landed = await session.GetAsync(posted.Location!);

        posted.Location.Should().Contain("done=saved").And.EndWith("#finish");
        var data = ((OwnerDataState.Ready)service.State).Data;
        data.Look.Should().Be(EveningLook);
        data.Changes.Should().ContainSingle().Which.Text.Should().Be("Cabinet look set to On legs · Oak · Painted backs · Lights: Evenings only · Depth: Deep");
        landed.Body.Should().Contain("Saved for everyone. Open pages follow within a few seconds.");
        landed.Body.Should().MatchRegex("<form[^>]*handler=Undo[^>]*>[\\s\\S]*?name=\"revision\" value=\"" + service.Revision + "\"[\\s\\S]*?Undo");
    }

    [Fact]
    public async Task The_undo_after_a_publish_puts_the_earlier_look_back_and_takes_the_log_entry_away()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await session.GetAsync(SettingsPath);
        await session.PostFormAsync(LookPath, EveningFields(0));

        var undone = await session.PostFormAsync(UndoPath, [Field("revision", service.Revision.ToString(CultureInfo.InvariantCulture))]);
        var landed = await session.GetAsync(undone.Location!);

        undone.Location.Should().Contain("done=undone").And.EndWith("#finish");
        var data = ((OwnerDataState.Ready)service.State).Data;
        data.Look.Should().Be(CabinetLook.Default);
        data.Changes.Should().BeEmpty();
        landed.Body.Should().Contain("Put back the earlier look.");
        landed.Body.Should().NotMatchRegex("handler=Undo");
    }

    [Fact]
    public async Task An_undo_after_a_newer_change_answers_that_it_cannot_be_undone_any_more_and_changes_nothing()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await session.GetAsync(SettingsPath);
        await session.PostFormAsync(LookPath, EveningFields(0));
        var firstRevision = service.Revision;
        await session.PostFormAsync(LookPath, [Field("design", "library"), Field("wood", "mahogany"), Field("lights", "off"), Field("depth", "flat"), Field("revision", firstRevision.ToString(CultureInfo.InvariantCulture))]);

        var undone = await session.PostFormAsync(UndoPath, [Field("revision", firstRevision.ToString(CultureInfo.InvariantCulture))]);
        var landed = await session.GetAsync(undone.Location!);

        undone.Location.Should().Contain("done=cant-undo");
        landed.Body.Should().Contain("That change can&#x27;t be undone any more.");
        ((OwnerDataState.Ready)service.State).Data.Look.Design.Should().Be("library");
    }

    [Fact]
    public async Task A_form_post_with_an_out_of_date_revision_stores_nothing_and_the_page_says_so()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await session.GetAsync(SettingsPath);
        await session.PostFormAsync(LookPath, EveningFields(0));

        var stale = await session.PostFormAsync(LookPath, [Field("design", "display"), Field("wood", "birch"), Field("lights", "off"), Field("depth", "flat"), Field("revision", "0")]);
        var landed = await session.GetAsync(stale.Location!);

        stale.Location.Should().Contain("done=out-of-date");
        landed.Body.Should().Contain("This page is out of date. Reload it and try again.");
        ((OwnerDataState.Ready)service.State).Data.Look.Should().Be(EveningLook);
        service.Revision.Should().Be(1);
    }

    [Theory]
    [InlineData("design", "sideways")]
    [InlineData("wood", "teak")]
    [InlineData("backs", "glossy")]
    [InlineData("lights", "dusk")]
    [InlineData("depth", "bottomless")]
    public async Task A_form_post_with_one_unknown_value_stores_nothing_and_says_so(string field, string value)
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await session.GetAsync(SettingsPath);
        var fields = EveningFields(0).Select(entry => entry.Key == field ? Field(field, value) : entry).ToArray();

        var posted = await session.PostFormAsync(LookPath, fields);
        var landed = await session.GetAsync(posted.Location!);

        posted.Location.Should().Contain("done=refused-unknown-look");
        landed.Body.Should().Contain("That look isn&#x27;t available.");
        ((OwnerDataState.Ready)service.State).Data.Look.Should().Be(CabinetLook.Default);
        service.Revision.Should().Be(0);
    }

    [Fact]
    public async Task A_form_post_without_the_antiforgery_token_is_refused_and_stores_nothing()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await session.GetAsync(SettingsPath);

        var refused = await session.PostFormAsync(LookPath, EveningFields(0), withToken: false);
        var undo = await session.PostFormAsync(UndoPath, [Field("revision", "0")], withToken: false);

        refused.Status.Should().Be(HttpStatusCode.BadRequest);
        undo.Status.Should().Be(HttpStatusCode.BadRequest);
        ((OwnerDataState.Ready)service.State).Data.Look.Should().Be(CabinetLook.Default);
        service.Revision.Should().Be(0);
    }

    [Fact]
    public async Task A_look_that_cannot_be_stored_says_nothing_changed_and_keeps_the_look_as_it_was()
    {
        using var storage = new TemporaryDirectory();
        var store = new SometimesFailingStore(new OwnerDataStore(storage.FullPath, NullLogger<OwnerDataStore>.Instance));
        await using var factory = new CabinetWebApplicationFactory(
            new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath },
            services => services.AddSingleton<IOwnerDataStore>(store));
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await session.GetAsync(SettingsPath);
        store.FailSaves = true;

        var posted = await session.PostFormAsync(LookPath, EveningFields(0));
        var landed = await session.GetAsync(posted.Location!);

        posted.Location.Should().Contain("done=refused-save-failed");
        landed.Body.Should().Contain("Couldn&#x27;t save that. Nothing changed. Try again.");
        ((OwnerDataState.Ready)service.State).Data.Look.Should().Be(CabinetLook.Default);
    }

    private static CabinetWebApplicationFactory NewFactory() =>
        SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());

    private static KeyValuePair<string, string>[] DefaultFields(long revision) =>
    [
        Field("design", "classic"),
        Field("wood", "brown"),
        Field("backs", "wood"),
        Field("lights", "off"),
        Field("depth", "shaded"),
        Field("revision", revision.ToString(CultureInfo.InvariantCulture)),
    ];

    private static KeyValuePair<string, string>[] EveningFields(long revision) =>
    [
        Field("design", "legs"),
        Field("wood", "oak"),
        Field("backs", "painted"),
        Field("lights", "evenings"),
        Field("depth", "deep"),
        Field("revision", revision.ToString(CultureInfo.InvariantCulture)),
    ];

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);
}
