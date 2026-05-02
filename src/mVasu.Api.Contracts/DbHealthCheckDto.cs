namespace mVasu.Api.Contracts;

public sealed record DbHealthCheckDto(
    bool Connected,
    double DurationMs,
    string? Error);
