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
    [InlineData("{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":2,\"revision\":7}")]
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

    [Fact]
    public async Task A_refused_write_over_a_damaged_file_leaves_its_bytes_and_the_directory_as_they_were()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"kind\":\"games-cabinet-owner-data\",\"schemaVersion\":2,\"revision\":7}");
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

    private OwnerDataStore CreateStore() => new(_directory.FullPath, NullLogger<OwnerDataStore>.Instance);

    private string[] Listing() => [.. Directory.EnumerateFileSystemEntries(_directory.FullPath).Order(StringComparer.Ordinal)];
}
