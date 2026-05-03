namespace mVasu.Api.Contracts;

/// <summary>
/// Day-bucket wrapper around a list of tasks. The four canonical group
/// ids — today / tomorrow / this-week / later — match the headers the
/// frontend renders. The group <see cref="Date"/> is pre-localised by
/// the server (Europe/Helsinki) so the client renders it verbatim.
/// </summary>
public sealed record TaskGroupDto(
    string Id,
    string Label,
    string Date,
    IReadOnlyList<TaskDto> Tasks);

/// <summary>Canonical group identifiers — see BACKEND.md §1.</summary>
public static class TaskGroupIds
{
    public const string Today    = "today";
    public const string Tomorrow = "tomorrow";
    public const string ThisWeek = "this-week";
    public const string Later    = "later";
}
