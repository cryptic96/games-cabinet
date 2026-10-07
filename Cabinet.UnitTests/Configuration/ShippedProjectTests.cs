using System.Xml.Linq;
using Cabinet.Repository.Storage;
using Cabinet.Service.Sync;
using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.UnitTests.Configuration;

/// <summary>
/// Proves the projects that make up a release depend only on each other, so the fake BGG and the test projects can never be
/// built into what is shipped.
/// </summary>
[Trait("Category", "Configuration")]
public class ShippedProjectTests
{
    private const string FakeBgg = "Cabinet.FakeBgg";

    private static readonly IReadOnlyDictionary<string, string[]> AllowedProjectReferences = new Dictionary<string, string[]>
    {
        ["Cabinet.Domain"] = [],
        ["Cabinet.Repository"] = ["Cabinet.Domain"],
        ["Cabinet.Service"] = ["Cabinet.Domain", "Cabinet.Repository"],
    };

    [Theory]
    [InlineData("Cabinet.Domain")]
    [InlineData("Cabinet.Repository")]
    [InlineData("Cabinet.Service")]
    public void A_shipped_project_references_only_the_shipped_projects_below_it(string project)
    {
        var document = XDocument.Load(ProjectFile(project));

        var projectReferences = document.Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(element.Attribute("Include")!.Value.Replace('\\', '/')));
        var packageReferences = document.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty);

        projectReferences.Should().BeEquivalentTo(AllowedProjectReferences[project]);
        packageReferences.Should().NotContain(reference => reference.Contains(FakeBgg, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void No_shipped_assembly_depends_on_the_fake_bgg_or_a_test_project()
    {
        var shipped = new[] { typeof(SyncEndpoints).Assembly, typeof(SnapshotStore).Assembly, typeof(Cabinet.Domain.Collection.ShrinkGuard).Assembly };

        var referenced = shipped.SelectMany(assembly => assembly.GetReferencedAssemblies()).Select(name => name.Name ?? string.Empty);

        referenced.Should().NotContain(name =>
            name.StartsWith(FakeBgg, StringComparison.OrdinalIgnoreCase)
            || name.Contains("UnitTests", StringComparison.OrdinalIgnoreCase)
            || name.Contains("IntegrationTests", StringComparison.OrdinalIgnoreCase));
    }

    private static string ProjectFile(string project)
    {
        var repositoryRoot = Directory.GetParent(RepositoryPaths.ServiceDirectory())!.FullName;
        var path = Path.Combine(repositoryRoot, project, $"{project}.csproj");

        File.Exists(path).Should().BeTrue($"{project} is one of the projects a release is built from");

        return path;
    }
}
