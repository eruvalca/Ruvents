var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithRepl();
var database = postgres.AddDatabase("ruventsdb", "ruvents");

var web = builder.AddProject<Projects.Ruvents>("ruvents")
    .WithReference(database)
    .WaitFor(database)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

// Pin the managed EF tool to the application's EF Core version rather than the global tool.
#pragma warning disable ASPIREDOTNETTOOL // The agreed Aspire EF migration integration uses the experimental tool resource API.
var migrations = web.AddEFMigrations("ruvents-migrations", "Ruvents.Data.ApplicationDbContext",
        tool => tool.WithToolVersion("10.0.12"))
    .WithReference(database)
    .WaitFor(database)
    // EF constructs the web host at design time but does not serve HTTP. Avoid inherited DCP endpoint tokens.
    .WithEnvironment("ASPNETCORE_URLS", "http://127.0.0.1:0")
    .WithMigrationOutputDirectory("Data/Migrations")
    .WithMigrationNamespace("Ruvents.Migrations");
#pragma warning restore ASPIREDOTNETTOOL

if (builder.ExecutionContext.IsRunMode)
{
    migrations.RunDatabaseUpdateOnStart();
    web.WaitForCompletion(migrations);
    postgres.WithPgAdmin(pgAdmin => pgAdmin.WithExplicitStart());
}

await builder.Build().RunAsync();
