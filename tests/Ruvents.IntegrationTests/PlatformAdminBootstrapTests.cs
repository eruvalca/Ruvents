using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ruvents.Data;
using Ruvents.Features.Account.Services;
using Ruvents.IntegrationTests.Support;
using Ruvents.SharedKernel.Security;
using Shouldly;
using Xunit;

namespace Ruvents.IntegrationTests;

[Collection(PostgresAppFixture.CollectionName)]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class PlatformAdminBootstrapTests(PostgresAppFixture fixture)
{
    [Fact]
    public async Task ConfirmsAndPromotesTheConfiguredAdministratorAsync()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.test";
        await using var scope = fixture.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new ApplicationUser { UserName = email, Email = email };
        (await users.CreateAsync(user)).Succeeded.ShouldBeTrue();
        (await users.IsEmailConfirmedAsync(user)).ShouldBeFalse();
        (await users.IsInRoleAsync(user, PlatformRoles.Admin)).ShouldBeFalse();
        var promotion = new AdminPromotionService(users, roles, database, Options.Create(new PlatformAdminOptions { AdminEmail = email }));
        var cancellation = new IdentityCancellation { Token = TestContext.Current.CancellationToken };

        var completed = await PlatformAdminBootstrap.ConfirmAsync(
            users, promotion, cancellation, email, NullLogger.Instance, TestContext.Current.CancellationToken);

        completed.ShouldBeTrue();
        user.EmailConfirmed.ShouldBeTrue();

        // A fresh scope proves the confirmation and role assignment were persisted.
        await using var verification = fixture.CreateScope();
        var persistedUsers = verification.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var persisted = await persistedUsers.FindByIdAsync(user.Id);
        persisted.ShouldNotBeNull();
        (await persistedUsers.IsEmailConfirmedAsync(persisted)).ShouldBeTrue();
        (await persistedUsers.IsInRoleAsync(persisted, PlatformRoles.Admin)).ShouldBeTrue();
    }

    [Fact]
    public async Task RepeatedConfirmationKeepsTheAdministratorPromotedAsync()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.test";
        await using var scope = fixture.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new ApplicationUser { UserName = email, Email = email };
        (await users.CreateAsync(user)).Succeeded.ShouldBeTrue();
        var promotion = new AdminPromotionService(users, roles, database, Options.Create(new PlatformAdminOptions { AdminEmail = email }));
        var cancellation = new IdentityCancellation { Token = TestContext.Current.CancellationToken };

        var first = await PlatformAdminBootstrap.ConfirmAsync(
            users, promotion, cancellation, email, NullLogger.Instance, TestContext.Current.CancellationToken);
        var second = await PlatformAdminBootstrap.ConfirmAsync(
            users, promotion, cancellation, email, NullLogger.Instance, TestContext.Current.CancellationToken);

        first.ShouldBeTrue();
        second.ShouldBeTrue();
        (await users.IsInRoleAsync(user, PlatformRoles.Admin)).ShouldBeTrue();
    }
}
