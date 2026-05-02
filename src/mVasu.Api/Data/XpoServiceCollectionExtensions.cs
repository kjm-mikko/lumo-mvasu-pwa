using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using xVasu.Data.Security;

namespace mVasu.Api.Data;

public static class XpoServiceCollectionExtensions
{
    public static IServiceCollection AddVasuXpo(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IXpoDataStoreProvider>(_ =>
        {
            var connectionString = configuration.GetConnectionString("VasuDb")
                ?? throw new InvalidOperationException(
                    "Connection string 'VasuDb' is not configured. " +
                    "Set it via: dotnet user-secrets set ConnectionStrings:VasuDb \"<dev connection string>\".");

            return new ConnectionStringDataStoreProvider(connectionString);
        });

        services.AddSingleton<IObjectSpaceProvider>(sp =>
        {
            _ = typeof(xVasuSecuritySystemUser);

            return new XPObjectSpaceProvider(
                sp.GetRequiredService<IXpoDataStoreProvider>(),
                threadSafe: true,
                useSeparateDataLayers: false);
        });

        services.AddScoped<IDbHealthCheck, XpoDbHealthCheck>();

        return services;
    }
}
