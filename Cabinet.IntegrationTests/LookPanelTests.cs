using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

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

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);
}
