using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Ruvents.Data;
using Ruvents.Features.Account.Endpoints;
using Ruvents.Features.Account.Services;
using Ruvents.SharedKernel.Features.Account;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ruvents.IntegrationTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class AccountQueryCancellationTests
{
    [Fact]
    public async Task HttpAbortCancelsAccountQueryWhilePostgresIsBlockedAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        await using var postgres = new PostgreSqlBuilder("postgres:18.3").Build();
        await postgres.StartAsync(timeout.Token);
        var probe = new QueryProbe();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthorization();
        builder.Services.Configure<IdentityOptions>(identity => identity.Stores.SchemaVersion = IdentitySchemaVersions.Version3);
        builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
        builder.Services.AddDbContextFactory<ApplicationDbContext>(db => db.UseNpgsql(postgres.GetConnectionString()).AddInterceptors(probe));
        builder.Services.AddScoped<AccountQueries>();
        await using var app = builder.Build();
        var factory = app.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var database = await factory.CreateDbContextAsync(timeout.Token);
        await database.Database.MigrateAsync(timeout.Token);
        database.Users.Add(new ApplicationUser { Id = "member", UserName = "member@example.test", EmailConfirmed = true });
        database.Users.Add(new ApplicationUser { Id = "other", UserName = "private@example.test" });
        await database.SaveChangesAsync(timeout.Token);
        app.Use((context, next) =>
        {
            // This isolated test host supplies a principal; production uses the Identity cookie middleware.
            context.User = Principal();
            return next(context);
        });
        app.UseAuthorization();
        app.MapGet("/api/account/summary", (ClaimsPrincipal user, AccountQueries queries, CancellationToken cancellationToken) =>
            AccountSummaryEndpoint.HandleAsync(user, queries, cancellationToken)).RequireAuthorization();
        await app.StartAsync(timeout.Token);
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            using (var response = await client.GetAsync(new Uri("api/account/summary", UriKind.Relative), timeout.Token))
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                (await response.Content.ReadFromJsonAsync<AccountSummary>(timeout.Token))
                    .ShouldBe(new AccountSummary("member@example.test", true));
            }

            // Hold a real database lock so the next production query cannot finish before the abort.
            await using var transaction = await database.Database.BeginTransactionAsync(timeout.Token);
            await database.Database.ExecuteSqlRawAsync("LOCK TABLE \"AspNetUsers\" IN ACCESS EXCLUSIVE MODE", timeout.Token);
            probe.ObserveCancellation = true;
            using var request = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            var pending = client.GetAsync(new Uri("api/account/summary", UriKind.Relative), request.Token);
            await probe.Started.Task.WaitAsync(timeout.Token);

            await request.CancelAsync();

            await Should.ThrowAsync<OperationCanceledException>(() => pending);
            await probe.Canceled.Task.WaitAsync(timeout.Token);
            probe.QueryToken.IsCancellationRequested.ShouldBeTrue();
            // Release only after Npgsql/EF reported cancellation, proving the lock did not end the query.
            await transaction.RollbackAsync(timeout.Token);

            await VerifyDirectQueryAsync(app.Services, timeout.Token);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task VerifyDirectQueryAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var state = (ServerAuthenticationStateProvider)scope.ServiceProvider.GetRequiredService<AuthenticationStateProvider>();
        state.SetAuthenticationState(Task.FromResult(new AuthenticationState(Principal())));
        var queries = scope.ServiceProvider.GetRequiredService<AccountQueries>();
        (await queries.GetCurrentAsync(cancellationToken)).ShouldBe(new AccountSummary("member@example.test", true));

        state.SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())));
        await Should.ThrowAsync<UnauthorizedAccessException>(() => queries.GetCurrentAsync(cancellationToken));
        state.SetAuthenticationState(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "missing")], "test")))));
        (await queries.GetCurrentAsync(cancellationToken)).ShouldBeNull();
    }

    private static ClaimsPrincipal Principal() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "member")], "test"));

    private sealed class QueryProbe : DbCommandInterceptor
    {
        public bool ObserveCancellation { get; set; }
        public CancellationToken QueryToken { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (ObserveCancellation)
            {
                QueryToken = cancellationToken;
                Started.TrySetResult();
            }
            return new(result);
        }

        public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData eventData, CancellationToken cancellationToken = default)
        {
            Canceled.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
