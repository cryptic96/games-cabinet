using System.Globalization;
using System.Net;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Storage;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the site-wide look reaches the pages that show the cabinet: it is read from the owner data, written as five attributes
/// on the root element of the cabinet on both ports from the first response, absent from owner pages that show no cabinet, and a
/// stored value that is not on its allow-list, or data that cannot be read, is never written into a page as it is.
/// </summary>
[Trait("Category", "Owner")]
public sealed class LookSlotTests
{
    private const string DefaultAttributes = "data-design=\"classic\" data-wood=\"brown\" data-backs=\"wood\" data-lights=\"off\" data-depth=\"shaded\">";
    private const string SettingsPath = "/owner/settings";
    private const string LookPath = SettingsPath + "?handler=Look";
    private static readonly string[] AttributeNames = ["data-design=", "data-wood=", "data-backs=", "data-lights=", "data-depth="];
    private static readonly string[] OwnerPagesWithoutCabinet = [SettingsPath, "/owner/pictures", "/owner/locations"];

    [Fact]
    public async Task The_cabinet_on_both_ports_carries_the_default_look_on_the_root_element_and_no_finish_class()
    {
        await using var factory = NewFactory();
        using var publicClient = factory.CreatePublicClient();
        using var session = new OwnerSession(factory);

        var publicPage = await publicClient.GetStringAsync("/", TestContext.Current.CancellationToken);
        var ownerCabinet = await session.GetAsync("/");

        publicPage.Should().MatchRegex("<html lang=\"[a-z-]+\" " + DefaultAttributes);
        ownerCabinet.Status.Should().Be(HttpStatusCode.OK);
        ownerCabinet.Body.Should().MatchRegex("<html lang=\"[a-z-]+\" " + DefaultAttributes);
        publicPage.Should().NotContain("finish-");
        ownerCabinet.Body.Should().NotContain("finish-");
    }

    [Fact]
    public async Task Owner_pages_that_show_no_cabinet_carry_none_of_the_five_attributes()
    {
        await using var factory = NewFactory();
        using var session = new OwnerSession(factory);

        foreach (var path in OwnerPagesWithoutCabinet)
        {
            var page = await session.GetAsync(path);

            page.Status.Should().Be(HttpStatusCode.OK);
            AttributeNames.Should().OnlyContain(name => !page.Body.Contains(name, StringComparison.Ordinal), path);
            page.Body.Should().NotContain("finish-classic");
        }
    }

    [Fact]
    public async Task A_look_posted_on_the_settings_page_is_stored_and_reaches_the_cabinet_on_both_ports()
    {
        await using var factory = NewFactory();
        using var publicClient = factory.CreatePublicClient();
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        await session.GetAsync(SettingsPath);

        var posted = await session.PostFormAsync(
            LookPath,
            [Field("design", "legs"), Field("wood", "oak"), Field("backs", "painted"), Field("lights", "evenings"), Field("depth", "deep"), Field("revision", service.Revision.ToString(CultureInfo.InvariantCulture))]);
        var publicPage = await publicClient.GetStringAsync("/", TestContext.Current.CancellationToken);
        var ownerCabinet = await session.GetAsync("/");

        posted.Status.Should().Be(HttpStatusCode.Redirect);
        ((OwnerDataState.Ready)service.State).Data.Look.Should().Be(new CabinetLook("legs", "oak", "painted", "evenings", "deep"));
        const string Expected = "data-design=\"legs\" data-wood=\"oak\" data-backs=\"painted\" data-lights=\"evenings\" data-depth=\"deep\">";
        publicPage.Should().Contain(Expected);
        ownerCabinet.Body.Should().Contain(Expected);
    }

    [Fact]
    public async Task A_stored_wood_that_is_not_on_the_allow_list_renders_as_warm_brown_and_is_never_written_into_a_page()
    {
        using var storage = new TemporaryDirectory();
        OwnerDataSeed.Write(storage.FullPath, OwnerData.Empty with { Revision = 1, Look = new CabinetLook("legs", "walnut\" onload=\"x", "wood", "off", "shaded") });
        await using var factory = NewFactory(storage);
        using var publicClient = factory.CreatePublicClient();
        using var session = new OwnerSession(factory);

        var publicPage = await publicClient.GetStringAsync("/", TestContext.Current.CancellationToken);
        var ownerCabinet = await session.GetAsync("/");
        var settings = await session.GetAsync(SettingsPath);

        publicPage.Should().Contain("data-design=\"legs\" data-wood=\"brown\"");
        ownerCabinet.Body.Should().Contain("data-design=\"legs\" data-wood=\"brown\"");
        new[] { publicPage, ownerCabinet.Body, settings.Body }.Should().OnlyContain(body => !body.Contains("onload=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Owner_data_that_cannot_be_read_still_renders_the_default_look_on_both_ports()
    {
        using var storage = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(storage.FullPath, OwnerDataStore.FileName), "this is not owner data", TestContext.Current.CancellationToken);
        await using var factory = NewFactory(storage);
        using var publicClient = factory.CreatePublicClient();
        using var session = new OwnerSession(factory);

        var publicPage = await publicClient.GetStringAsync("/", TestContext.Current.CancellationToken);
        var ownerCabinet = await session.GetAsync("/");

        factory.ServingServices.GetRequiredService<OwnerDataService>().State.Should().BeOfType<OwnerDataState.Unreadable>();
        publicPage.Should().Contain(DefaultAttributes);
        ownerCabinet.Body.Should().Contain(DefaultAttributes);
    }

    [Fact]
    public async Task The_settings_panel_shows_the_stored_values_checked_and_a_fallback_field_shows_its_default_checked()
    {
        using var storage = new TemporaryDirectory();
        OwnerDataSeed.Write(storage.FullPath, OwnerData.Empty with { Revision = 1, Look = new CabinetLook("library", "mahogany\" onload=\"x", "painted", "evenings", "deep") });
        await using var factory = NewFactory(storage);
        using var session = new OwnerSession(factory);

        var settings = await session.GetAsync(SettingsPath);

        settings.Body.Should().MatchRegex("<input[^>]*type=\"radio\"[^>]*name=\"design\"[^>]*value=\"library\"[^>]*checked");
        settings.Body.Should().MatchRegex("<input[^>]*type=\"radio\"[^>]*name=\"wood\"[^>]*value=\"brown\"[^>]*checked");
        settings.Body.Should().MatchRegex("<input[^>]*type=\"checkbox\"[^>]*name=\"backs\"[^>]*value=\"painted\"[^>]*checked");
        settings.Body.Should().MatchRegex("<input[^>]*type=\"radio\"[^>]*name=\"lights\"[^>]*value=\"evenings\"[^>]*checked");
        settings.Body.Should().MatchRegex("<input[^>]*type=\"radio\"[^>]*name=\"depth\"[^>]*value=\"deep\"[^>]*checked");
        WebUtility.HtmlDecode(settings.Body).Should().Contain("Visitors see: Library · Warm brown · Painted backs · Lights: Evenings only · Depth: Deep");
    }

    private static CabinetWebApplicationFactory NewFactory(TemporaryDirectory? storage = null) =>
        SyncHarness.CreateFactory(
            new ScriptedBggHandler(),
            SyncHarness.NewClock(),
            storage is null ? null : new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);
}
