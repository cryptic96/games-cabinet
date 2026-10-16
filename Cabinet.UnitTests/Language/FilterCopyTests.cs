using System.Text.Json;
using Cabinet.Service.Language;
using FluentAssertions;

namespace Cabinet.UnitTests.Language;

/// <summary>
/// Runs the shared copy cases against the server's labels. The page script's string tables are tested against the same
/// cases file, so a phrase that both the server and the page show cannot drift between them.
/// </summary>
public class FilterCopyTests
{
    private static readonly Dictionary<string, Func<SiteText, JsonElement[], string>> Writers = new()
    {
        ["playersSuffix"] = (text, args) => text.PlayersSuffix(args[0].GetInt32()),
        ["timeSuffix"] = (text, args) => text.TimeSuffix(args[0].GetInt32()),
    };

    [Fact]
    public void The_cases_file_holds_cases_for_the_server()
    {
        ReadCases().Should().NotBeEmpty();
    }

    [Fact]
    public void Every_case_for_the_server_gives_exactly_the_contracted_text()
    {
        foreach (var entry in ReadCases())
        {
            Writers.Should().ContainKey(entry.Key, $"the server has no writer for {entry.Key}");

            var text = entry.Language == "nl" ? SiteText.Dutch : SiteText.English;

            Writers[entry.Key](text, entry.Args).Should().Be(entry.Text, $"{entry.Key} in {entry.Language}");
        }
    }

    private static List<CopyCase> ReadCases()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindFixture()));

        return document.RootElement.EnumerateArray()
            .Where(entry => entry.GetProperty("stack").GetString() is "cs" or "both")
            .Select(entry => new CopyCase(
                entry.GetProperty("key").GetString()!,
                entry.GetProperty("language").GetString()!,
                entry.GetProperty("args").EnumerateArray().Select(argument => argument.Clone()).ToArray(),
                entry.GetProperty("text").GetString()!))
            .ToList();
    }

    private static string FindFixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "build", "tests", "fixtures", "filter-copy-cases.json");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("The shared copy case table was not found above the test output directory.");
    }

    private sealed record CopyCase(string Key, string Language, JsonElement[] Args, string Text);
}
