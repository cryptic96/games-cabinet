using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>
/// Remembers every line the host logs, as the formatted message plus the type name of any exception, so a test can prove a
/// secret never reached a log. Register it as an <see cref="ILoggerProvider"/> so the committed log-level filters apply to
/// it exactly as they do in production.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _lines = new();
    private readonly ConcurrentQueue<string> _problems = new();

    /// <summary>Every line logged so far, oldest first.</summary>
    public IReadOnlyList<string> Lines => [.. _lines];

    /// <summary>The lines logged so far at Warning or above, oldest first.</summary>
    public IReadOnlyList<string> Problems => [.. _problems];

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_lines, _problems);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> lines, ConcurrentQueue<string> problems) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            var message = formatter(state, exception);
            var line = exception is null ? message : $"{message} [{exception.GetType().FullName}]";
            lines.Enqueue(line);

            if (logLevel >= LogLevel.Warning)
            {
                problems.Enqueue(line);
            }
        }
    }
}
