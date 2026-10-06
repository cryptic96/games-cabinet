using Cabinet.Domain.Layout;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>Pins the seeded generator's output, because every synthetic collection and its layout depends on it.</summary>
public class SplitMix64Tests
{
    [Fact]
    [Trait("Category", "Layout")]
    public void Seed_zero_gives_the_published_sequence()
    {
        var generator = new SplitMix64(0);

        generator.Next().Should().Be(0xE220A8397B1DCDAFUL);
        generator.Next().Should().Be(0x6E789E6AA1B965F4UL);
        generator.Next().Should().Be(0x06C45D188009454FUL);
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_same_seed_gives_the_same_whole_numbers()
    {
        var first = new SplitMix64(42);
        var second = new SplitMix64(42);

        for (var step = 0; step < 100; step++)
        {
            first.NextInt(-5, 500).Should().Be(second.NextInt(-5, 500), "step {0}", step);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Whole_numbers_stay_inside_the_requested_range()
    {
        var generator = new SplitMix64(7);

        for (var step = 0; step < 1000; step++)
        {
            generator.NextInt(10, 14).Should().BeInRange(10, 13, "step {0}", step);
        }
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void An_empty_range_is_rejected()
    {
        var act = () => new SplitMix64(1).NextInt(5, 5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void Mix_of_the_first_stepped_state_gives_the_first_output()
    {
        SplitMix64.Mix(0x9E3779B97F4A7C15UL).Should().Be(0xE220A8397B1DCDAFUL);
    }
}
