using System.Diagnostics.CodeAnalysis;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ruvents.Data;
using Ruvents.Features.Account.Pages;
using Shouldly;
using Xunit;

namespace Ruvents.ComponentTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class RegistrationTests
{
    [Fact]
    public async Task RegistrationHasASingleSubmitButtonOwnedByThePageAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureInteractiveAccount();

        var component = account.Render<Register>(context);

        var submit = component.FindAll("button[type='submit']");
        submit.Count.ShouldBe(1);
        submit[0].TextContent.ShouldBe("Create account");
    }

    [Fact]
    public async Task RejectedPasswordRegistrationDisplaysIdentityErrorAndSkipsContinuationAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureInteractiveAccount();
        account.Users.SupportsUserEmail.Returns(true);
        account.Users.CreateAsync(Arg.Any<ApplicationUser>(), "password").Returns(IdentityResult.Failed(new IdentityError { Description = "Email already registered" }));
        var component = account.Render<Register>(context);
        await FillRequiredFieldsAsync(component);
        await component.Find("input[name='Input.Email']").ChangeAsync(new ChangeEventArgs { Value = "member@example.test" });
        await component.Find("input[name='Input.Password']").ChangeAsync(new ChangeEventArgs { Value = "password" });
        await component.Find("input[name='Input.ConfirmPassword']").ChangeAsync(new ChangeEventArgs { Value = "password" });

        await component.Find("form").SubmitAsync();

        await component.WaitForAssertionAsync(() => component.Find(".notice[data-kind='error']").TextContent.ShouldContain("Email already registered"));
        await account.Photos.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default!, Xunit.TestContext.Current.CancellationToken);
        await account.SignIn.DidNotReceiveWithAnyArgs().SignInAsync(default!, default(bool), default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulPasswordRegistrationStoresNamesPhotoAndSignsInAsync(bool requireConfirmation)
    {
        await using var context = new BunitContext();
        var account = context.ConfigureInteractiveAccount();
        account.Users.SupportsUserEmail.Returns(true);
        account.Users.Options.SignIn.RequireConfirmedAccount = requireConfirmation;
        ApplicationUser? created = null;
        account.Users.CreateAsync(Arg.Any<ApplicationUser>(), "password").Returns(call =>
        {
            created = call.Arg<ApplicationUser>();
            return IdentityResult.Success;
        });
        account.Users.GetUserIdAsync(Arg.Any<ApplicationUser>()).Returns("member");
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("Account/Register?returnUrl=%2Fevents");
        var component = account.Render<Register>(context);
        await FillRequiredFieldsAsync(component);
        await component.Find("input[name='Input.Email']").ChangeAsync(new ChangeEventArgs { Value = "member@example.test" });
        await component.Find("input[name='Input.Password']").ChangeAsync(new ChangeEventArgs { Value = "password" });
        await component.Find("input[name='Input.ConfirmPassword']").ChangeAsync(new ChangeEventArgs { Value = "password" });

        await component.Find("form").SubmitAsync();

        created.ShouldNotBeNull();
        created.FirstName.ShouldBe("Ada");
        created.MiddleName.ShouldBe("Grace");
        created.LastName.ShouldBe("Lovelace");
        created.DisplayName.ShouldBe("Ada Grace Lovelace");
        await account.Photos.Received(1).SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), "image/png", Arg.Any<CancellationToken>());
        navigation.Uri.ShouldBe("http://localhost/events");
        await account.SignIn.Received(1).SignInAsync(Arg.Any<ApplicationUser>(), false, null);
    }

    [Fact]
    public async Task CustomDisplayNameOverridesTheFullNameDefaultAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureInteractiveAccount();
        account.Users.SupportsUserEmail.Returns(true);
        ApplicationUser? created = null;
        account.Users.CreateAsync(Arg.Any<ApplicationUser>(), "password").Returns(call =>
        {
            created = call.Arg<ApplicationUser>();
            return IdentityResult.Success;
        });
        account.Users.GetUserIdAsync(Arg.Any<ApplicationUser>()).Returns("member");
        var component = account.Render<Register>(context);
        await FillRequiredFieldsAsync(component);
        await component.Find("input[name='Input.DisplayName']").ChangeAsync(new ChangeEventArgs { Value = "Ada" });
        await component.Find("input[name='Input.Email']").ChangeAsync(new ChangeEventArgs { Value = "member@example.test" });
        await component.Find("input[name='Input.Password']").ChangeAsync(new ChangeEventArgs { Value = "password" });
        await component.Find("input[name='Input.ConfirmPassword']").ChangeAsync(new ChangeEventArgs { Value = "password" });

        await component.Find("form").SubmitAsync();

        created.ShouldNotBeNull();
        created.DisplayName.ShouldBe("Ada");
    }

    private static async Task FillRequiredFieldsAsync(IRenderedComponent<Register> component)
    {
        await component.Find("input[name='Input.FirstName']").ChangeAsync(new ChangeEventArgs { Value = "Ada" });
        await component.Find("input[name='Input.MiddleName']").ChangeAsync(new ChangeEventArgs { Value = "Grace" });
        await component.Find("input[name='Input.LastName']").ChangeAsync(new ChangeEventArgs { Value = "Lovelace" });
    }
}
