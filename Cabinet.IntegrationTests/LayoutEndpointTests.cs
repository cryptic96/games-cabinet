using System.Net;
using System.Text.Json;
using Cabinet.Domain.Layout;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies the layout endpoint answers for allowlisted samples and profiles and refuses everything else.</summary>
public class LayoutEndpointTests
{
    [Fact]
    public async Task Layout_for_the_default_sample_is_json_with_sections_cubbies_and_placements()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync("/cabinet/layout?sample=65&profile=desktop", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var sections = root.GetProperty("sections");
        var placementCount = sections.EnumerateArray()
            .SelectMany(section => section.GetProperty("cubbies").EnumerateArray())
            .Sum(cubby => cubby.GetProperty("placements").GetArrayLength());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        root.GetProperty("layoutVersion").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        root.GetProperty("profile").GetString().Should().Be("desktop");
        sections.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
        sections[0].GetProperty("cubbies").GetArrayLength().Should().Be(SectionDesigns.Desktop.Cubbies.Count);
        placementCount.Should().Be(65);
    }

    [Fact]
    public async Task Layout_for_the_empty_sample_is_one_section_of_bare_cubbies()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        var body = await client.GetStringAsync("/cabinet/layout?sample=0&profile=desktop", TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var sections = document.RootElement.GetProperty("sections");
        var cubbies = sections[0].GetProperty("cubbies");

        sections.GetArrayLength().Should().Be(1);
        cubbies.GetArrayLength().Should().Be(SectionDesigns.Desktop.Cubbies.Count);
        cubbies.EnumerateArray().Should().OnlyContain(cubby => cubby.GetProperty("placements").GetArrayLength() == 0);
    }

    [Theory]
    [InlineData("sample=64&profile=desktop")]
    [InlineData("sample=-1&profile=desktop")]
    [InlineData("sample=abc&profile=desktop")]
    [InlineData("sample=&profile=desktop")]
    [InlineData("profile=desktop")]
    [InlineData("sample=65&profile=tablet")]
    [InlineData("sample=65&profile=Desktop")]
    [InlineData("sample=65&profile=")]
    [InlineData("sample=65")]
    public async Task Layout_answers_not_found_outside_the_allowlists(string query)
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var client = factory.CreatePublicClient();

        using var response = await client.GetAsync($"/cabinet/layout?{query}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
