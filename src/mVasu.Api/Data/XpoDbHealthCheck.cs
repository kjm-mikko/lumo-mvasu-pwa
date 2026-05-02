using System.Diagnostics;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using mVasu.Api.Contracts;

namespace mVasu.Api.Data;

public sealed class XpoDbHealthCheck(
    IObjectSpaceProvider provider,
    ILogger<XpoDbHealthCheck> logger) : IDbHealthCheck
{
    public Task<DbHealthCheckDto> CheckAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var os = provider.CreateObjectSpace();
            var session = ((XPObjectSpace)os).Session;
            session.ExecuteScalar("SELECT 1");
            sw.Stop();
            return Task.FromResult(new DbHealthCheckDto(
                Connected: true,
                DurationMs: sw.Elapsed.TotalMilliseconds,
                Error: null));
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "DB health check failed after {ElapsedMs} ms", sw.Elapsed.TotalMilliseconds);
            return Task.FromResult(new DbHealthCheckDto(
                Connected: false,
                DurationMs: sw.Elapsed.TotalMilliseconds,
                Error: ex.GetType().Name));
        }
    }
}
