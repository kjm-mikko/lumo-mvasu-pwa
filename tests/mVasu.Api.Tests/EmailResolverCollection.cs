namespace mVasu.Api.Tests;

/// <summary>
/// xunit collection definition that serialises every test touching the
/// process-wide impersonation field on <c>EmailResolver</c>. Without
/// this, <c>EmailResolverImpersonationTests</c> and <c>EmailResolverTests</c>
/// can run in parallel; the static field set by one then leaks into the
/// other.
/// </summary>
[CollectionDefinition(Name)]
public sealed class EmailResolverCollection
{
    public const string Name = "EmailResolverStatic";
}
