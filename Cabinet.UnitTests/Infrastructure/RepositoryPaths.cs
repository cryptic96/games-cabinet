namespace Cabinet.UnitTests.Infrastructure;

/// <summary>Finds the committed source folders from the test output directory, for tests that read the shipped files.</summary>
public static class RepositoryPaths
{
    /// <summary>The folder of the web service project, found by walking up from the test output directory.</summary>
    /// <exception cref="DirectoryNotFoundException">No folder above the test output holds the web service project.</exception>
    public static string ServiceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Cabinet.Service");

            if (File.Exists(Path.Combine(candidate, "Cabinet.Service.csproj")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Cabinet.Service was not found above the test output directory.");
    }
}
