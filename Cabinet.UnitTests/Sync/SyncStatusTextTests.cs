using System.Text.Json;
using Cabinet.Service.Pages;
using FluentAssertions;

namespace Cabinet.UnitTests.Sync;

/// <summary>Verifies the first-paint wording of the sync status line, against the same case table the page script is tested with.</summary>
[Trait("Category", "Sync")]
public class SyncStatusTextTests
{
    private static readonly DateTimeOffset Synced = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan ThreeHours = TimeSpan.FromHours(3);

    public static TheoryData<int, string> SharedCases()
    {
        var data = new TheoryData<int, string>();

        foreach (var (seconds, text) in ReadCases())
        {
            data.Add(seconds, text);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SharedCases))]
    public void Relative_text_follows_the_shared_case_table(int elapsedSeconds, string expected)
    {
        SyncStatusText.Relative(TimeSpan.FromSeconds(elapsedSeconds)).Should().Be(expected);
    }

    [Fact]
    public void The_shared_case_table_covers_every_unit_boundary()
    {
        ReadCases().Select(entry => entry.Seconds).Should().Contain([-30, 59, 60, 3599, 3600, 86399, 86400, 172800]);
    }

    [Fact]
    public void The_exact_time_is_written_in_utc_with_the_invariant_culture()
    {
        SyncStatusText.ExactUtc(new DateTimeOffset(2026, 10, 6, 14, 32, 0, TimeSpan.FromHours(2)))
            .Should().Be("6 October 2026 at 12:32 UTC");
    }

    [Fact]
    public void Never_synced_is_never_stale()
    {
        SyncStatusText.IsStale(null, Synced, ThreeHours, heldBack: false).Should().BeFalse();
        SyncStatusText.IsStale(null, Synced, ThreeHours, heldBack: true).Should().BeFalse();
    }

    [Fact]
    public void Two_hours_fifty_nine_minutes_is_not_stale_and_exactly_three_hours_is()
    {
        SyncStatusText.IsStale(Synced, Synced + TimeSpan.FromMinutes(179), ThreeHours, heldBack: false).Should().BeFalse();
        SyncStatusText.IsStale(Synced, Synced + ThreeHours, ThreeHours, heldBack: false).Should().BeTrue();
    }

    [Fact]
    public void A_held_back_result_is_stale_at_any_age()
    {
        SyncStatusText.IsStale(Synced, Synced + TimeSpan.FromMinutes(1), ThreeHours, heldBack: true).Should().BeTrue();
    }

    [Fact]
    public void A_last_sync_in_the_future_is_not_stale()
    {
        SyncStatusText.IsStale(Synced, Synced - TimeSpan.FromMinutes(1), ThreeHours, heldBack: false).Should().BeFalse();
    }

    private static List<(int Seconds, string Text)> ReadCases()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindFixture()));

        return document.RootElement.EnumerateArray()
            .Select(entry => (entry.GetProperty("elapsedSeconds").GetInt32(), entry.GetProperty("text").GetString()!))
            .ToList();
    }

    private static string FindFixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "build", "tests", "fixtures", "relative-time-cases.json");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("The shared relative-time case table was not found above the test output directory.");
    }
}
