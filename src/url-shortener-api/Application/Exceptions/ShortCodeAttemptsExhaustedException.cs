namespace UrlShortener.Application.Exceptions;

public sealed class ShortCodeAttemptsExhaustedException : Exception
{
    public const string ErrorCode = "short_code_capacity_exhausted";

    public ShortCodeAttemptsExhaustedException()
        : base("A unique short code could not be generated after the allowed attempts.") { }
}
