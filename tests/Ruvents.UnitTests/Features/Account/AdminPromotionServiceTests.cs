using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Ruvents.Data;
using Ruvents.Features.Account.Services;
using Ruvents.SharedKernel.Security;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class AdminPromotionServiceTests
{
    private const string AdminEmail = "admin@example.test";

    [Fact]
    public async Task ConcurrentRoleCreationIsTreatedAsSuccessAsync()
    {
        using var identity = IdentityTestContext.Create();
        var user = new ApplicationUser { Email = AdminEmail, EmailConfirmed = true };
        ConfigurePromotableUser(identity, user);
        using var roles = CreateRoleManager();
        await using var database = CreateDatabase();
        roles.RoleExistsAsync(PlatformRoles.Admin).Returns(false, true);
        roles.CreateAsync(Arg.Any<IdentityRole>())
            .Returns(Task.FromException<IdentityResult>(new DbUpdateException("duplicate role")));
        var service = new AdminPromotionService(
            identity.Users, roles, database, Options.Create(new PlatformAdminOptions { AdminEmail = AdminEmail }));

        var promoted = await service.PromoteIfConfiguredAsync(user, TestContext.Current.CancellationToken);

        promoted.ShouldBeTrue();
        await identity.Users.Received(1).AddToRoleAsync(user, PlatformRoles.Admin);
    }

    [Fact]
    public async Task RoleCreationFailureWithoutAnExistingRoleRethrowsAsync()
    {
        using var identity = IdentityTestContext.Create();
        var user = new ApplicationUser { Email = AdminEmail, EmailConfirmed = true };
        ConfigurePromotableUser(identity, user);
        using var roles = CreateRoleManager();
        await using var database = CreateDatabase();
        roles.RoleExistsAsync(PlatformRoles.Admin).Returns(false);
        roles.CreateAsync(Arg.Any<IdentityRole>())
            .Returns(Task.FromException<IdentityResult>(new DbUpdateException("duplicate role")));
        var service = new AdminPromotionService(
            identity.Users, roles, database, Options.Create(new PlatformAdminOptions { AdminEmail = AdminEmail }));

        await Should.ThrowAsync<DbUpdateException>(
            () => service.PromoteIfConfiguredAsync(user, TestContext.Current.CancellationToken));

        await identity.Users.DidNotReceiveWithAnyArgs().AddToRoleAsync(default!, default!);
    }

    private static void ConfigurePromotableUser(IdentityTestContext identity, ApplicationUser user)
    {
        identity.Users.GetEmailAsync(user).Returns(AdminEmail);
        identity.Users.IsEmailConfirmedAsync(user).Returns(true);
        identity.Users.IsInRoleAsync(user, PlatformRoles.Admin).Returns(false);
        identity.Users.AddToRoleAsync(user, PlatformRoles.Admin).Returns(IdentityResult.Success);
    }

    private static RoleManager<IdentityRole> CreateRoleManager() =>
        Substitute.For<RoleManager<IdentityRole>>(
            Substitute.For<IRoleStore<IdentityRole>>(),
            Array.Empty<IRoleValidator<IdentityRole>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);

    private static ApplicationDbContext CreateDatabase() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
}
