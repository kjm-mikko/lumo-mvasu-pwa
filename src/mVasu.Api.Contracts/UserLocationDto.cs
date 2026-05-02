namespace mVasu.Api.Contracts;

public sealed record UserLocationDto(
    double Latitude,
    double Longitude,
    double? Accuracy,
    DateTimeOffset RecordedAt);
