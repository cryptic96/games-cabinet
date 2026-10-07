using Cabinet.UnitTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cabinet.UnitTests.Configuration;

/// <summary>
/// Proves the committed log levels keep the outgoing request logging of the HTTP client quiet in every environment. The
/// client logs the full address of each request, and the address of a collection request carries the owner's username, so
/// those lines must never be written at the committed levels.
/// </summary>
[Trait("Category", "Configuration")]
public class CommittedLogLevelTests
{
    private static readonly string[] RequestLogCategories =
    [
        "System.Net.Http.HttpClient.ICollectionSource.LogicalHandler",
        "System.Net.Http.HttpClient.ICollectionSource.ClientHandler",
    ];

    [Theory]
    [InlineData(null)]
    [InlineData("Development")]
    [InlineData("Production")]
    public void The_http_client_request_lines_are_not_written_below_warning_in_any_environment(string? environment)
    {
        using var factory = CreateFactory(environment);

        foreach (var category in RequestLogCategories)
        {
            var logger = factory.CreateLogger(category);

            logger.IsEnabled(LogLevel.Trace).Should().BeFalse($"{category} in {environment ?? "the base settings"}");
            logger.IsEnabled(LogLevel.Debug).Should().BeFalse($"{category} in {environment ?? "the base settings"}");
            logger.IsEnabled(LogLevel.Information).Should().BeFalse($"{category} in {environment ?? "the base settings"}");
            logger.IsEnabled(LogLevel.Warning).Should().BeTrue($"{category} in {environment ?? "the base settings"}");
        }
    }

    [Fact]
    public void The_committed_http_client_level_is_set_explicitly_to_warning_or_higher()
    {
        var configuration = Load(null);

        var level = configuration["Logging:LogLevel:System.Net.Http.HttpClient"];

        Enum.TryParse<LogLevel>(level, out var parsed).Should().BeTrue();
        (parsed >= LogLevel.Warning).Should().BeTrue();
    }

    private static ILoggerFactory CreateFactory(string? environment)
    {
        var configuration = Load(environment);

        return LoggerFactory.Create(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddProvider(new NullProvider());
        });
    }

    private static IConfigurationRoot Load(string? environment)
    {
        var directory = RepositoryPaths.ServiceDirectory();
        var builder = new ConfigurationBuilder().AddJsonFile(Path.Combine(directory, "appsettings.json"), optional: false);

        if (environment is not null)
        {
            builder.AddJsonFile(Path.Combine(directory, $"appsettings.{environment}.json"), optional: false);
        }

        return builder.Build();
    }

    private sealed class NullProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new NullLogger();

        public void Dispose()
        {
        }
    }

    private sealed class NullLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }
}
