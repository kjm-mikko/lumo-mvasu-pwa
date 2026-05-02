namespace mVasu.Api.Contracts;

public sealed record UpdateSettingsDto(
    string? PreferredName,
    string Theme,
    string Language);
