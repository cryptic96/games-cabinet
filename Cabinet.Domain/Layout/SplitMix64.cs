namespace Cabinet.Domain.Layout;

/// <summary>
/// A small seeded generator of 64-bit values whose output is fixed by its algorithm, so a seed gives the same
/// sequence on every machine and runtime version. It is the only source of variety in the project's synthetic data.
/// </summary>
/// <param name="seed">The starting state.</param>
public sealed class SplitMix64(ulong seed)
{
    private const ulong Step = 0x9E3779B97F4A7C15UL;
    private const ulong FirstMultiplier = 0xBF58476D1CE4E5B9UL;
    private const ulong SecondMultiplier = 0x94D049BB133111EBUL;

    private ulong _state = seed;

    /// <summary>Advances the state and returns the next value.</summary>
    public ulong Next()
    {
        unchecked
        {
            _state += Step;
        }

        return Mix(_state);
    }

    /// <summary>Returns a whole number from <paramref name="minInclusive"/> up to but not including <paramref name="maxExclusive"/>.</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxExclusive, minInclusive);

        var span = (ulong)((long)maxExclusive - minInclusive);

        return (int)(minInclusive + (long)(Next() % span));
    }

    /// <summary>The finishing mix of the generator without the state step: a well-spread value for any input.</summary>
    public static ulong Mix(ulong value)
    {
        unchecked
        {
            var z = value;
            z = (z ^ (z >> 30)) * FirstMultiplier;
            z = (z ^ (z >> 27)) * SecondMultiplier;
            return z ^ (z >> 31);
        }
    }
}
