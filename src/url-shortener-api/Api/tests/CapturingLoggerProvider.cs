using Microsoft.Extensions.Logging;

namespace Api.Tests;

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public List<CapturedLogRecord> Records { get; } = [];

    public CapturingLoggerProvider() : this([]) { }
    public CapturingLoggerProvider(List<CapturedLogRecord> records) => Records = records;
    public ILogger CreateLogger(string categoryName) => new CapturingLogger<LogCategoryProxy>(Records);
    public void Dispose() { }

    private sealed class LogCategoryProxy { }
}
