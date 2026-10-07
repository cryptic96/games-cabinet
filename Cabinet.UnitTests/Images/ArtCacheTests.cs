using Cabinet.Repository.Images;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.UnitTests.Images;

/// <summary>Proves the art directory writes whole files under checked names and deletes only what is unused and old enough.</summary>
[Trait("Category", "Images")]
public sealed class ArtCacheTests : IDisposable
{
    private const string FirstName = "0123456789abcdef-240.webp";
    private const string SecondName = "fedcba9876543210-480.webp";

    private static readonly DateTimeOffset Now = new(2030, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Grace = TimeSpan.FromDays(7);

    private readonly TemporaryDirectory _state = new();

    public void Dispose() => _state.Dispose();

    [Fact]
    public void The_directory_is_created_beside_the_stored_collection()
    {
        var cache = new ArtCache(_state.FullPath);

        cache.Path.Should().Be(Path.Combine(_state.FullPath, "art"));
        Directory.Exists(cache.Path).Should().BeTrue();
    }

    [Fact]
    public void A_written_file_is_found_and_leaves_no_temporary_file()
    {
        var cache = new ArtCache(_state.FullPath);

        cache.Write(new EncodedArt(240, 320, FirstName, [1, 2, 3]));

        cache.Has(FirstName).Should().BeTrue();
        cache.Has(SecondName).Should().BeFalse();
        File.ReadAllBytes(Path.Combine(cache.Path, FirstName)).Should().Equal(1, 2, 3);
        Directory.EnumerateFileSystemEntries(cache.Path).Select(Path.GetFileName).Should().Equal(FirstName);
    }

    [Theory]
    [InlineData("../snapshot.json")]
    [InlineData("..\\snapshot.json")]
    [InlineData("x.json")]
    [InlineData("0123456789ABCDEF-240.webp")]
    [InlineData("0123456789abcdef-240.png")]
    [InlineData("0123456789abcde-240.webp")]
    [InlineData("0123456789abcdef-24000.webp")]
    [InlineData("sub/0123456789abcdef-240.webp")]
    [InlineData("0123456789abcdef-240.webp\n")]
    [InlineData("")]
    public void A_name_that_is_not_a_stored_picture_name_is_rejected_and_never_found(string name)
    {
        var cache = new ArtCache(_state.FullPath);

        var write = () => cache.Write(new EncodedArt(1, 1, name, [1]));

        write.Should().Throw<ArgumentException>();
        cache.Has(name).Should().BeFalse();
        Directory.EnumerateFileSystemEntries(_state.FullPath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(_state.FullPath, path)).Should().Equal("art");
    }

    [Fact]
    public void Temporary_files_an_interrupted_write_left_are_removed_when_the_cache_opens()
    {
        var directory = Path.Combine(_state.FullPath, "art");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ".0123456789abcdef-240.webp.0123456789abcdef0123456789abcdef.tmp"), "partial");
        File.WriteAllBytes(Path.Combine(directory, FirstName), [1]);

        _ = new ArtCache(_state.FullPath);

        Directory.EnumerateFileSystemEntries(directory).Select(Path.GetFileName).Should().Equal(FirstName);
    }

    [Fact]
    public void Prune_keeps_referenced_files_and_young_files_and_deletes_only_old_unreferenced_ones()
    {
        var cache = new ArtCache(_state.FullPath);
        const string referencedOld = "aaaaaaaaaaaaaaaa-240.webp";
        const string unreferencedOld = "bbbbbbbbbbbbbbbb-240.webp";
        const string unreferencedYoung = "cccccccccccccccc-240.webp";
        const string unreferencedJustInsideGrace = "dddddddddddddddd-240.webp";
        Stored(cache, referencedOld, Now - TimeSpan.FromDays(30));
        Stored(cache, unreferencedOld, Now - TimeSpan.FromDays(30));
        Stored(cache, unreferencedYoung, Now - TimeSpan.FromHours(1));
        Stored(cache, unreferencedJustInsideGrace, Now - Grace + TimeSpan.FromMinutes(1));

        var deleted = cache.Prune(new HashSet<string>(StringComparer.Ordinal) { referencedOld }, Grace, Now);

        deleted.Should().Be(1);
        cache.Has(referencedOld).Should().BeTrue();
        cache.Has(unreferencedOld).Should().BeFalse();
        cache.Has(unreferencedYoung).Should().BeTrue();
        cache.Has(unreferencedJustInsideGrace).Should().BeTrue();
    }

    [Fact]
    public void Prune_never_touches_anything_outside_the_art_directory_or_that_is_not_a_stored_picture()
    {
        var cache = new ArtCache(_state.FullPath);
        var outside = Path.Combine(_state.FullPath, FirstName);
        var snapshot = Path.Combine(_state.FullPath, "snapshot.json");
        var notAPicture = Path.Combine(cache.Path, "notes.webp");
        File.WriteAllText(outside, "x");
        File.WriteAllText(snapshot, "{}");
        File.WriteAllText(notAPicture, "x");

        foreach (var path in new[] { outside, snapshot, notAPicture })
        {
            File.SetLastWriteTimeUtc(path, (Now - TimeSpan.FromDays(400)).UtcDateTime);
        }

        var deleted = cache.Prune(new HashSet<string>(StringComparer.Ordinal), Grace, Now);

        deleted.Should().Be(0);
        File.Exists(outside).Should().BeTrue();
        File.Exists(snapshot).Should().BeTrue();
        File.Exists(notAPicture).Should().BeTrue();
    }

    private static void Stored(ArtCache cache, string name, DateTimeOffset writtenAt)
    {
        cache.Write(new EncodedArt(240, 240, name, [1]));
        File.SetLastWriteTimeUtc(Path.Combine(cache.Path, name), writtenAt.UtcDateTime);
    }
}
