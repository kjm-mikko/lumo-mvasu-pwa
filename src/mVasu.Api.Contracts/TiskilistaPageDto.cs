namespace mVasu.Api.Contracts;

public sealed record TiskilistaPageDto(
    IReadOnlyList<TiskilistaCardDto> Items,
    int Total,
    int Page,
    int PageSize);
