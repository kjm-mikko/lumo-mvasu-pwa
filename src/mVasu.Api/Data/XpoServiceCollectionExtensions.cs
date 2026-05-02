using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo.DB;
using mVasu.Api.Domain;
using xVasu.Module;

namespace mVasu.Api.Data;

public static class XpoServiceCollectionExtensions
{
    public static IServiceCollection AddVasuXpo(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("VasuDb")
            ?? throw new InvalidOperationException(
                "Connection string 'VasuDb' is not configured. " +
                "Set it via: dotnet user-secrets set ConnectionStrings:VasuDb \"<dev connection string>\".");

        // Build the XafApplication host once during DI setup. Setup() walks all
        // modules, collects their AdditionalExportedTypes, and populates
        // XafTypesInfo + the XPDictionary so CompositeObjectSpace.FindObject
        // can resolve any persistent type the resolver or future endpoints touch.
        // SchemaUpdateMode.None ensures the runtime never emits DDL — all schema
        // changes go through db/scripts/ run manually.
        services.AddSingleton<XafApplication>(_ =>
        {
            var application = new MVasuApiApplication
            {
                ConnectionString = connectionString,
            };

            application.Modules.Add(new xVasuModule());
            application.Modules.Add(new MVasuApiModule());

            application.CreateCustomObjectSpaceProvider += (_, e) =>
            {
                e.ObjectSpaceProvider = new XPObjectSpaceProvider(
                    new MutableSchemaDataStoreProvider(
                        connectionString,
                        AutoCreateOption.SchemaAlreadyExists),
                    threadSafe: true,
                    useSeparateDataLayers: false);
            };

            application.Setup();
            return application;
        });

        services.AddSingleton<IObjectSpaceProvider>(sp =>
            sp.GetRequiredService<XafApplication>().ObjectSpaceProvider);

        services.AddScoped<IDbHealthCheck, XpoDbHealthCheck>();

        return services;
    }
}
