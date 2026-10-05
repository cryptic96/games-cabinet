using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>Pins the stable hash to known outputs so a game's decisions never change between runtime versions.</summary>
public class StableHashTests
{
    [Theory]
    [InlineData(0L, 0, 0x813F0174A2367C13UL)]
    [InlineData(12345L, 1, 0xD624FAE7AEE7FA43UL)]
    [InlineData(-7L, 100, 0x5BE07E6DC110A81BUL)]
    [Trait("Category", "Layout")]
    public void Hash_matches_values_computed_independently(long id, int salt, ulong expected)
    {
        StableHash.Hash(id, salt).Should().Be(expected);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Bucket_is_the_hash_modulo_the_modulus()
    {
        StableHash.Bucket(98765, StableHash.FlatSalt, 10000).Should().Be(568);
        StableHash.Bucket(42, StableHash.OptionsSalt, 10000).Should().Be(6671);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void A_different_salt_gives_a_different_value_for_the_same_game()
    {
        var salts = new[] { StableHash.CoverSalt, StableHash.FlatSalt, StableHash.ToneSalt, StableHash.PatternSalt, StableHash.OptionsSalt };

        salts.Select(salt => StableHash.Hash(31337, salt)).Distinct().Should().HaveCount(salts.Length);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Bucket_rejects_a_modulus_that_is_not_positive()
    {
        var act = () => StableHash.Bucket(1, StableHash.CoverSalt, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Buckets_spread_across_the_range_for_consecutive_games()
    {
        var buckets = Enumerable.Range(1, 2000).Select(id => StableHash.Bucket(id, StableHash.CoverSalt, 10)).ToList();

        for (var bucket = 0; bucket < 10; bucket++)
        {
            buckets.Count(value => value == bucket).Should().BeInRange(120, 280, "bucket {0}", bucket);
        }
    }
}
