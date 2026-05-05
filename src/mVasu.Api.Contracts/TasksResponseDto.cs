namespace mVasu.Api.Contracts;

/// <summary>
/// Top-level <c>GET /api/tasks</c> envelope. <see cref="Total"/> covers
/// every task across the visible groups so the client can render a
/// summary count without re-summing. <see cref="GeneratedAt"/> stamps the
/// server-side aggregation moment for the cache headers and the
/// "Päivitetty"-toast on pull-refresh.
/// </summary>
public sealed record TasksResponseDto(
    IReadOnlyList<TaskGroupDto> Groups,
    int Total,
    DateTimeOffset GeneratedAt);
