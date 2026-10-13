using System.Text;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using Cabinet.Repository.Images;
using Cabinet.Repository.Storage;
using Cabinet.Service.Owner;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves the check of a file chosen for a restore: it accepts a valid owner-data file whole and refuses everything else with a
/// reason of its own, so a file that is not fully acceptable never reaches the writer.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerRestoreParserTests
{
    private const string LinkAddress = "https://example.org/pictures/linked.png";
    private const string IdA = "0a1b2c3d";
    private const string IdB = "4e5f6a7b";
    private const string Marker = "games-cabinet-owner-data";

    private static readonly ArtSourcePolicy Policy = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "example.org" });
    private static readonly DateTimeOffset Moment = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void No_file_and_an_empty_file_are_no_file()
    {
        Check(null).Refusal.Should().Be(RestoreProblem.NoFile);
        Check([]).Refusal.Should().Be(RestoreProblem.NoFile);
    }

    [Fact]
    public void A_file_of_two_megabytes_is_read_and_one_byte_more_is_too_large_with_its_size()
    {
        var atLimit = Pad(Export(Valid()), OwnerDataExport.MaxRestoreBytes);
        var over = Pad(Export(Valid()), OwnerDataExport.MaxRestoreBytes + 1);

        Check(atLimit).IsOk.Should().BeTrue();
        var refused = Check(over);
        refused.Refusal.Should().Be(RestoreProblem.TooLarge);
        refused.SizeBytes.Should().Be(OwnerDataExport.MaxRestoreBytes + 1);
    }

    [Fact]
    public void Valid_json_nested_seventeen_deep_is_damaged_even_without_the_owner_data_marker()
    {
        Check(Nested(17)).Refusal.Should().Be(RestoreProblem.Damaged);
        Check(Text(string.Concat(Enumerable.Repeat("[", 20)) + string.Concat(Enumerable.Repeat("]", 20)))).Refusal.Should().Be(RestoreProblem.Damaged);
    }

    [Theory]
    [InlineData("this is a plain note")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{}")]
    [InlineData("\"games-cabinet-owner-data\"")]
    [InlineData("{\"kind\":\"something-else\",\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"kind\":5,\"schemaVersion\":1}")]
    public void A_file_that_is_not_json_or_not_an_owner_data_file_is_refused_as_such(string text)
    {
        Check(Encoding.UTF8.GetBytes(text)).Refusal.Should().Be(RestoreProblem.NotOwnerData);
    }

    [Fact]
    public void An_owner_data_file_that_is_cut_short_is_damaged()
    {
        var whole = Export(Valid());

        Check(whole[..(whole.Length / 2)]).Refusal.Should().Be(RestoreProblem.Damaged);
    }

    [Fact]
    public void A_file_without_a_schema_version_or_with_one_below_one_is_damaged()
    {
        Check(Text($"{{\"kind\":\"{Marker}\"}}")).Refusal.Should().Be(RestoreProblem.Damaged);
        Check(Text($"{{\"kind\":\"{Marker}\",\"schemaVersion\":0}}")).Refusal.Should().Be(RestoreProblem.Damaged);
    }

    [Fact]
    public void A_file_of_a_newer_schema_is_refused_with_its_version()
    {
        var refused = Check(Text($"{{\"kind\":\"{Marker}\",\"schemaVersion\":2}}"));

        refused.Refusal.Should().Be(RestoreProblem.Newer);
        refused.FormatVersion.Should().Be(2);
        OwnerStrings.RestoreProblemText(refused).Should().Contain("file format 2; this site reads up to 1");
    }

    [Fact]
    public void A_valid_file_without_a_required_member_or_with_a_wrong_kind_of_value_is_damaged()
    {
        Check(Text($"{{\"kind\":\"{Marker}\",\"schemaVersion\":1}}")).Refusal.Should().Be(RestoreProblem.Damaged);
        Check(Text(Json("\"revision\": 3", "\"revision\": \"three\""))).Refusal.Should().Be(RestoreProblem.Damaged);
        Check(Text(Json("\"choice\": \"mainPicture\"", "\"choice\": \"sideways\""))).Refusal.Should().Be(RestoreProblem.Damaged);
        Check(Text(Json("\"games\": [", "\"games\": [null,"))).Refusal.Should().Be(RestoreProblem.Damaged);
    }

    [Fact]
    public void A_valid_export_is_accepted_whole_with_its_format()
    {
        var data = Valid();

        var check = Check(Export(data));

        check.IsOk.Should().BeTrue();
        check.FormatVersion.Should().Be(1);
        check.Data!.Locations.Should().Equal(data.Locations);
        check.Data.Games.Select(game => game.GameId).Should().Equal(100, 101);
        check.Data.Changes.Should().Equal(data.Changes);
        check.Data.Revision.Should().Be(data.Revision);
    }

    [Fact]
    public void A_revision_or_change_count_too_large_to_keep_counting_from_is_over_the_limits()
    {
        Check(Text(Json("\"revision\": 3", $"\"revision\": {OwnerDataExport.MaxRevision}"))).IsOk.Should().BeTrue();
        Check(Text(Json("\"revision\": 3", $"\"revision\": {OwnerDataExport.MaxRevision + 1L}"))).Refusal.Should().Be(RestoreProblem.OverLimits);
        Check(Text(Json("\"revision\": 3", "\"revision\": 9223372036854775807"))).Refusal.Should().Be(RestoreProblem.OverLimits);
        Check(Text(Json("\"revision\": 3", "\"revision\": 9007199254740993"))).Refusal.Should().Be(RestoreProblem.OverLimits);
        OwnerStrings.RestoreProblemText(Check(Text(Json("\"revision\": 3", "\"revision\": 9007199254740993")))).Should().NotBeNullOrWhiteSpace();
        Check(Text(Json("\"changesSinceDownload\": 0", $"\"changesSinceDownload\": {OwnerDataExport.MaxChangesSinceDownload}"))).IsOk.Should().BeTrue();
        Check(Text(Json("\"changesSinceDownload\": 0", "\"changesSinceDownload\": 2147483647"))).Refusal.Should().Be(RestoreProblem.OverLimits);
    }

    [Fact]
    public void The_games_of_an_accepted_file_come_out_sorted_by_identifier()
    {
        var data = Valid() with { Games = [.. Valid().Games.Reverse()] };

        Check(Export(data)).Data!.Games.Select(game => game.GameId).Should().Equal(100, 101);
    }

    [Fact]
    public void The_fetched_record_of_a_link_in_a_stored_copy_is_left_out_so_the_picture_is_fetched_again()
    {
        using var directory = new TemporaryDirectory();
        var record = new ImageRecord(LinkAddress, ImageStatus.Ok, Moment, [new ArtFile(240, 300, "..-evil-240.webp")]);
        var linked = new OwnerGame(101, IdB, null, new PictureSetting(PictureChoice.Link, true, null, null, new PictureLink(LinkAddress, record, 0, null)));
        new OwnerDataStore(directory.FullPath, NullLogger<OwnerDataStore>.Instance).Save(Valid() with { Games = [new OwnerGame(100, IdA, null, null), linked] });

        var check = Check(File.ReadAllBytes(Path.Combine(directory.FullPath, OwnerDataStore.FileName)));

        check.IsOk.Should().BeTrue();
        check.Data!.Games[1].Picture!.Link!.Url.Should().Be(LinkAddress);
        check.Data.Games[1].Picture!.Link!.Record.Should().BeNull();
    }

    [Fact]
    public void Fifty_locations_are_accepted_and_fifty_one_are_over_the_limits()
    {
        Check(Export(WithLocations(50))).IsOk.Should().BeTrue();
        Check(Export(WithLocations(51))).Refusal.Should().Be(RestoreProblem.OverLimits);
    }

    [Fact]
    public void A_name_of_sixty_characters_is_accepted_and_one_of_sixty_one_is_over_the_limits()
    {
        Check(Export(WithName(new string('a', 60)))).IsOk.Should().BeTrue();
        Check(Export(WithName(new string('a', 61)))).Refusal.Should().Be(RestoreProblem.OverLimits);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Cabinet‮A")]
    [InlineData("Cabinet\u0007A")]
    [InlineData(" Cabinet A")]
    public void A_name_that_is_empty_has_a_forbidden_character_or_edge_spaces_is_over_the_limits(string name)
    {
        Check(Export(WithName(name))).Refusal.Should().Be(RestoreProblem.OverLimits);
    }

    [Fact]
    public void Two_names_equal_ignoring_case_are_over_the_limits()
    {
        var data = Valid() with { Locations = [new OwnerLocation(IdA, "Cabinet A"), new OwnerLocation(IdB, "CABINET a")], Games = [] };

        Check(Export(data)).Refusal.Should().Be(RestoreProblem.OverLimits);
    }

    [Fact]
    public void Two_locations_with_the_same_identifier_are_over_the_limits()
    {
        var data = Valid() with { Locations = [new OwnerLocation(IdA, "Cabinet A"), new OwnerLocation(IdA, "Shelf B")], Games = [] };

        Check(Export(data)).Refusal.Should().Be(RestoreProblem.OverLimits);
    }

    [Theory]
    [InlineData("seed0000")]
    [InlineData("0A1B2C3D")]
    [InlineData("0a1b2c3")]
    [InlineData("0a1b2c3d4")]
    [InlineData("../0a1b2")]
    public void An_identifier_that_is_not_the_shape_the_server_makes_is_over_the_limits(string id)
    {
        var data = Valid() with { Locations = [new OwnerLocation(id, "Cabinet A")], Games = [] };

        Check(Export(data)).Refusal.Should().Be(RestoreProblem.OverLimits);
    }

    [Fact]
    public void A_game_kept_in_a_location_the_file_does_not_list_is_over_the_limits()
    {
        var data = Valid() with { Games = [new OwnerGame(100, "ffffffff", null, null)] };

        Check(Export(data)).Refusal.Should().Be(RestoreProblem.OverLimits);
    }

    [Fact]
    public void Two_entries_for_one_game_are_over_the_limits()
    {
        var data = Valid() with { Games = [new OwnerGame(100, IdA, null, null), new OwnerGame(100, IdB, null, null)] };

        Check(Export(data)).Refusal.Should().Be(RestoreProblem.OverLimits);
    }

    [Fact]
    public void Five_thousand_games_are_accepted_and_one_more_is_over_the_limits()
    {
        Check(Export(WithGames(OwnerDataExport.MaxGames))).IsOk.Should().BeTrue();
        Check(Export(WithGames(OwnerDataExport.MaxGames + 1))).Refusal.Should().Be(RestoreProblem.OverLimits);
    }

    [Theory]
    [InlineData("https://evil.example.com/pictures/linked.png")]
    [InlineData("http://example.org/pictures/linked.png")]
    [InlineData("https://example.org:8443/pictures/linked.png")]
    [InlineData("https://user@example.org/pictures/linked.png")]
    [InlineData("/" + "/example.org/pictures/linked.png")]
    [InlineData("not an address")]
    public void A_link_on_another_host_over_http_or_in_another_shape_is_over_the_limits(string address)
    {
        var data = Valid() with { Games = [new OwnerGame(100, IdA, null, LinkPicture(address))] };

        Check(Export(data)).Refusal.Should().Be(RestoreProblem.OverLimits);
    }

    [Fact]
    public void A_game_that_is_not_a_positive_identifier_is_over_the_limits()
    {
        var data = Valid() with { Games = [new OwnerGame(0, IdA, null, null)] };

        Check(Export(data)).Refusal.Should().Be(RestoreProblem.OverLimits);
    }

    [Fact]
    public void More_log_lines_than_the_site_keeps_or_a_very_longLine_are_over_the_limits()
    {
        var many = Enumerable.Range(0, OwnerData.MaxStoredChanges + 1).Select(index => new ChangeEntry(Moment, $"Change {index}")).ToList();
        var longLine = new ChangeEntry(Moment, new string('x', 2000));

        Check(Export(Valid() with { Changes = many })).Refusal.Should().Be(RestoreProblem.OverLimits);
        Check(Export(Valid() with { Changes = [longLine] })).Refusal.Should().Be(RestoreProblem.OverLimits);
    }

    [Fact]
    public void Each_refusal_has_words_of_its_own_and_none_echoes_the_file()
    {
        var texts = new[]
        {
            RestoreProblem.NoFile,
            RestoreProblem.NotOwnerData,
            RestoreProblem.Damaged,
            RestoreProblem.OverLimits,
        }.Select(problem => OwnerStrings.RestoreProblemText(RestoreCheck.Problem(problem))).ToList();

        texts.Should().OnlyHaveUniqueItems();
        OwnerStrings.RestoreProblemText(RestoreCheck.Problem(RestoreProblem.NoFile)).Should().Be("Choose a file first.");
        OwnerStrings.RestoreProblemText(RestoreCheck.Problem(RestoreProblem.NotOwnerData)).Should().Be("That isn't an owner-data file. Choose a file you downloaded here.");
        OwnerStrings.RestoreProblemText(RestoreCheck.Problem(RestoreProblem.Damaged)).Should().Be("That file is damaged or incomplete, so nothing was changed.");
        OwnerStrings.RestoreProblemText(RestoreCheck.Problem(RestoreProblem.OverLimits)).Should().Be("That file holds more than this site accepts, so nothing was changed.");
        OwnerStrings.RestoreProblemText(RestoreCheck.Problem(RestoreProblem.TooLarge, sizeBytes: OwnerDataExport.MaxRestoreBytes + 1))
            .Should().Be("That file is too large (2.1 MB; the limit is 2 MB). Owner-data files are small, so this is probably the wrong file.");
        OwnerStrings.RestoreProblemText(RestoreCheck.Problem(RestoreProblem.Newer, formatVersion: 4))
            .Should().Be("That file was made by a newer version of the site (file format 4; this site reads up to 1). Update the site first, or use an older download.");
    }

    private static RestoreCheck Check(byte[]? bytes) => OwnerDataExport.Check(bytes, Policy);

    private static byte[] Text(string text) => Encoding.UTF8.GetBytes(text);

    private static byte[] Export(OwnerData data) => OwnerDataExport.Write(data, Moment);

    private static byte[] Pad(byte[] json, int length)
    {
        var padded = new byte[length];
        json.CopyTo(padded, 0);
        Array.Fill(padded, (byte)' ', json.Length, length - json.Length);

        return padded;
    }

    private static byte[] Nested(int depth)
    {
        var open = string.Concat(Enumerable.Repeat("[", depth - 1));
        var close = string.Concat(Enumerable.Repeat("]", depth - 1));

        return Text($"{{\"kind\":\"{Marker}\",\"schemaVersion\":1,\"x\":{open}{close}}}");
    }

    private static string Json(string find, string replace)
    {
        var text = Encoding.UTF8.GetString(Export(Valid()));

        text.Should().Contain(find);

        return text.Replace(find, replace, StringComparison.Ordinal);
    }

    private static PictureSetting LinkPicture(string address) =>
        new(PictureChoice.Link, true, null, null, new PictureLink(address, null, 0, null));

    private static OwnerData Valid() => OwnerData.Empty with
    {
        Revision = 3,
        SavedAtUtc = Moment.AddDays(-1),
        Locations = [new OwnerLocation(IdA, "Cabinet A"), new OwnerLocation(IdB, "Shelf B")],
        Games =
        [
            new OwnerGame(100, IdA, null, new PictureSetting(PictureChoice.MainPicture, true, null, null, null)),
            new OwnerGame(101, IdB, null, LinkPicture(LinkAddress)),
        ],
        Changes = [new ChangeEntry(Moment.AddDays(-1), "Added location \"Cabinet A\"")],
    };

    private static OwnerData WithName(string name) => Valid() with { Locations = [new OwnerLocation(IdA, name)], Games = [] };

    private static OwnerData WithLocations(int count) => Valid() with
    {
        Locations = [.. Enumerable.Range(0, count).Select(index => new OwnerLocation($"{index:x8}", $"Place {index}"))],
        Games = [],
    };

    private static OwnerData WithGames(int count) => Valid() with
    {
        Games = [.. Enumerable.Range(1, count).Select(index => new OwnerGame(index, IdA, null, null))],
    };
}
