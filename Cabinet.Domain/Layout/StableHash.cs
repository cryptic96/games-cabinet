namespace Cabinet.Domain.Layout;

/// <summary>
/// A hash of a game identifier and a salt whose output is fixed by its algorithm, so a game gets the same value on every
/// machine and runtime version. Each decision about a game uses its own salt, so the decisions are independent of each
/// other and of every other game in the collection.
/// </summary>
public static class StableHash
{
    /// <summary>Salt for the decision whether a box faces out.</summary>
    public const int CoverSalt = 1;

    /// <summary>Salt for the decision whether a small or thin box lies flat.</summary>
    public const int FlatSalt = 2;

    /// <summary>Salt for the colour table index of a game.</summary>
    public const int ToneSalt = 7;

    /// <summary>Salt for the cover pattern index of a game.</summary>
    public const int PatternSalt = 8;

    /// <summary>Salt for the fingerprint of the layout settings.</summary>
    public const int OptionsSalt = 9;

    /// <summary>The first salt used to order the boxes inside a cubby; the cubby's position in the cabinet is added to it.</summary>
    public const int CubbyOrderSaltBase = 100;

    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;
    private const int ByteCount = 8;
    private const int SaltShift = 32;
    private const int ByteMask = 0xFF;

    /// <summary>
    /// Hashes the identifier with the salt: FNV-1a over the eight little-endian bytes of the combined value, followed by
    /// the finishing mix of <see cref="SplitMix64"/> so the low bits are as well spread as the high ones.
    /// </summary>
    public static ulong Hash(long id, int salt)
    {
        unchecked
        {
            var combined = (ulong)id ^ ((ulong)(uint)salt << SaltShift);
            var hash = OffsetBasis;

            for (var index = 0; index < ByteCount; index++)
            {
                hash ^= (combined >> (index * ByteCount)) & ByteMask;
                hash *= Prime;
            }

            return SplitMix64.Mix(hash);
        }
    }

    /// <summary>A whole number from zero up to but not including <paramref name="modulus"/>, fixed by the identifier and the salt.</summary>
    public static int Bucket(long id, int salt, int modulus)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(modulus, 0);

        return (int)(Hash(id, salt) % (ulong)modulus);
    }
}
