using Aspire.Hosting.ApplicationModel;
using Ruvents.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.ExecutionContext.IsRunMode
    ? builder.AddPostgres("postgres").WithDataVolume().WithRepl()
    : null;
IResourceBuilder<IResourceWithConnectionString> database = postgres is not null
    ? postgres.AddDatabase("ruventsdb", "ruvents")
    : AzureDeployment.AddDatabase(builder);

// Profile photos and event covers live in Blob Storage. Local runs use the Azurite
// emulator; deployed environments provision a storage account.
var storage = builder.ExecutionContext.IsRunMode
    ? builder.AddAzureStorage("storage").RunAsEmulator()
    : builder.AddAzureStorage("storage");
var blobs = storage.AddBlobs("blobs");

var web = builder.AddProject<Projects.Ruvents>("ruvents-web")
    .WithReference(database)
    .WaitFor(database)
    .WithReference(blobs)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

// Pin the managed EF tool to the application's EF Core version rather than the global tool.
#pragma warning disable ASPIREDOTNETTOOL // The agreed Aspire EF migration integration uses the experimental tool resource API.
var migrations = web.AddEFMigrations("ruvents-migrations", "Ruvents.Data.ApplicationDbContext",
        tool => tool.WithToolVersion("10.0.14"))
    .WithReference(database)
    .WaitFor(database)
    // EF constructs the web host at design time but does not serve HTTP. Avoid inherited DCP endpoint tokens.
    .WithEnvironment("ASPNETCORE_URLS", "http://127.0.0.1:0")
    // Bundle generation runs before Azure resolves the connection string, so the tool
    // may build the model with a provider-only placeholder. Runtime never sets this.
    .WithEnvironment("Ruvents__DesignTime", "true")
    .WithMigrationOutputDirectory("Data/Migrations")
    .WithMigrationNamespace("Ruvents.Migrations");
#pragma warning restore ASPIREDOTNETTOOL

if (postgres is not null)
{
    migrations.RunDatabaseUpdateOnStart();
    web.WaitForCompletion(migrations);
    postgres.WithPgAdmin(pgAdmin => pgAdmin.WithExplicitStart());
}
else
{
    AzureDeployment.Configure(builder, web, migrations);
}

// The host owns Ctrl+C/SIGTERM shutdown; there is no outer operation to cancel it.
await builder.Build().RunAsync(CancellationToken.None);
