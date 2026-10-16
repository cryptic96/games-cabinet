using System.Text;
using Cabinet.Domain.Owner;
using Cabinet.Repository.Images;
using Cabinet.Repository.Storage;
using Cabinet.Service.Owner;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves owner-data files of the first format, which held one finish name, are read and restored as the default look without
/// being rewritten, that a stored look is checked one field at a time, and that a file from a newer format is still refused.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerDataUpgradeTests : IDisposable
{
    private const string Marker = "games-cabinet-owner-data";

    private static readonly ArtSourcePolicy Policy = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "example.org" });

    private readonly TemporaryDirectory _directory = new();

    private string FilePath => Path.Combine(_directory.FullPath, OwnerDataStore.FileName);

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void A_stored_file_of_the_first_format_with_the_classic_finish_loads_with_the_default_look_and_is_left_as_it_is()
    {
        var bytes = FirstFormat("classic");
        File.WriteAllBytes(FilePath, bytes);
        var listingBefore = Directory.EnumerateFileSystemEntries(_directory.FullPath).Order().ToList();

        var state = CreateStore().Load();

        var data = state.Should().BeOfType<OwnerDataState.Ready>().Which.Data;
        data.Look.Should().Be(CabinetLook.Default);
        data.SchemaVersion.Should().Be(3);
        data.Revision.Should().Be(3);
        File.ReadAllBytes(FilePath).Should().Equal(bytes);
        Directory.EnumerateFileSystemEntries(_directory.FullPath).Order().Should().Equal(listingBefore);
    }

    [Theory]
    [InlineData("walnut")]
    [InlineData("")]
    [InlineData("anything else at all")]
    public void A_stored_file_of_the_first_format_with_any_other_finish_loads_with_the_default_look(string finish)
    {
        File.WriteAllBytes(FilePath, FirstFormat(finish));

        var state = CreateStore().Load();

        state.Should().BeOfType<OwnerDataState.Ready>().Which.Data.Look.Should().Be(CabinetLook.Default);
    }

    [Fact]
    public void A_stored_file_of_the_first_format_that_is_otherwise_unreadable_is_still_unreadable_and_left_as_it_is()
    {
        var bytes = Encoding.UTF8.GetBytes($"{{\"kind\":\"{Marker}\",\"schemaVersion\":1,\"revision\":3,\"finish\":\"classic\",\"locations\":null,\"games\":[],\"changesSinceDownload\":0,\"changes\":[]}}");
        File.WriteAllBytes(FilePath, bytes);

        var state = CreateStore().Load();

        state.Should().BeOfType<OwnerDataState.Unreadable>();
        File.ReadAllBytes(FilePath).Should().Equal(bytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_stored_file_below_the_first_format_is_unreadable_and_left_as_it_is(int schemaVersion)
    {
        var bytes = Encoding.UTF8.GetBytes($"{{\"kind\":\"{Marker}\",\"schemaVersion\":{schemaVersion},\"revision\":3,\"finish\":\"classic\",\"locations\":[],\"games\":[],\"changesSinceDownload\":0,\"changes\":[]}}");
        File.WriteAllBytes(FilePath, bytes);
        var listingBefore = Directory.EnumerateFileSystemEntries(_directory.FullPath).Order().ToList();

        var state = CreateStore().Load();

        state.Should().BeOfType<OwnerDataState.Unreadable>().Which.Reason.Should().Be("malformed");
        File.ReadAllBytes(FilePath).Should().Equal(bytes);
        Directory.EnumerateFileSystemEntries(_directory.FullPath).Order().Should().Equal(listingBefore);
    }

    [Fact]
    public void A_stored_file_of_the_current_format_with_an_unknown_wood_loads_with_warm_brown_and_keeps_its_other_four_fields()
    {
        File.WriteAllBytes(FilePath, CurrentFormat("legs", "teak", "painted", "evenings", "deep"));

        var state = CreateStore().Load();

        state.Should().BeOfType<OwnerDataState.Ready>().Which.Data.Look.Should().Be(new CabinetLook("legs", "brown", "painted", "evenings", "deep"));
    }

    [Fact]
    public void A_stored_file_of_the_current_format_without_a_look_is_unreadable()
    {
        var bytes = Encoding.UTF8.GetBytes($"{{\"kind\":\"{Marker}\",\"schemaVersion\":3,\"revision\":3,\"locations\":[],\"games\":[],\"changesSinceDownload\":0,\"changes\":[]}}");
        File.WriteAllBytes(FilePath, bytes);

        CreateStore().Load().Should().BeOfType<OwnerDataState.Unreadable>();
    }

    [Fact]
    public void A_stored_file_of_the_current_format_missing_one_look_field_is_unreadable()
    {
        var text = Encoding.UTF8.GetString(CurrentFormat("legs", "oak", "wood", "off", "shaded")).Replace("\"depth\":\"shaded\"", "\"unrelated\":1", StringComparison.Ordinal);
        File.WriteAllBytes(FilePath, Encoding.UTF8.GetBytes(text));

        CreateStore().Load().Should().BeOfType<OwnerDataState.Unreadable>();
    }

    [Fact]
    public void A_stored_file_of_a_newer_format_is_unreadable_as_newer_and_left_as_it_is()
    {
        var bytes = Encoding.UTF8.GetBytes($"{{\"kind\":\"{Marker}\",\"schemaVersion\":4,\"revision\":7}}");
        File.WriteAllBytes(FilePath, bytes);

        var state = CreateStore().Load();

        state.Should().BeOfType<OwnerDataState.Unreadable>().Which.Reason.Should().Be("newer format");
        File.ReadAllBytes(FilePath).Should().Equal(bytes);
    }

    [Fact]
    public void A_stored_file_of_the_second_format_with_game_wide_entries_loads_as_the_current_format_and_is_left_as_it_is()
    {
        var bytes = SecondFormat("{\"gameId\":100,\"locationId\":\"0a1b2c3d\"},{\"gameId\":101,\"locationId\":\"4e5f6a7b\"}");
        File.WriteAllBytes(FilePath, bytes);
        var listingBefore = Directory.EnumerateFileSystemEntries(_directory.FullPath).Order().ToList();

        var state = CreateStore().Load();

        var data = state.Should().BeOfType<OwnerDataState.Ready>().Which.Data;
        data.SchemaVersion.Should().Be(OwnerData.CurrentSchemaVersion);
        data.Games.Select(game => (game.GameId, game.LocationId, game.CollectionId))
            .Should().Equal((100, "0a1b2c3d", (long?)null), (101, "4e5f6a7b", (long?)null));
        data.Look.Should().Be(new CabinetLook("legs", "oak", "painted", "evenings", "deep"));
        File.ReadAllBytes(FilePath).Should().Equal(bytes);
        Directory.EnumerateFileSystemEntries(_directory.FullPath).Order().Should().Equal(listingBefore);
    }

    [Fact]
    public async Task The_first_save_after_loading_a_second_format_file_keeps_a_copy_of_it_that_the_earlier_release_lists()
    {
        var bytes = SecondFormat("{\"gameId\":100,\"locationId\":\"0a1b2c3d\"}");
        File.WriteAllBytes(FilePath, bytes);
        var service = CreateService(CreateStore());

        var result = await service.MutateAsync(
            service.Revision,
            data => LocationEdits.Assign(data, 100, 7002, [7001, 7002, 7003], "4e5f6a7b", "Quiet Quarry"),
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(OwnerOutcome.Saved);
        var kept = EarlierReleaseVersionReader.List(Path.Combine(_directory.FullPath, "owner-data-versions"));
        kept.Should().ContainSingle();
        File.ReadAllBytes(kept[0].Path).Should().Equal(bytes);
        CreateStore().StoredSchemaVersion().Should().Be(OwnerData.CurrentSchemaVersion);
        CreateStore().Load().Should().BeOfType<OwnerDataState.Ready>().Which.Data.Games
            .Select(game => (game.CollectionId, game.LocationId))
            .Should().Equal(((long?)7001, "0a1b2c3d"), ((long?)7002, "4e5f6a7b"), ((long?)7003, "0a1b2c3d"));
    }

    [Fact]
    public async Task The_first_save_after_loading_a_second_format_file_is_refused_when_the_copy_cannot_be_kept()
    {
        var bytes = SecondFormat("{\"gameId\":100,\"locationId\":\"0a1b2c3d\"}");
        File.WriteAllBytes(FilePath, bytes);
        var service = CreateService(CreateStore());
        File.WriteAllText(Path.Combine(_directory.FullPath, "owner-data-versions"), "in the way");

        var result = await service.MutateAsync(
            service.Revision,
            data => LocationEdits.Assign(data, 100, 7002, [7001, 7002], "4e5f6a7b", "Quiet Quarry"),
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(OwnerOutcome.Refused);
        File.ReadAllBytes(FilePath).Should().Equal(bytes);
    }

    [Fact]
    public void The_restore_check_of_a_second_format_file_is_accepted_as_format_two_and_gives_the_current_format()
    {
        var check = OwnerDataExport.Check(SecondFormat("{\"gameId\":100,\"locationId\":\"0a1b2c3d\"}"), Policy);

        check.IsOk.Should().BeTrue();
        check.FormatVersion.Should().Be(2);
        check.Data!.SchemaVersion.Should().Be(OwnerData.CurrentSchemaVersion);
        check.Data.Games.Should().ContainSingle().Which.CollectionId.Should().BeNull();
    }

    [Fact]
    public void The_look_a_save_writes_loads_back_the_same()
    {
        var look = new CabinetLook("plywood-legs", "birch", "painted", "on", "flat");
        var store = CreateStore();

        store.Save(OwnerData.Empty with { Revision = 1, Look = look });

        store.Load().Should().BeOfType<OwnerDataState.Ready>().Which.Data.Look.Should().Be(look);
    }

    [Fact]
    public void The_restore_check_of_a_first_format_file_is_accepted_as_format_one_with_the_default_look()
    {
        var check = OwnerDataExport.Check(FirstFormat("classic"), Policy);

        check.IsOk.Should().BeTrue();
        check.FormatVersion.Should().Be(1);
        check.Data!.Look.Should().Be(CabinetLook.Default);
        check.Data.SchemaVersion.Should().Be(3);
    }

    [Fact]
    public void The_restore_check_of_a_file_of_a_newer_format_is_refused_as_newer()
    {
        var check = OwnerDataExport.Check(Encoding.UTF8.GetBytes($"{{\"kind\":\"{Marker}\",\"schemaVersion\":4,\"revision\":7}}"), Policy);

        check.Refusal.Should().Be(RestoreProblem.Newer);
        check.FormatVersion.Should().Be(4);
    }

    [Fact]
    public void The_restore_check_accepts_an_unknown_look_value_as_that_fields_default()
    {
        var check = OwnerDataExport.Check(CurrentFormat("sideways", "oak", "wood", "off", "shaded"), Policy);

        check.IsOk.Should().BeTrue();
        check.Data!.Look.Should().Be(new CabinetLook("classic", "oak", "wood", "off", "shaded"));
    }

    [Fact]
    public void The_restore_check_accepts_a_look_value_of_exactly_the_longest_length_and_refuses_one_character_more()
    {
        var atLimit = OwnerDataExport.Check(CurrentFormat("legs", new string('a', CabinetLook.MaxValueLength), "wood", "off", "shaded"), Policy);
        var over = OwnerDataExport.Check(CurrentFormat("legs", new string('a', CabinetLook.MaxValueLength + 1), "wood", "off", "shaded"), Policy);

        atLimit.IsOk.Should().BeTrue();
        atLimit.Data!.Look.Wood.Should().Be("brown");
        over.Refusal.Should().Be(RestoreProblem.OverLimits);
    }

    [Fact]
    public void A_download_of_a_look_restores_to_the_same_look()
    {
        var look = new CabinetLook("display", "walnut", "painted", "evenings", "deep");
        var export = OwnerDataExport.Write(OwnerData.Empty with { Revision = 2, Look = look }, new DateTimeOffset(2030, 1, 15, 12, 0, 0, TimeSpan.Zero));

        var check = OwnerDataExport.Check(export, Policy);

        check.IsOk.Should().BeTrue();
        check.FormatVersion.Should().Be(3);
        check.Data!.Look.Should().Be(look);
    }

    private static byte[] FirstFormat(string finish) =>
        Encoding.UTF8.GetBytes($"{{\"kind\":\"{Marker}\",\"schemaVersion\":1,\"revision\":3,\"finish\":\"{finish}\",\"locations\":[],\"games\":[],\"changesSinceDownload\":0,\"changes\":[]}}");

    private static byte[] CurrentFormat(string design, string wood, string backs, string lights, string depth) =>
        Encoding.UTF8.GetBytes(
            $"{{\"kind\":\"{Marker}\",\"schemaVersion\":3,\"revision\":3,\"look\":{{\"design\":\"{design}\",\"wood\":\"{wood}\",\"backs\":\"{backs}\",\"lights\":\"{lights}\",\"depth\":\"{depth}\"}},"
            + "\"locations\":[],\"games\":[],\"changesSinceDownload\":0,\"changes\":[]}");

    private static byte[] SecondFormat(string gamesJson) =>
        Encoding.UTF8.GetBytes(
            $"{{\"kind\":\"{Marker}\",\"schemaVersion\":2,\"revision\":5,"
            + "\"look\":{\"design\":\"legs\",\"wood\":\"oak\",\"backs\":\"painted\",\"lights\":\"evenings\",\"depth\":\"deep\"},"
            + "\"locations\":[{\"id\":\"0a1b2c3d\",\"name\":\"Cabinet A\"},{\"id\":\"4e5f6a7b\",\"name\":\"Shelf B\"}],"
            + $"\"games\":[{gamesJson}],\"changesSinceDownload\":0,\"changes\":[]}}");

    private static OwnerDataService CreateService(OwnerDataStore store) =>
        new(store, new FakeTimeProvider(new DateTimeOffset(2030, 1, 15, 12, 0, 0, TimeSpan.Zero)), NullLogger<OwnerDataService>.Instance);

    private OwnerDataStore CreateStore() => new(_directory.FullPath, NullLogger<OwnerDataStore>.Instance);
}
