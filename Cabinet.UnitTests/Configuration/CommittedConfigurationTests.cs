using System.Net;
using System.Text.Json;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.UnitTests.Configuration;

/// <summary>
/// Proves every committed appsettings file parses and never carries a non-empty secret-shaped value or a BGG account setting,
/// and that the committed env file example holds placeholders only.
/// </summary>
public class CommittedConfigurationTests
{
    private static readonly string[] SecretMarkers = ["password", "secret", "token", "apikey"];

    private static readonly string[] AccountKeys = ["Bgg:Username", "Bgg:Token", "Bgg:ContactUrl"];

    private static readonly string[] AccountVariables = ["Bgg__Username", "Bgg__Token", "Bgg__ContactUrl"];

    private static readonly string[] DocumentationRanges = ["192.0.2.", "198.51.100.", "203.0.113."];

    [Fact]
    [Trait("Category", "Configuration")]
    public void Every_committed_appsettings_file_parses_and_has_no_non_empty_secret_values()
    {
        var serviceDirectory = RepositoryPaths.ServiceDirectory();
        var appsettingsFiles = Directory.GetFiles(serviceDirectory, "appsettings*.json", SearchOption.TopDirectoryOnly);

        appsettingsFiles.Should().NotBeEmpty();

        foreach (var path in appsettingsFiles)
        {
            var json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);

            var violations = new List<string>();
            CollectSecretViolations(document.RootElement, string.Empty, violations);

            violations.Should().BeEmpty($"file {Path.GetFileName(path)} must not carry a non-empty secret value");
        }
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void The_committed_layout_section_turns_series_grouping_on()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryPaths.ServiceDirectory(), "appsettings.json")));

        document.RootElement.GetProperty("Layout").GetProperty("GroupSeries").GetBoolean().Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void The_committed_layout_section_makes_a_base_game_with_two_owned_expansions_face_out()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryPaths.ServiceDirectory(), "appsettings.json")));

        document.RootElement.GetProperty("Layout").GetProperty("CoverFromExpansions").GetInt32().Should().Be(2);
    }

    [Fact]
    public void Secret_detection_flags_a_non_empty_secret_shaped_value()
    {
        using var document = JsonDocument.Parse("""{ "Section": { "ApiToken": "example-value", "Name": "plain" } }""");
        var violations = new List<string>();

        CollectSecretViolations(document.RootElement, string.Empty, violations);

        violations.Should().ContainSingle().Which.Should().Be("Section:ApiToken");
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void No_committed_appsettings_file_sets_the_bgg_username_token_or_contact_address()
    {
        var appsettingsFiles = Directory.GetFiles(RepositoryPaths.ServiceDirectory(), "appsettings*.json", SearchOption.TopDirectoryOnly);

        appsettingsFiles.Should().NotBeEmpty();

        foreach (var path in appsettingsFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));

            AccountViolations(document.RootElement).Should().BeEmpty($"{Path.GetFileName(path)} must leave the BGG account to the server env file");
        }
    }

    [Fact]
    public void Account_detection_flags_any_letter_case_and_ignores_empty_values()
    {
        using var document = JsonDocument.Parse("""{ "bgg": { "USERNAME": "example-person", "Token": "", "ContactUrl": null }, "Other": { "Username": "plain" } }""");

        AccountViolations(document.RootElement).Should().ContainSingle().Which.Should().Be("bgg:USERNAME");
    }

    [Fact]
    [Trait("Category", "Configuration")]
    public void The_env_file_example_holds_only_placeholders_for_the_bgg_account_and_documentation_addresses()
    {
        var path = Path.Combine(Directory.GetParent(RepositoryPaths.ServiceDirectory())!.FullName, "deploy", "cabinet.env.example");
        var assignments = File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('=', 2))
            .Select(parts => (Name: parts[0].Trim(), Value: parts.Length > 1 ? parts[1].Trim() : string.Empty))
            .ToList();

        foreach (var variable in AccountVariables)
        {
            assignments.Count(assignment => assignment.Name == variable).Should().Be(1, $"{variable} appears once as a placeholder");
        }

        Value(assignments, "Bgg__Username").Should().StartWith("replace-with-");
        Value(assignments, "Bgg__Token").Should().StartWith("replace-with-");
        var contact = new Uri(Value(assignments, "Bgg__ContactUrl"));
        contact.Scheme.Should().Be(Uri.UriSchemeHttps);
        contact.Host.Should().Match(host => IsPlaceholderHost(host));
        assignments.Where(assignment => IPAddress.TryParse(assignment.Value, out _))
            .Should().OnlyContain(assignment => DocumentationRanges.Any(range => assignment.Value.StartsWith(range, StringComparison.Ordinal)));
    }

    private static string Value(IEnumerable<(string Name, string Value)> assignments, string name) =>
        assignments.Single(assignment => assignment.Name == name).Value;

    private static bool IsPlaceholderHost(string host) =>
        host is "example.com" or "example.org"
        || host.EndsWith(".example.com", StringComparison.Ordinal)
        || host.EndsWith(".example.org", StringComparison.Ordinal);

    private static List<string> AccountViolations(JsonElement root)
    {
        var violations = new List<string>();
        CollectAccountViolations(root, string.Empty, violations);

        return violations;
    }

    private static void CollectAccountViolations(JsonElement element, string path, List<string> violations)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            var propertyPath = path.Length == 0 ? property.Name : $"{path}:{property.Name}";

            if (AccountKeys.Contains(propertyPath, StringComparer.OrdinalIgnoreCase) && HasNonEmptyValue(property.Value))
            {
                violations.Add(propertyPath);
            }

            CollectAccountViolations(property.Value, propertyPath, violations);
        }
    }

    private static void CollectSecretViolations(JsonElement element, string path, List<string> violations)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = path.Length == 0 ? property.Name : $"{path}:{property.Name}";

                    if (LooksLikeSecretKey(property.Name) && HasNonEmptyValue(property.Value))
                    {
                        violations.Add(propertyPath);
                    }

                    CollectSecretViolations(property.Value, propertyPath, violations);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    CollectSecretViolations(item, $"{path}[{index}]", violations);
                    index++;
                }

                break;
        }
    }

    private static bool LooksLikeSecretKey(string keyName) =>
        SecretMarkers.Any(marker => keyName.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static bool HasNonEmptyValue(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => !string.IsNullOrEmpty(value.GetString()),
            JsonValueKind.Null => false,
            _ => true
        };
}
