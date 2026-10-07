using System.Globalization;

namespace Cabinet.FakeBgg;

/// <summary>How the fake answers: normally, or in one of the ways the real service misbehaves.</summary>
/// <param name="Name">The scenario name, one of <see cref="Names"/>.</param>
/// <param name="QueuedCount">For <c>queued</c>, how many times each distinct request is answered with a wait before the data.</param>
/// <param name="SlowMilliseconds">For <c>slow</c>, how long every answer is held back.</param>
public sealed record FakeBggScenario(string Name, int QueuedCount = 0, int SlowMilliseconds = 0)
{
    /// <summary>Normal answers.</summary>
    public const string Normal = "normal";

    /// <summary>Every scenario name the fake understands; <c>queued</c> and <c>slow</c> take a number after an equals sign.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        "normal",
        "queued",
        "throttle",
        "slow",
        "broken",
        "malformed",
        "errors",
        "mismatch",
        "empty",
        "shrunk",
        "unauthorized",
        "unavailable",
    ];

    /// <summary>The normal scenario.</summary>
    public static FakeBggScenario Default { get; } = new(Normal);

    /// <summary>Parses text such as <c>normal</c>, <c>queued=2</c> or <c>slow=1500</c>; returns false when it is not a scenario.</summary>
    /// <param name="text">The scenario text.</param>
    /// <param name="scenario">The parsed scenario when the text is valid.</param>
    public static bool TryParse(string? text, out FakeBggScenario scenario)
    {
        scenario = Default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var separator = text.IndexOf('=');
        var name = (separator < 0 ? text : text[..separator]).Trim().ToLowerInvariant();
        var argument = separator < 0 ? null : text[(separator + 1)..].Trim();

        switch (name)
        {
            case "queued" when TryCount(argument, out var count):
                scenario = new FakeBggScenario(name, QueuedCount: count);
                return true;
            case "slow" when TryCount(argument, out var milliseconds):
                scenario = new FakeBggScenario(name, SlowMilliseconds: milliseconds);
                return true;
            case "queued" or "slow":
                return false;
            default:
                if (argument is not null || !Names.Contains(name))
                {
                    return false;
                }

                scenario = new FakeBggScenario(name);
                return true;
        }
    }

    /// <summary>Parses scenario text and fails with a clear message when it is not a scenario.</summary>
    /// <param name="text">The scenario text.</param>
    public static FakeBggScenario Parse(string? text) =>
        TryParse(text, out var scenario)
            ? scenario
            : throw new FormatException($"Unknown scenario '{text}'. Use one of: {string.Join(", ", Names)} (queued=N and slow=ms take a number).");

    /// <inheritdoc />
    public override string ToString() => Name switch
    {
        "queued" => $"queued={QueuedCount.ToString(CultureInfo.InvariantCulture)}",
        "slow" => $"slow={SlowMilliseconds.ToString(CultureInfo.InvariantCulture)}",
        _ => Name,
    };

    private static bool TryCount(string? argument, out int value) =>
        int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
