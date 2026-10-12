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
/// Proves the settings page's cabinet finish panel: a single checked tile with a drawn swatch and its note, and a post that
/// saves a finish through the same owner handler shape as every other setting, refuses what is not allowed, and needs the
/// antiforgery token.
/// </summary>
[Trait("Category", "Owner")]
public sealed class FinishPanelTests
{
    private const string SettingsPath = "/owner/settings";
    private const string FinishPath = SettingsPath + "?handler=Finish";

    [Fact]
    public async Task The_settings_page_offers_one_checked_classic_tile_with_a_drawn_swatch_and_the_note()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);

        var settings = await session.GetAsync(SettingsPath);

        settings.Body.Should().Contain("The look of the cabinet for every visitor.")
            .And.Contain("Warm wood, as it is today.")
            .And.Contain("Classic is the only finish for now.");
        Regex.Matches(settings.Body, "name=\"finish\"").Should().HaveCount(1);
        settings.Body.Should().MatchRegex("<input[^>]*type=\"radio\"[^>]*name=\"finish\"[^>]*value=\"classic\"[^>]*checked");
        settings.Body.Should().Contain("class=\"finish-swatch\" aria-hidden=\"true\"");
        settings.Body.Should().NotContain("finish-extras");
    }

    [Fact]
    public async Task Choosing_classic_again_answers_that_nothing_changed_and_writes_nothing()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await session.GetAsync(SettingsPath);

        var answer = await session.PostJsonAsync(FinishPath, [Field("finish", "classic"), Field("revision", service.Revision.ToString(CultureInfo.InvariantCulture))]);

        answer.Status.Should().Be(HttpStatusCode.OK);
        answer.JsonText("outcome").Should().Be("noChange");
        ((OwnerDataState.Ready)service.State).Data.Finish.Should().Be("classic");
        service.Revision.Should().Be(0);
    }

    [Fact]
    public async Task A_finish_that_is_not_on_the_allow_list_is_refused_and_the_stored_finish_stays_classic()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await session.GetAsync(SettingsPath);

        var answer = await session.PostJsonAsync(FinishPath, [Field("finish", "walnut"), Field("revision", "0")]);

        answer.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        answer.JsonText("outcome").Should().Be("refused");
        ((OwnerDataState.Ready)service.State).Data.Finish.Should().Be("classic");
    }

    [Fact]
    public async Task A_form_posted_without_script_goes_back_to_the_finish_panel_with_the_words_for_the_result()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        await session.GetAsync(SettingsPath);

        var posted = await session.PostFormAsync(FinishPath, [Field("finish", "classic"), Field("revision", "0")]);
        var landed = await session.GetAsync(posted.Location!);

        posted.Status.Should().Be(HttpStatusCode.Redirect);
        posted.Location.Should().Contain("/owner/settings").And.Contain("done=unchanged").And.EndWith("#finish");
        landed.Body.Should().MatchRegex("id=\"finish-status\"[^>]*>\\s*<span>Saved</span>");
    }

    [Fact]
    public async Task A_finish_post_without_the_antiforgery_token_is_refused_and_nothing_is_stored()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);
        await session.GetAsync(SettingsPath);

        var answer = await session.PostJsonAsync(FinishPath, [Field("finish", "classic"), Field("revision", "0")], withToken: false);

        answer.Status.Should().Be(HttpStatusCode.BadRequest);
        factory.ServingServices.GetRequiredService<OwnerDataService>().Revision.Should().Be(0);
    }

    private static CabinetWebApplicationFactory NewFactory() =>
        SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);
}
