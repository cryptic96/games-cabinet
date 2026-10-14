using System.Net;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the sample layout the owner's look preview draws and the script that draws it are owner-only: both answer an empty not found
/// on the public port, and the sample is served as JSON on the owner port.
/// </summary>
[Trait("Category", "Owner")]
public sealed class OwnerLookSampleTests
{
    private const string SamplePath = "/js/owner-look-sample.json";
    private const string ScriptPath = "/js/owner-look.js";

    [Theory]
    [InlineData(SamplePath)]
    [InlineData(ScriptPath)]
    public async Task The_look_preview_files_answer_not_found_on_the_public_port(string path)
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var publicClient = factory.CreatePublicClient();

        using var response = await publicClient.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task The_sample_layout_is_served_as_json_on_the_owner_port()
    {
        await using var factory = new CabinetWebApplicationFactory();
        using var owner = factory.CreateOwnerClient();

        using var response = await owner.GetAsync(SamplePath, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("\"sections\"");
    }
}
