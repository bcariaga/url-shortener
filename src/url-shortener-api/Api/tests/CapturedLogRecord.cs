using Microsoft.Extensions.Logging;

namespace Api.Tests;

public sealed record CapturedLogRecord(
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, object?> State);
