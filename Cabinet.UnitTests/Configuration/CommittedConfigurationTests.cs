using Cabinet.UnitTests.Infrastructure;
using System.Text.Json;
using FluentAssertions;

namespace Cabinet.UnitTests.Configuration;

/// <summary>Proves every committed appsettings file parses and never carries a non-empty secret-shaped value.</summary>
public class CommittedConfigurationTests
{
    private static readonly string[] SecretMarkers = ["password", "secret", "token", "apikey"];

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
    public void Secret_detection_flags_a_non_empty_secret_shaped_value()
    {
        using var document = JsonDocument.Parse("""{ "Section": { "ApiToken": "example-value", "Name": "plain" } }""");
        var violations = new List<string>();

        CollectSecretViolations(document.RootElement, string.Empty, violations);

        violations.Should().ContainSingle().Which.Should().Be("Section:ApiToken");
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
