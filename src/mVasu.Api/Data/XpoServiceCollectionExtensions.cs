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
                // SchemaAlreadyExists is the right "never touch DDL" choice here.
                // AutoCreateOption.None looks safer but actually validates the
                // schema at first query and throws SchemaCorrectionNeededException
                // when any xVasu table is missing — and we register every xVasu
                // persistent type into AdditionalExportedTypes via assembly scan,
                // so any gap in the dev database (e.g. MaksuLajiRyhma) crashes
                // the resolver. SchemaAlreadyExists skips validation and reads
                // tables on demand, never emitting DDL.
                e.ObjectSpaceProvider = new XPObjectSpaceProvider(
                    new MutableSchemaDataStoreProvider(
                        connectionString,
                        AutoCreateOption.SchemaAlreadyExists),
                    threadSafe: true,
                    useSeparateDataLayers: false);
            };

            try
            {
                application.Setup();
            }
            catch (NullReferenceException)
            {
                // XAF's Setup() builds an ApplicationModel that includes
                // navigation items, views and other UI concepts. A headless
                // Web API host has no UI, so the model-building step throws
                // NRE in ModelNavigationItemsDomainLogic. By the time we hit
                // that step the work we actually need has already happened:
                // modules loaded, AdditionalExportedTypes registered into
                // XafTypesInfo, and the ObjectSpaceProvider wired through
                // CreateCustomObjectSpaceProvider. We swallow the NRE so DI
                // gets a usable application instance — but only if the
                // ObjectSpaceProvider really did get assigned, otherwise the
                // failure is something else and we re-throw.
                if (application.ObjectSpaceProvider is null)
                {
                    throw;
                }
            }

            return application;
        });

        services.AddSingleton<IObjectSpaceProvider>(sp =>
            sp.GetRequiredService<XafApplication>().ObjectSpaceProvider);

        services.AddScoped<IDbHealthCheck, XpoDbHealthCheck>();

        return services;
    }
}
