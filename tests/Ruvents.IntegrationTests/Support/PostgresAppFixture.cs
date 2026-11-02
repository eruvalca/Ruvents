using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ruvents.Data;
using Ruvents.Features.Administration.Services;
using Ruvents.Features.Events.Contracts;
using Ruvents.Features.Events.Services;
using Ruvents.Features.Families.Contracts;
using Ruvents.Features.Families.Services;
using Ruvents.Features.Notifications.Contracts;
using Ruvents.Features.Notifications.Services;
using Ruvents.Features.Potluck.Contracts;
using Ruvents.Features.Potluck.Services;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ruvents.IntegrationTests.Support;

/// <summary>
/// One migrated PostgreSQL database shared by the scenario tests. Each test creates
/// its own users and groups so tests stay independent without paying container cost.
/// </summary>
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public collection fixtures for discovery.")]
public sealed class PostgresAppFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;

    internal ServiceProvider Services { get; private set; } = null!;

    internal TestClock Clock { get; } = new();

    public async ValueTask InitializeAsync()
    {
        // Match Aspire.Hosting.PostgreSQL 13.6.4's default image; review together on upgrades.
        _postgres = new PostgreSqlBuilder("postgres:18.3").Build();
        await _postgres.StartAsync(TestContext.Current.CancellationToken);

        var services = new ServiceCollection();
        services.AddLogging();
        var connectionString = _postgres.GetConnectionString();
        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));
        services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());
        services.AddIdentityCore<ApplicationUser>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped<IFamilyAccess, FamilyAccess>();
        services.AddScoped<IFamilyGroups, FamilyService>();
        services.AddScoped<IJoinRequests, JoinRequestService>();
        services.AddScoped<IHouseholdDirectory, HouseholdService>();
        services.AddScoped<IEventAccess, EventAccessService>();
        services.AddScoped<EventService>();
        services.AddScoped<IRsvpDirectory, RsvpService>();
        services.AddScoped<IEventPostDirectory, EventPostService>();
        services.AddScoped<IPotluckBoard, PotluckService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<INotificationPublisher>(provider => provider.GetRequiredService<NotificationService>());
        services.AddScoped<INotificationQueries>(provider => provider.GetRequiredService<NotificationService>());
        services.AddScoped<AdministrationService>();
        services.AddScoped<Features.Account.Services.AccountDeletionService>();
        Services = services.BuildServiceProvider();

        await using var scope = Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await database.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            // Initialization can fail before the provider is built, so tolerate a
            // partially initialized fixture instead of throwing over the real error.
            if (Services is not null)
            {
                await Services.DisposeAsync();
            }
        }
        finally
        {
            // The container must be released even when provider disposal fails.
            if (_postgres is not null)
            {
                await _postgres.DisposeAsync();
            }
        }
    }

    internal AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    internal const string CollectionName = "postgres-app";
}

