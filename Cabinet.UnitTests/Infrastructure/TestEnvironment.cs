using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Cabinet.UnitTests.Infrastructure;

/// <summary>A host environment with only a name, for code that reads nothing else from it.</summary>
/// <param name="environmentName">The environment name, such as Development or Production.</param>
public sealed class TestEnvironment(string environmentName) : IHostEnvironment
{
    /// <inheritdoc />
    public string EnvironmentName { get; set; } = environmentName;

    /// <inheritdoc />
    public string ApplicationName { get; set; } = "Cabinet.UnitTests";

    /// <inheritdoc />
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    /// <inheritdoc />
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
