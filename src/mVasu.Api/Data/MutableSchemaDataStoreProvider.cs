using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;

namespace mVasu.Api.Data;

/// <summary>
/// IXpoDataStoreProvider that honours an explicit AutoCreateOption — required
/// because the stock ConnectionStringDataStoreProvider only supports
/// SchemaAlreadyExists and we need DatabaseAndSchema so MVasuUserSettings is
/// created on first connect.
/// </summary>
public sealed class MutableSchemaDataStoreProvider(
    string connectionString,
    AutoCreateOption autoCreateOption) : IXpoDataStoreProvider
{
    public string ConnectionString { get; } = connectionString;

    public IDataStore CreateWorkingStore(out IDisposable[] disposableObjects) =>
        XpoDefault.GetConnectionProvider(ConnectionString, autoCreateOption, out disposableObjects);

    public IDataStore CreateUpdatingStore(bool allowUpdateSchema, out IDisposable[] disposableObjects) =>
        XpoDefault.GetConnectionProvider(
            ConnectionString,
            allowUpdateSchema ? autoCreateOption : AutoCreateOption.SchemaAlreadyExists,
            out disposableObjects);

    public IDataStore CreateSchemaCheckingStore(out IDisposable[] disposableObjects) =>
        XpoDefault.GetConnectionProvider(
            ConnectionString,
            AutoCreateOption.SchemaAlreadyExists,
            out disposableObjects);
}
