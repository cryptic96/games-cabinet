using System.Globalization;
using System.Net;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the posts behind the owner's list of locations: what a script and a form each get back for a rename, a move and a
/// delete, that a delete asks first when games would become not placed, and that an identifier the request carries is looked up
/// in the owner data and never echoed or followed.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerLocationListPostTests
{
    private const string LocationsPath = "/owner/locations";

    [Fact]
    public async Task Adding_as_json_answers_with_the_identifier_of_the_new_location_and_renaming_keeps_it()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);

        var added = await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "Cabinet A"), Field("revision", "0")]);
        var id = added.JsonText("id");
        var renamed = await session.PostJsonAsync($"{LocationsPath}?handler=Rename", [Field("id", id), Field("name", "Shelf B"), Field("revision", "1")]);

        added.Status.Should().Be(HttpStatusCode.OK);
        id.Should().NotBeNullOrWhiteSpace();
        renamed.Status.Should().Be(HttpStatusCode.OK);
        renamed.JsonText("outcome").Should().Be("saved");
        renamed.JsonText("status").Should().Be("location-renamed");
        renamed.Json.GetProperty("canUndo").GetBoolean().Should().BeTrue();
        Locations(factory).Should().Equal(new OwnerLocation(id, "Shelf B"));
    }

    [Fact]
    public async Task A_rename_to_a_taken_name_is_refused_and_the_same_name_changes_nothing()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);
        var first = (await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "Cabinet A"), Field("revision", "0")])).JsonText("id");
        await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "Shelf B"), Field("revision", "1")]);

        var taken = await session.PostJsonAsync($"{LocationsPath}?handler=Rename", [Field("id", first), Field("name", "shelf b"), Field("revision", "2")]);
        var same = await session.PostJsonAsync($"{LocationsPath}?handler=Rename", [Field("id", first), Field("name", "Cabinet A"), Field("revision", "2")]);

        taken.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        taken.JsonText("status").Should().Be("name-taken");
        same.Status.Should().Be(HttpStatusCode.OK);
        same.JsonText("outcome").Should().Be("noChange");
        factory.ServingServices.GetRequiredService<OwnerDataService>().Revision.Should().Be(2);
    }

    [Fact]
    public async Task Moving_swaps_neighbours_and_the_ends_change_nothing()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);
        var first = (await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "Cabinet A"), Field("revision", "0")])).JsonText("id");
        await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "Shelf B"), Field("revision", "1")]);

        var down = await session.PostJsonAsync($"{LocationsPath}?handler=MoveDown", [Field("id", first), Field("revision", "2")]);
        var past = await session.PostJsonAsync($"{LocationsPath}?handler=MoveDown", [Field("id", first), Field("revision", "3")]);
        var up = await session.PostJsonAsync($"{LocationsPath}?handler=MoveUp", [Field("id", first), Field("revision", "3")]);

        down.JsonText("status").Should().Be("location-moved");
        past.JsonText("outcome").Should().Be("noChange");
        up.JsonText("outcome").Should().Be("saved");
        Locations(factory).Select(location => location.Name).Should().Equal("Cabinet A", "Shelf B");
    }

    [Fact]
    public async Task An_unknown_identifier_is_refused_by_every_handler_and_changes_nothing()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);
        await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "Cabinet A"), Field("revision", "0")]);

        foreach (var handler in new[] { "Rename", "MoveUp", "MoveDown", "Delete", "DeleteConfirmed" })
        {
            var reply = await session.PostJsonAsync($"{LocationsPath}?handler={handler}", [Field("id", "nope"), Field("name", "Anything"), Field("revision", "1")]);

            reply.Status.Should().Be(HttpStatusCode.UnprocessableEntity, handler);
            reply.JsonText("status").Should().Be("unknown-location", handler);
        }

        Locations(factory).Select(location => location.Name).Should().Equal("Cabinet A");
    }

    [Fact]
    public async Task Deleting_a_location_with_games_asks_first_and_a_confirmed_delete_leaves_the_games_not_placed()
    {
        using var storage = new TemporaryDirectory();
        var clock = SyncHarness.NewClock();
        await using var factory = SyncHarness.CreateFactory(
            ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5)),
            clock,
            new Dictionary<string, string?> { ["Storage:Directory"] = storage.FullPath });
        using var client = factory.CreatePublicClient();
        await SyncRounds.PressAndWait(client, clock, advance: false);
        using var session = new OwnerSession(factory);
        var service = factory.ServingServices.GetRequiredService<OwnerDataService>();
        var added = await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "Shelf B"), Field("revision", "0")]);
        var id = added.JsonText("id");
        var game = await FirstBaseGameAsync(client);
        await service.MutateAsync(service.Revision, data => LocationEdits.Assign(data, game, id, "Example Game"), TestContext.Current.CancellationToken);
        var revision = service.Revision.ToString(CultureInfo.InvariantCulture);

        var form = await session.PostFormAsync($"{LocationsPath}?handler=Delete", [Field("id", id), Field("revision", revision)]);
        var page = await session.GetAsync(form.Location!);
        var unconfirmed = await session.PostJsonAsync($"{LocationsPath}?handler=Delete", [Field("id", id), Field("revision", revision)]);
        var confirmed = await session.PostJsonAsync($"{LocationsPath}?handler=DeleteConfirmed", [Field("id", id), Field("revision", revision)]);

        form.Status.Should().Be(HttpStatusCode.Found);
        form.Location.Should().Be($"{LocationsPath}?confirm={id}#loc-{id}");
        page.Body.Should().Contain("class=\"loc-confirm\"").And.Contain("Delete location").And.Contain("Keep it");
        page.Body.Should().Contain("will become not placed.");
        unconfirmed.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        unconfirmed.JsonText("status").Should().Be("confirm-needed");
        confirmed.JsonText("outcome").Should().Be("saved");
        confirmed.JsonText("status").Should().Be("location-deleted");
        Locations(factory).Should().BeEmpty();
        service.State.Should().BeOfType<OwnerDataState.Ready>().Which.Data.Games.Should().BeEmpty();
        service.State.Should().BeOfType<OwnerDataState.Ready>().Which.Data.Changes[0].Text.Should().Contain("Deleted location \"Shelf B\"").And.Contain("now not placed");
    }

    [Fact]
    public async Task Deleting_an_empty_location_without_script_deletes_at_once_and_lands_on_the_neighbour()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);
        var first = (await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "Cabinet A"), Field("revision", "0")])).JsonText("id");
        var second = (await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "Shelf B"), Field("revision", "1")])).JsonText("id");

        var deleted = await session.PostFormAsync($"{LocationsPath}?handler=Delete", [Field("id", first), Field("revision", "2")]);
        var page = await session.GetAsync(deleted.Location!);

        deleted.Status.Should().Be(HttpStatusCode.Found);
        deleted.Location.Should().Be($"{LocationsPath}?done=saved&item={second}#loc-{second}");
        Locations(factory).Select(location => location.Name).Should().Equal("Shelf B");
        page.Body.Should().Contain("<details class=\"owner-panel loc-panel\" id=\"your-locations\" open=\"open\">");
    }

    [Fact]
    public async Task A_form_post_for_a_rename_comes_back_to_the_same_item_and_a_refused_name_is_shown_inline_there()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);
        var first = (await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "Cabinet A"), Field("revision", "0")])).JsonText("id");
        await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "Shelf B"), Field("revision", "1")]);

        var renamed = await session.PostFormAsync($"{LocationsPath}?handler=Rename", [Field("id", first), Field("name", "Cabinet Z"), Field("revision", "2")]);
        var refused = await session.PostFormAsync($"{LocationsPath}?handler=Rename", [Field("id", first), Field("name", "shelf b"), Field("revision", "3")]);
        var page = await session.GetAsync(refused.Location!);

        renamed.Location.Should().Be($"{LocationsPath}?done=saved&item={first}#loc-{first}");
        refused.Location.Should().Be($"{LocationsPath}?done=refused-name-taken&item={first}#loc-{first}");
        page.Body.Should().Contain($"id=\"loc-error-{first}\" role=\"alert\">You already have a location with that name.</p>");
        page.Body.Should().Contain("aria-invalid=\"true\"").And.Contain($"aria-describedby=\"loc-error-{first}\"");
    }

    [Fact]
    public async Task A_confirm_or_item_that_is_not_one_of_the_locations_is_ignored_and_never_echoed()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var session = new OwnerSession(factory);
        await session.PostJsonAsync($"{LocationsPath}?handler=AddLocation", [Field("name", "Cabinet A"), Field("revision", "0")]);

        var confirm = await session.GetAsync($"{LocationsPath}?confirm=%22%3E%3Cscript%3Ealert(1)%3C/script%3E");
        var item = await session.GetAsync($"{LocationsPath}?done=saved&item=https://example.org/");

        confirm.Body.Should().NotContain("<script>alert").And.NotContain("loc-confirm");
        item.Body.Should().NotContain("example.org");
        item.Body.Should().Contain("id=\"owner-notice\"", "the notice has no valid item to sit in, so it stays at the top");
    }

    private static KeyValuePair<string, string> Field(string name, string value) => new(name, value);

    private static IReadOnlyList<OwnerLocation> Locations(CabinetWebApplicationFactory factory) =>
        factory.ServingServices.GetRequiredService<OwnerDataService>().State is OwnerDataState.Ready ready ? ready.Data.Locations : [];

    private static async Task<int> FirstBaseGameAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/cabinet/cards?profile=desktop", TestContext.Current.CancellationToken);
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return document.RootElement.GetProperty("cards").EnumerateArray()
            .First(card => card.GetProperty("title").GetString() == "Lantern & Harbour")
            .GetProperty("gameId").GetInt32();
    }
}
