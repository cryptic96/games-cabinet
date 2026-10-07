using System.Text.Json;
using Cabinet.Domain.Collection;
using Microsoft.Extensions.Logging;

namespace Cabinet.Repository.Storage;

/// <summary>Keeps the sync bookkeeping as <c>sync-state.json</c> in the storage directory.</summary>
public sealed class SyncStateStore : ISyncStateStore
{
    /// <summary>The name of the stored file.</summary>
    public const string FileName = "sync-state.json";

    /// <summary>The name a damaged or too-new file is renamed to. A file that could not be read at all is left in place.</summary>
    public const string SetAsideFileName = "sync-state.json.bad";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private const int FingerprintLength = 64;

    private readonly string _path;
    private readonly TimeProvider _time;
    private readonly TimeSpan _manualCooldown;
    private readonly ILogger<SyncStateStore> _logger;

    /// <summary>Creates a store over a directory.</summary>
    /// <param name="directory">The storage directory; it must exist.</param>
    /// <param name="time">The clock a loaded cooldown is checked against.</param>
    /// <param name="manualCooldown">How long a sync that starts now keeps visitors from asking for another; no stored cooldown may end later than this from now.</param>
    /// <param name="logger">Receives one line when a stored file has to be set aside or could not be read, or a stored value is corrected.</param>
    public SyncStateStore(string directory, TimeProvider time, TimeSpan manualCooldown, ILogger<SyncStateStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThan(manualCooldown, TimeSpan.Zero);

        _path = Path.Combine(directory, FileName);
        _time = time;
        _manualCooldown = manualCooldown;
        _logger = logger;
    }

    /// <inheritdoc />
    public SyncState Load()
    {
        byte[] content;

        try
        {
            if (!File.Exists(_path))
            {
                return SyncState.Initial;
            }

            content = File.ReadAllBytes(_path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("The stored sync state could not be read ({Reason}) and was left in place.", "unreadable");

            return SyncState.Initial;
        }

        return Parse(content);
    }

    /// <inheritdoc />
    public void Save(SyncState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        AtomicJsonFile.WriteAtomically(_path, JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions));
    }

    private SyncState Parse(byte[] content)
    {
        SyncState? state;

        try
        {
            state = JsonSerializer.Deserialize<SyncState>(content, JsonOptions);
        }
        catch (JsonException)
        {
            return SetAside("malformed");
        }

        if (state is null || state.SchemaVersion < 1 || state.ConsecutiveFailures < 0)
        {
            return SetAside("malformed");
        }

        return state.SchemaVersion > SyncState.CurrentSchemaVersion ? SetAside("newer schema") : Sanitize(state);
    }

    private SyncState Sanitize(SyncState state)
    {
        var heldBack = state.HeldBack;

        if (heldBack is not null && !IsWellFormedFingerprint(heldBack.Fingerprint))
        {
            _logger.LogWarning("The stored sync state held an answer set aside with a malformed fingerprint; it was dropped.");
            heldBack = null;
        }

        var cooldownEnds = state.CooldownEndsUtc;
        var latestSensible = _time.GetUtcNow() + _manualCooldown;

        if (cooldownEnds is { } ends && ends > latestSensible)
        {
            _logger.LogWarning("The stored sync state had a cooldown ending later than the cooldown allows; it was shortened.");
            cooldownEnds = latestSensible;
        }

        return state with { HeldBack = heldBack, CooldownEndsUtc = cooldownEnds };
    }

    private static bool IsWellFormedFingerprint(string fingerprint) =>
        fingerprint.Length == FingerprintLength && fingerprint.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private SyncState SetAside(string reason)
    {
        try
        {
            File.Move(_path, Path.Combine(Path.GetDirectoryName(_path)!, SetAsideFileName), overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("The stored sync state could not be read ({Reason}) and could not be set aside.", reason);

            return SyncState.Initial;
        }

        _logger.LogWarning("The stored sync state could not be read ({Reason}) and was set aside; it starts afresh.", reason);

        return SyncState.Initial;
    }

    private static JsonSerializerOptions CreateJsonOptions() => StoredJson.CreateOptions(new Dictionary<Type, string[]>());
}
