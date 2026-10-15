using System.Text;
using Cabinet.Domain.Owner;
using Cabinet.Repository.Storage;
using Cabinet.Service.Owner;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Owner;

/// <summary>Proves the owner-data file round-trips, is read strictly, and is never renamed, rewritten or joined by other files when it cannot be read.</summary>
[Trait("Category", "Owner")]
public sealed class OwnerDataStoreTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private string FilePath => Path.Combine(_directory.FullPath, OwnerDataStore.FileName);

    private string VersionsPath => Path.Combine(_directory.FullPath, "owner-data-versions");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void A_missing_file_loads_as_empty_owner_data()
    {
        var state = CreateStore().Load();

        state.Should().BeOfType<OwnerDataState.Ready>().Which.Data.Should().BeEquivalentTo(OwnerData.Empty);
        File.Exists(FilePath).Should().BeFalse();
    }

    [Fact]
    public void Saved_owner_data_loads_back_the_same()
    {
        var moment = new DateTimeOffset(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var data = OwnerData.Empty with
        {
            Revision = 4,
            SavedAtUtc = moment,
            Locations = [new OwnerLocation("a1", "Cabinet A"), new OwnerLocation("a2", "Shelf B")],
            Games = [new OwnerGame(100, "a1", null, null), new OwnerGame(101, "a2", moment, null)],
            ChangesSinceDownload = 3,
            Changes = [new ChangeEntry(moment, "Moved Lantern Harbour to Cabinet A")],
        };
        var store = CreateStore();

        store.Save(data);

        store.Load().Should().BeOfType<OwnerDataState.Ready>().Which.Data.Should().BeEquivalentTo(data);
    }

    [Fact]
    public void A_save_leaves_no_temporary_file_beside_the_owner_data()
    {
        var store = CreateStore();

        store.Save(OwnerData.Empty with { Revision = 1 });
        store.Save(OwnerData.Empty with { Revision = 2 });

        Directory.EnumerateFileSystemEntries(_directory.FullPath).Should().Equal(FilePath);
    }

    [Theory]
    [InlineData("{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":3,\"revision\":7}")]
    [InlineData("{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":99,\"locations\":\"unknown shape\"}")]
    [InlineData("{\"kind\":\"games-cabinet-snapshot\",\"schemaVersion\":1,\"revision\":1}")]
    [InlineData("{\"schemaVersion\":1,\"revision\":1}")]
    [InlineData("{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":1,\"revision\":3,\"finish\":\"classic\",\"locations\":[")]
    [InlineData("{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":1,\"revision\":3,\"finish\":\"classic\",\"locations\":null,\"games\":[],\"changesSinceDownload\":0,\"changes\":[]}")]
    [InlineData("{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":1,\"revision\":3,\"finish\":\"classic\",\"locations\":[null],\"games\":[],\"changesSinceDownload\":0,\"changes\":[]}")]
    [InlineData("{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":1,\"revision\":3,\"finish\":\"classic\",\"locations\":[],\"games\":[],\"changesSinceDownload\":0}")]
    [InlineData("not json at all")]
    [InlineData("")]
    public void A_file_this_build_cannot_read_is_reported_unreadable_and_nothing_about_it_changes(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        File.WriteAllBytes(FilePath, bytes);
        var listingBefore = Listing();

        var state = CreateStore().Load();

        state.Should().BeOfType<OwnerDataState.Unreadable>();
        File.ReadAllBytes(FilePath).Should().Equal(bytes);
        Listing().Should().Equal(listingBefore);
    }

    [Theory]
    [InlineData("[null]", false)]
    [InlineData("[{\"width\":240,\"height\":300,\"name\":\"a.webp\"}]", true)]
    [InlineData("[]", true)]
    public void A_missing_entry_in_a_stored_picture_file_list_makes_the_file_unreadable(string files, bool readable)
    {
        var content = "{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":1,\"revision\":3,\"finish\":\"classic\",\"locations\":[],"
            + "\"games\":[{\"gameId\":5,\"picture\":{\"choice\":\"link\",\"checked\":false,\"link\":{\"url\":\"https://img.example.org/a.jpg\",\"failedChecks\":0,"
            + "\"record\":{\"sourceUrl\":\"https://img.example.org/a.jpg\",\"status\":\"ok\",\"attemptedAtUtc\":\"2026-01-01T00:00:00Z\",\"files\":" + files + "}}}}],"
            + "\"changesSinceDownload\":0,\"changes\":[]}";
        File.WriteAllBytes(FilePath, Encoding.UTF8.GetBytes(content));

        var state = CreateStore().Load();

        if (readable)
        {
            state.Should().BeOfType<OwnerDataState.Ready>();
        }
        else
        {
            state.Should().BeOfType<OwnerDataState.Unreadable>();
        }
    }

    [Fact]
    public async Task A_refused_write_over_a_damaged_file_leaves_its_bytes_and_the_directory_as_they_were()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":3,\"revision\":7}");
        File.WriteAllBytes(FilePath, bytes);
        var listingBefore = Listing();
        var bytesBeforeLoad = File.ReadAllBytes(FilePath);
        var service = new OwnerDataService(CreateStore(), new FakeTimeProvider(OwnerTestKit.Start), NullLogger<OwnerDataService>.Instance);

        var added = await service.MutateAsync(0, data => LocationEdits.Add(data, "Cabinet A", OwnerDataService.NewId), CancellationToken.None);
        var undone = await service.UndoAsync(1, CancellationToken.None);

        service.State.Should().BeOfType<OwnerDataState.Unreadable>();
        added.Outcome.Should().Be(OwnerOutcome.Unreadable);
        undone.Outcome.Should().Be(OwnerOutcome.Unreadable);
        File.ReadAllBytes(FilePath).Should().Equal(bytesBeforeLoad).And.Equal(bytes);
        Listing().Should().Equal(listingBefore);
    }

    [Fact]
    public void Keeping_an_earlier_version_copies_the_file_as_it_is_even_when_it_cannot_be_read()
    {
        var bytes = Encoding.UTF8.GetBytes("{ this is not json");
        File.WriteAllBytes(FilePath, bytes);
        var store = CreateStore();

        store.KeepEarlierVersion(OwnerTestKit.Start);

        var versionPath = Path.Combine(VersionsPath, "owner-data.20300115T120000Z.json");
        File.ReadAllBytes(versionPath).Should().Equal(bytes);
        File.ReadAllBytes(FilePath).Should().Equal(bytes);
        store.EarlierVersions().Should().Equal(new EarlierVersion(0, OwnerTestKit.Start));
        store.ReadEarlierVersion(0).Should().Equal(bytes);
        store.NewestEarlierVersionAt().Should().Be(OwnerTestKit.Start);
    }

    [Fact]
    public void Keeping_an_earlier_version_with_no_file_yet_keeps_nothing_and_creates_nothing()
    {
        var store = CreateStore();

        store.KeepEarlierVersion(OwnerTestKit.Start);

        Directory.Exists(VersionsPath).Should().BeFalse();
        store.EarlierVersions().Should().BeEmpty();
        store.NewestEarlierVersionAt().Should().BeNull();
        store.ReadEarlierVersion(0).Should().BeNull();
    }

    [Fact]
    public void After_twelve_copies_exactly_the_newest_ten_remain_listed_newest_first()
    {
        var store = CreateStore();
        byte[] newestCopied = [];

        for (var number = 0; number < 12; number++)
        {
            store.Save(OwnerData.Empty with { Revision = number + 1 });
            newestCopied = File.ReadAllBytes(FilePath);
            store.KeepEarlierVersion(OwnerTestKit.Start.AddHours(number));
        }

        var listed = store.EarlierVersions();
        listed.Should().HaveCount(OwnerDataStore.KeptVersions);
        listed.Select(version => version.SavedAtUtc).Should().Equal(Enumerable.Range(2, 10).Reverse().Select(number => OwnerTestKit.Start.AddHours(number)));
        listed.Select(version => version.Index).Should().Equal(Enumerable.Range(0, 10));
        Directory.EnumerateFiles(VersionsPath).Should().HaveCount(OwnerDataStore.KeptVersions);
        store.ReadEarlierVersion(0).Should().Equal(newestCopied);
    }

    [Fact]
    public void A_version_is_read_by_its_place_in_the_listing_only_and_other_files_in_the_folder_are_ignored()
    {
        var store = CreateStore();
        store.Save(OwnerData.Empty with { Revision = 1 });
        store.KeepEarlierVersion(OwnerTestKit.Start);
        File.WriteAllText(Path.Combine(VersionsPath, "owner-data.notatime.json"), "stranger");
        File.WriteAllText(Path.Combine(VersionsPath, "notes.txt"), "stranger");

        store.EarlierVersions().Should().HaveCount(1);
        store.ReadEarlierVersion(1).Should().BeNull();
        store.ReadEarlierVersion(-1).Should().BeNull();
        store.ReadEarlierVersion(int.MaxValue).Should().BeNull();
    }

    [Fact]
    public void Temporary_files_an_interrupted_copy_left_in_the_versions_folder_are_removed_when_the_store_starts()
    {
        Directory.CreateDirectory(VersionsPath);
        var stray = Path.Combine(VersionsPath, ".owner-data.20300115T120000Z.json.0123456789abcdef.tmp");
        var real = Path.Combine(VersionsPath, "owner-data.20300115T110000Z.json");
        File.WriteAllText(stray, "half");
        File.WriteAllText(real, "whole");

        var store = CreateStore();

        File.Exists(stray).Should().BeFalse();
        File.Exists(real).Should().BeTrue();
        store.EarlierVersions().Should().HaveCount(1);
    }

    [Fact]
    public void Keeping_a_version_leaves_no_temporary_file_and_does_not_touch_the_live_file()
    {
        var store = CreateStore();
        store.Save(OwnerData.Empty with { Revision = 1 });
        var before = File.ReadAllBytes(FilePath);

        store.KeepEarlierVersion(OwnerTestKit.Start);

        File.ReadAllBytes(FilePath).Should().Equal(before);
        Directory.EnumerateFileSystemEntries(VersionsPath).Should().HaveCount(1);
        Directory.EnumerateFileSystemEntries(_directory.FullPath).Should().BeEquivalentTo([FilePath, VersionsPath]);
    }

    [Fact]
    public void A_file_in_an_older_format_is_kept_under_the_name_earlier_releases_list_and_is_listed_and_readable_like_any_other_copy()
    {
        var bytes = FirstFormatFile();
        var store = CreateStore();

        store.KeepEarlierVersion(OwnerTestKit.Start);

        Directory.EnumerateFiles(VersionsPath).Select(Path.GetFileName).Should().Equal("owner-data.20300115T120000Z.json");
        store.EarlierVersions().Should().Equal(new EarlierVersion(0, OwnerTestKit.Start));
        store.ReadEarlierVersion(0).Should().Equal(bytes);
        store.NewestEarlierVersionAt().Should().Be(OwnerTestKit.Start);
        File.ReadAllBytes(FilePath).Should().Equal(bytes);
    }

    [Fact]
    public void The_earlier_release_lists_the_copy_of_a_file_in_an_older_format_under_the_moment_it_was_taken_with_the_original_bytes()
    {
        var bytes = FirstFormatFile();
        var store = CreateStore();

        store.KeepEarlierVersion(OwnerTestKit.Start);

        var listed = EarlierReleaseVersionReader.List(VersionsPath);
        listed.Should().ContainSingle().Which.SavedAtUtc.Should().Be(OwnerTestKit.Start);
        File.ReadAllBytes(listed[0].Path).Should().Equal(bytes);
        Directory.EnumerateFiles(VersionsPath)
            .Select(path => EarlierReleaseVersionReader.ParseVersionTime(Path.GetFileName(path)))
            .Should().AllSatisfy(moment => moment.Should().NotBeNull());
    }

    [Fact]
    public void A_copy_of_an_older_format_written_under_the_dedicated_name_is_still_listed_and_readable()
    {
        var bytes = FirstFormatFile();
        Directory.CreateDirectory(VersionsPath);
        File.WriteAllBytes(Path.Combine(VersionsPath, "owner-data.format-1.20300115T110000000Z.json"), bytes);

        var store = CreateStore();

        store.EarlierVersions().Should().Equal(new EarlierVersion(0, OwnerTestKit.Start.AddHours(-1)));
        store.ReadEarlierVersion(0).Should().Equal(bytes);
    }

    [Fact]
    public void A_file_already_copied_under_the_dedicated_name_is_not_copied_again()
    {
        var bytes = FirstFormatFile();
        Directory.CreateDirectory(VersionsPath);
        File.WriteAllBytes(Path.Combine(VersionsPath, "owner-data.format-1.20300115T110000000Z.json"), bytes);
        var store = CreateStore();

        store.KeepEarlierVersion(OwnerTestKit.Start);

        Directory.EnumerateFiles(VersionsPath).Should().ContainSingle();
    }

    [Theory]
    [InlineData("owner-data.20300115T110000Z.json")]
    [InlineData("owner-data.format-1.20300115T110000000Z.json")]
    public void More_than_ten_newer_ordinary_copies_never_remove_a_copy_in_an_older_format_whatever_its_name(string olderCopyName)
    {
        var olderBytes = FirstFormatFile();
        Directory.CreateDirectory(VersionsPath);
        var olderCopy = Path.Combine(VersionsPath, olderCopyName);
        File.WriteAllBytes(olderCopy, olderBytes);
        var store = CreateStore();

        for (var number = 1; number <= OwnerDataStore.KeptVersions + 3; number++)
        {
            store.Save(OwnerData.Empty with { Revision = number });
            store.KeepEarlierVersion(OwnerTestKit.Start.AddHours(number));
        }

        File.ReadAllBytes(olderCopy).Should().Equal(olderBytes);
        Directory.EnumerateFiles(VersionsPath).Should().HaveCount(OwnerDataStore.KeptVersions + 1);
        store.EarlierVersions().Should().HaveCount(OwnerDataStore.KeptVersions + 1);
    }

    [Fact]
    public void A_copy_whose_header_cannot_be_read_is_never_removed_while_ordinary_copies_are_still_kept_to_ten()
    {
        Directory.CreateDirectory(VersionsPath);
        var damaged = Path.Combine(VersionsPath, "owner-data.20300115T110000Z.json");
        File.WriteAllText(damaged, "{ this is not json");
        var store = CreateStore();

        for (var number = 1; number <= OwnerDataStore.KeptVersions + 3; number++)
        {
            store.Save(OwnerData.Empty with { Revision = number });
            store.KeepEarlierVersion(OwnerTestKit.Start.AddHours(number));
        }

        File.Exists(damaged).Should().BeTrue();
        store.EarlierVersions().Select(version => version.SavedAtUtc).Should().Equal(
            Enumerable.Range(4, OwnerDataStore.KeptVersions).Reverse().Select(number => OwnerTestKit.Start.AddHours(number))
                .Append(OwnerTestKit.Start.AddHours(-1)));
    }

    [Fact]
    public void An_ordinary_copy_whose_header_starts_past_a_long_first_member_is_kept_rather_than_removed()
    {
        Directory.CreateDirectory(VersionsPath);
        var unusual = Path.Combine(VersionsPath, "owner-data.20300115T110000Z.json");
        File.WriteAllText(unusual, $"{{\"note\":\"{new string('x', 8000)}\",\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":{OwnerData.CurrentSchemaVersion}}}");
        var store = CreateStore();

        for (var number = 1; number <= OwnerDataStore.KeptVersions + 1; number++)
        {
            store.Save(OwnerData.Empty with { Revision = number });
            store.KeepEarlierVersion(OwnerTestKit.Start.AddHours(number));
        }

        File.Exists(unusual).Should().BeTrue();
    }

    [Fact]
    public void A_copy_of_an_older_format_moves_past_every_taken_second_and_overwrites_none_of_them()
    {
        Directory.CreateDirectory(VersionsPath);
        var first = Path.Combine(VersionsPath, "owner-data.20300115T120000Z.json");
        var second = Path.Combine(VersionsPath, "owner-data.20300115T120001Z.json");
        var store = CreateStore();
        store.Save(OwnerData.Empty with { Revision = 1 });
        var firstBytes = File.ReadAllBytes(FilePath);
        File.WriteAllBytes(first, firstBytes);
        var secondBytes = FirstFormatFile(revision: 2);
        File.WriteAllBytes(second, secondBytes);
        var olderBytes = FirstFormatFile(revision: 3);

        store.KeepEarlierVersion(OwnerTestKit.Start);

        File.ReadAllBytes(first).Should().Equal(firstBytes);
        File.ReadAllBytes(second).Should().Equal(secondBytes);
        File.ReadAllBytes(Path.Combine(VersionsPath, "owner-data.20300115T120002Z.json")).Should().Equal(olderBytes);
        EarlierReleaseVersionReader.List(VersionsPath).Should().HaveCount(3);
    }

    [Fact]
    public void An_ordinary_copy_in_the_same_second_as_a_copy_of_an_older_format_moves_on_and_replaces_only_an_ordinary_copy()
    {
        var olderBytes = FirstFormatFile();
        var store = CreateStore();
        store.KeepEarlierVersion(OwnerTestKit.Start);
        store.Save(OwnerData.Empty with { Revision = 4 });
        store.KeepEarlierVersion(OwnerTestKit.Start);
        store.Save(OwnerData.Empty with { Revision = 5 });
        var newest = File.ReadAllBytes(FilePath);

        store.KeepEarlierVersion(OwnerTestKit.Start);

        File.ReadAllBytes(Path.Combine(VersionsPath, "owner-data.20300115T120000Z.json")).Should().Equal(olderBytes);
        File.ReadAllBytes(Path.Combine(VersionsPath, "owner-data.20300115T120001Z.json")).Should().Equal(newest);
        Directory.EnumerateFiles(VersionsPath).Should().HaveCount(2);
    }

    [Fact]
    public void A_copy_taken_in_the_same_second_after_the_upgrade_does_not_replace_the_copy_of_the_older_format()
    {
        var bytes = FirstFormatFile();
        var store = CreateStore();
        store.KeepEarlierVersion(OwnerTestKit.Start);
        store.Save(OwnerData.Empty with { Revision = 4 });

        store.KeepEarlierVersion(OwnerTestKit.Start);

        var listed = store.EarlierVersions();
        listed.Should().HaveCount(2);
        listed.Select(version => store.ReadEarlierVersion(version.Index)).Should().ContainEquivalentOf(bytes);
        Directory.EnumerateFiles(VersionsPath).Should().HaveCount(2);
    }

    [Fact]
    public void The_copy_of_an_older_format_survives_any_number_of_later_copies()
    {
        var bytes = FirstFormatFile();
        var store = CreateStore();
        store.KeepEarlierVersion(OwnerTestKit.Start);

        for (var number = 1; number <= OwnerDataStore.KeptVersions + 5; number++)
        {
            store.Save(OwnerData.Empty with { Revision = number });
            store.KeepEarlierVersion(OwnerTestKit.Start.AddHours(number));
        }

        var listed = store.EarlierVersions();
        listed.Should().HaveCount(OwnerDataStore.KeptVersions + 1);
        store.ReadEarlierVersion(listed.Single(version => version.SavedAtUtc == OwnerTestKit.Start).Index).Should().Equal(bytes);
    }

    [Fact]
    public void The_same_older_format_file_is_not_copied_twice_and_a_different_one_at_the_same_moment_gets_its_own_copy()
    {
        FirstFormatFile();
        var store = CreateStore();
        store.KeepEarlierVersion(OwnerTestKit.Start);
        store.KeepEarlierVersion(OwnerTestKit.Start.AddMinutes(1));
        store.EarlierVersions().Should().HaveCount(1);

        FirstFormatFile(revision: 9);
        store.KeepEarlierVersion(OwnerTestKit.Start);

        Directory.EnumerateFiles(VersionsPath).Should().HaveCount(2);
        store.EarlierVersions().Should().HaveCount(2);
    }

    [Fact]
    public void A_file_in_the_current_format_is_still_copied_under_the_ordinary_name()
    {
        var store = CreateStore();
        store.Save(OwnerData.Empty with { Revision = 1 });

        store.KeepEarlierVersion(OwnerTestKit.Start);

        Directory.EnumerateFiles(VersionsPath).Select(Path.GetFileName).Should().Equal("owner-data.20300115T120000Z.json");
    }

    [Fact]
    public void The_stored_schema_version_is_null_without_a_file_and_the_version_of_the_header_with_one()
    {
        var store = CreateStore();
        store.StoredSchemaVersion().Should().BeNull();

        File.WriteAllText(FilePath, "{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":1}");
        store.StoredSchemaVersion().Should().Be(1);

        store.Save(OwnerData.Empty with { Revision = 1 });
        store.StoredSchemaVersion().Should().Be(OwnerData.CurrentSchemaVersion);
    }

    [Fact]
    public void The_stored_schema_version_is_null_when_the_header_cannot_be_read_and_the_file_is_left_alone()
    {
        File.WriteAllText(FilePath, "not json at all");
        var before = File.ReadAllBytes(FilePath);

        CreateStore().StoredSchemaVersion().Should().BeNull();

        File.ReadAllBytes(FilePath).Should().Equal(before);
    }

    private byte[] FirstFormatFile(int revision = 3)
    {
        var bytes = Encoding.UTF8.GetBytes(
            $"{{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":1,\"revision\":{revision},\"finish\":\"classic\",\"locations\":[],\"games\":[],\"changesSinceDownload\":0,\"changes\":[]}}");
        File.WriteAllBytes(FilePath, bytes);

        return bytes;
    }

    private OwnerDataStore CreateStore() => new(_directory.FullPath, NullLogger<OwnerDataStore>.Instance);

    private string[] Listing() => [.. Directory.EnumerateFileSystemEntries(_directory.FullPath).Order(StringComparer.Ordinal)];
}
