namespace mVasu.Api.Contracts;

public sealed record HealthCheckDto(
    string Status,
    string Version,
    double UptimeSeconds,
    DateTimeOffset Timestamp);
