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

    /// <summary>Every line logged so far, oldest first.</summary>
    public IReadOnlyList<string> Lines => [.. _lines];

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_lines);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            var message = formatter(state, exception);
            lines.Enqueue(exception is null ? message : $"{message} [{exception.GetType().FullName}]");
        }
    }
}
