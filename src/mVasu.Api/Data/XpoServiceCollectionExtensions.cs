using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo.DB;
using mVasu.Api.Domain;
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

            // DatabaseAndSchema: additive — creates the MVasuUserSettings table on
            // first connect, leaves existing xVasu tables untouched.
            return new MutableSchemaDataStoreProvider(
                connectionString,
                AutoCreateOption.DatabaseAndSchema);
        });

        services.AddSingleton<IObjectSpaceProvider>(sp =>
        {
            // XAF requires explicit XafTypesInfo registration before
            // IObjectSpace.FindObject<T> can resolve the type. Without this
            // the framework throws "class is not registered within the business
            // model" because we are not running inside a XafApplication.Setup pipeline.
            XafTypesInfo.Instance.RegisterEntity(typeof(xVasuSecuritySystemUser));
            XafTypesInfo.Instance.RegisterEntity(typeof(xVasuSecuritySystemRole));
            XafTypesInfo.Instance.RegisterEntity(typeof(MVasuUserSettings));

            return new XPObjectSpaceProvider(
                sp.GetRequiredService<IXpoDataStoreProvider>(),
                threadSafe: true,
                useSeparateDataLayers: false);
        });

        services.AddScoped<IDbHealthCheck, XpoDbHealthCheck>();

        return services;
    }
}
