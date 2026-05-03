using System.Security.Claims;
using mVasu.Api.Contracts;

namespace mVasu.Api.Tasks;

/// <summary>
/// Aggregates per-user heterogeneous tasks across XAF entity sources
/// and returns them as day-grouped <see cref="TaskGroupDto"/>s. See
/// BACKEND.md §2 for the entity-to-type mapping rules.
/// </summary>
public interface ITaskQueryService
{
    Task<TasksResponseDto> ListAsync(
        ClaimsPrincipal principal,
        TaskQueryParameters query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Bound from the <c>GET /api/tasks</c> query string.
/// </summary>
/// <param name="From">
///   Inclusive lower bound for time-bound entities (Tutustumiskaynti.Alkuaika,
///   Esittelyaika, Aikataulu, …). Defaults to "today".
/// </param>
/// <param name="To">
///   Inclusive upper bound. Defaults to <c>From</c> + 14 days.
/// </param>
/// <param name="UserId">
///   "me" or an explicit user id (manager / admin scope). The service
///   resolves "me" via <see cref="ClaimsPrincipal"/>.
/// </param>
/// <param name="Types">
///   Optional filter on TaskType identifiers — see <see cref="TaskTypeNames"/>.
/// </param>
/// <param name="UrgentOnly">
///   When true, only urgent rows are returned regardless of date window.
/// </param>
public sealed record TaskQueryParameters(
    DateOnly? From,
    DateOnly? To,
    string UserId,
    IReadOnlyList<string>? Types,
    bool UrgentOnly);
