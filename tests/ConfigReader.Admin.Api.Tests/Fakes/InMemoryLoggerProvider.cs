using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ConfigReader.Admin.Api.Tests.Fakes;

public sealed record CapturedLog(LogLevel Level, string Message, Exception? Exception);

/// <summary>Captures log output from the hosted API so tests can assert that a failure was
/// logged (e.g. an audit-write failure must not be silently swallowed).</summary>
public sealed class CapturedLogs
{
    private readonly ConcurrentQueue<CapturedLog> _logs = new();

    public void Add(CapturedLog log) => _logs.Enqueue(log);

    public IReadOnlyList<CapturedLog> Entries => _logs.ToList();
}

public sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly CapturedLogs _logs;

    public InMemoryLoggerProvider(CapturedLogs logs) => _logs = logs;

    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(_logs);

    public void Dispose()
    {
    }

    private sealed class InMemoryLogger : ILogger
    {
        private readonly CapturedLogs _logs;

        public InMemoryLogger(CapturedLogs logs) => _logs = logs;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => _logs.Add(new CapturedLog(logLevel, formatter(state, exception), exception));
    }
}
