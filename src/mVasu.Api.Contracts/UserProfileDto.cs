namespace mVasu.Api.Contracts;

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? PreferredName,
    string Theme,
    string Language,
    bool LocationConsent);
