using Microsoft.Extensions.Logging;

namespace Api.Tests;

public sealed class CapturingLogger<T> : ILogger<T>
{
    public List<CapturedLogRecord> Records { get; }

    public CapturingLogger() : this([]) { }
    public CapturingLogger(List<CapturedLogRecord> records) => Records = records;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => Noop.Instance;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? pairs.Where(pair => pair.Key != "{OriginalFormat}").ToDictionary(pair => pair.Key, pair => pair.Value)
            : new Dictionary<string, object?>();
        Records.Add(new CapturedLogRecord(logLevel, eventId, formatter(state, exception), values));
    }

    private sealed class Noop : IDisposable
    {
        public static readonly Noop Instance = new();
        public void Dispose() { }
    }
}
