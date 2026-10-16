using System.Security.Cryptography;
using Cabinet.Repository.Images;

namespace Cabinet.Service.Owner;

/// <summary>A linked picture that was fetched and processed and waits for the owner to use or discard it.</summary>
/// <param name="GameId">The game the owner checked the link for.</param>
/// <param name="CollectionId">The copy of that game the owner checked the link for; the picture can be used for that copy alone.</param>
/// <param name="Link">The processed picture: its address, its resized files and its record.</param>
/// <param name="Preview">The small file the preview shows, served from the site's own origin until the owner decides.</param>
public sealed record StagedLink(int GameId, long CollectionId, ProcessedLink Link, EncodedArt Preview);

/// <summary>
/// A small store in memory for things that wait for the owner's decision, such as a previewed picture. Each item is addressed
/// by a random token of 32 lowercase hexadecimal characters that the server makes; a token that has any other shape is never
/// looked up, so nothing a request carries can name a file or reach anything but an item staged here. At most
/// <see cref="Capacity"/> items are held, the oldest being dropped to make room, and an item is forgotten
/// <see cref="Lifetime"/> after it was staged, by the injected clock. Nothing is written to disk, and a restart forgets everything.
/// </summary>
/// <typeparam name="T">What is staged.</typeparam>
/// <param name="time">The clock the lifetime runs on.</param>
public sealed class StagedItems<T>(TimeProvider time)
    where T : notnull
{
    /// <summary>The most items held at once.</summary>
    public const int Capacity = 8;

    /// <summary>The number of characters in a token.</summary>
    public const int TokenLength = 32;

    /// <summary>How long an item stays after it was staged.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly object _gate = new();
    private readonly List<Entry> _entries = [];

    /// <summary>Tells whether a text has the shape of a token: 32 characters, each a lowercase hexadecimal digit.</summary>
    /// <param name="text">The text, or null.</param>
    public static bool IsToken(string? text) =>
        text is { Length: TokenLength } && text.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>Holds an item and gives the token it is addressed by.</summary>
    /// <param name="item">The item to hold.</param>
    public string Stage(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var now = time.GetUtcNow();
        var token = RandomNumberGenerator.GetHexString(TokenLength, lowercase: true);

        lock (_gate)
        {
            Forget(now);

            while (_entries.Count >= Capacity)
            {
                _entries.RemoveAt(0);
            }

            _entries.Add(new Entry(token, item, now + Lifetime));
        }

        return token;
    }

    /// <summary>Finds the item a token addresses, when it is still held.</summary>
    /// <param name="token">The token as it arrived; anything that is not the shape of a token finds nothing.</param>
    /// <param name="item">The item, when it was found.</param>
    public bool TryGet(string? token, out T item)
    {
        item = default!;

        if (!IsToken(token))
        {
            return false;
        }

        lock (_gate)
        {
            Forget(time.GetUtcNow());

            var entry = _entries.Find(candidate => candidate.Token == token);

            if (entry is null)
            {
                return false;
            }

            item = entry.Item;

            return true;
        }
    }

    /// <summary>Forgets the item a token addresses.</summary>
    /// <param name="token">The token as it arrived.</param>
    /// <returns>Whether an item was held under it.</returns>
    public bool Remove(string? token)
    {
        if (!IsToken(token))
        {
            return false;
        }

        lock (_gate)
        {
            return _entries.RemoveAll(candidate => candidate.Token == token) > 0;
        }
    }

    private void Forget(DateTimeOffset now) => _entries.RemoveAll(entry => entry.ExpiresAt <= now);

    private sealed record Entry(string Token, T Item, DateTimeOffset ExpiresAt);
}
