using System.Diagnostics.CodeAnalysis;
using Bogus;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ruvents.Data;
using Shouldly;
using Xunit;
using Profile = Ruvents.Features.Account.Pages.Manage.Index;

namespace Ruvents.ComponentTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class ProfileTests
{
    [Fact]
    public async Task ProfileLoadsCurrentValuesAndSavingUnchangedFieldsAvoidsWriteAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        var user = account.Authenticate();
        var fake = new Faker { Random = new Randomizer(724) };
        var username = fake.Internet.UserName();
        var phone = fake.Phone.PhoneNumber("###-###-####");
        SetNames(user, "Ada", "Grace", "Lovelace", "Ada Grace Lovelace");
        user.PhoneNumber = phone;
        account.Http.Request.Method = HttpMethods.Get;
        account.Users.GetUserNameAsync(user).Returns(username);
        account.Users.GetPhoneNumberAsync(user).Returns(phone);

        var component = account.Render<Profile>(context);

        component.Find("#username").GetAttribute("value").ShouldBe(username);
        component.Find("#username").HasAttribute("disabled").ShouldBeTrue();
        component.Find("input[name='Input.FirstName']").GetAttribute("value").ShouldBe("Ada");
        component.Find("input[name='Input.MiddleName']").GetAttribute("value").ShouldBe("Grace");
        component.Find("input[name='Input.LastName']").GetAttribute("value").ShouldBe("Lovelace");
        component.Find("input[name='Input.DisplayName']").GetAttribute("value").ShouldBe("Ada Grace Lovelace");
        component.Find("input[name='Input.PhoneNumber']").GetAttribute("value").ShouldBe(phone);

        await component.Find("form").SubmitAsync();

        await account.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await account.Users.DidNotReceiveWithAnyArgs().SetPhoneNumberAsync(default!, default);
        await account.SignIn.Received(1).RefreshSignInAsync(user);
        account.StatusCookie.ShouldContain("Your profile has been updated");
    }

    [Theory]
    [InlineData("+1 (312) 555-0123")]
    [InlineData(null)]
    public async Task ChangedOrRemovedPhoneIsSavedBeforeRefreshingSessionAsync(string? phone)
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        var user = account.Authenticate();
        SetNames(user, "Ada", "Grace", "Lovelace", "Ada Grace Lovelace");
        account.Http.Request.Method = HttpMethods.Get;
        user.PhoneNumber = "+1 (312) 555-0199";
        user.PhoneNumberConfirmed = true;
        account.Users.GetPhoneNumberAsync(user).Returns(user.PhoneNumber);
        account.Users.SetPhoneNumberAsync(user, phone).Returns(IdentityResult.Success);
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("Account/Manage?from=profile");
        var component = account.Render<Profile>(context);
        // Supply the nullable posted value because bUnit does not run static SSR's form mapper.
        component.Instance.SetInputValue("PhoneNumber", phone);

        await component.Find("form").SubmitAsync();

        component.FindAll(".error-text[role='alert']").ShouldBeEmpty();
        await account.Users.Received(1).SetPhoneNumberAsync(user, phone);
        await account.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        user.PhoneNumberConfirmed.ShouldBeFalse();
        await account.SignIn.Received(1).RefreshSignInAsync(user);
        Received.InOrder(() =>
        {
            _ = account.Users.SetPhoneNumberAsync(user, phone);
            _ = account.SignIn.RefreshSignInAsync(user);
        });
        account.StatusCookie.ShouldContain("Your profile has been updated");
        navigation.Uri.ShouldBe("http://localhost/Account/Manage");
    }

    [Fact]
    public async Task PhoneAndNameChangesArePersistedInOneWriteAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        var user = account.Authenticate();
        SetNames(user, "Ada", "Grace", "Lovelace", "Ada Grace Lovelace");
        account.Http.Request.Method = HttpMethods.Get;
        user.PhoneNumber = "+1 (312) 555-0199";
        account.Users.GetPhoneNumberAsync(user).Returns(user.PhoneNumber);
        account.Users.SetPhoneNumberAsync(user, "+1 (312) 555-0123").Returns(IdentityResult.Success);
        var component = account.Render<Profile>(context);
        await component.Find("input[name='Input.FirstName']").ChangeAsync(new ChangeEventArgs { Value = "Augusta" });
        await component.Find("input[name='Input.PhoneNumber']").ChangeAsync(new ChangeEventArgs { Value = "+1 (312) 555-0123" });

        await component.Find("form").SubmitAsync();

        await account.Users.Received(1).SetPhoneNumberAsync(user, "+1 (312) 555-0123");
        await account.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        user.FirstName.ShouldBe("Augusta");
        user.DisplayName.ShouldBe("Augusta Grace Lovelace");
        await account.SignIn.Received(1).RefreshSignInAsync(user);
    }

    [Fact]
    public async Task ChangingNamesUpdatesTheDefaultDisplayNameAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        var user = account.Authenticate();
        SetNames(user, "Ada", "Grace", "Lovelace", "Ada Grace Lovelace");
        account.Http.Request.Method = HttpMethods.Get;
        var component = account.Render<Profile>(context);
        await component.Find("input[name='Input.LastName']").ChangeAsync(new ChangeEventArgs { Value = "Byron" });

        await component.Find("form").SubmitAsync();

        await account.Users.Received(1).UpdateAsync(user);
        user.LastName.ShouldBe("Byron");
        user.DisplayName.ShouldBe("Ada Grace Byron");
    }

    [Fact]
    public async Task TruncatedDefaultDisplayNameStillFollowsNameEditsAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        var user = account.Authenticate();
        var firstName = new string('a', 40);
        var middleName = new string('b', 40);
        var lastName = new string('c', 40);
        var storedDefault = UserProfileNames.ResolveDisplayName(null, firstName, middleName, lastName, string.Empty);
        storedDefault.Length.ShouldBe(UserProfileNames.MaxDisplayNameLength);
        SetNames(user, firstName, middleName, lastName, storedDefault);
        account.Http.Request.Method = HttpMethods.Get;
        var component = account.Render<Profile>(context);
        var newLastName = new string('d', 40);

        await component.Find("input[name='Input.LastName']").ChangeAsync(new ChangeEventArgs { Value = newLastName });
        await component.Find("form").SubmitAsync();

        var expected = UserProfileNames.ResolveDisplayName(null, firstName, middleName, newLastName, string.Empty);
        await account.Users.Received(1).UpdateAsync(user);
        user.DisplayName.ShouldBe(expected);
    }

    [Fact]
    public async Task CustomDisplayNameIsPreservedWhenNamesChangeAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        var user = account.Authenticate();
        SetNames(user, "Ada", "Grace", "Lovelace", "Ada");
        account.Http.Request.Method = HttpMethods.Get;
        var component = account.Render<Profile>(context);
        await component.Find("input[name='Input.LastName']").ChangeAsync(new ChangeEventArgs { Value = "Byron" });

        await component.Find("form").SubmitAsync();

        await account.Users.Received(1).UpdateAsync(user);
        user.LastName.ShouldBe("Byron");
        user.DisplayName.ShouldBe("Ada");
    }

    [Fact]
    public async Task LegacyEmailDisplayNameFollowsNamesOnceTheyAreAddedAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        var user = account.Authenticate();
        user.DisplayName = "member";
        account.Http.Request.Method = HttpMethods.Get;
        account.Users.GetUserNameAsync(user).Returns("member@example.test");
        var component = account.Render<Profile>(context);
        component.Find("input[name='Input.DisplayName']").GetAttribute("value").ShouldBe("member");

        await component.Find("input[name='Input.FirstName']").ChangeAsync(new ChangeEventArgs { Value = "Ada" });
        await component.Find("input[name='Input.LastName']").ChangeAsync(new ChangeEventArgs { Value = "Lovelace" });

        await component.Find("form").SubmitAsync();

        await account.Users.Received(1).UpdateAsync(user);
        user.DisplayName.ShouldBe("Ada Lovelace");
    }

    [Fact]
    public async Task UpdateFailureReportsFailureAndDoesNotRefreshSessionAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        var user = account.Authenticate();
        SetNames(user, "Ada", "Grace", "Lovelace", "Ada Grace Lovelace");
        account.Http.Request.Method = HttpMethods.Get;
        account.Users.SetPhoneNumberAsync(user, "312-555-0123")
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Profile save failed" }));

        var component = account.Render<Profile>(context);
        await component.Find("input[name='Input.PhoneNumber']").ChangeAsync(new ChangeEventArgs { Value = "312-555-0123" });

        await component.Find("form").SubmitAsync();

        await account.Users.Received(1).SetPhoneNumberAsync(user, "312-555-0123");
        await account.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await account.SignIn.DidNotReceiveWithAnyArgs().RefreshSignInAsync(default!);
        account.StatusCookie.ShouldContain("Profile save failed");
        account.StatusCookie.ShouldNotContain("Your profile has been updated");
    }

    [Fact]
    public async Task InvalidPhoneNumberDoesNotWriteOrRefreshSessionAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        var user = account.Authenticate();
        SetNames(user, "Ada", "Grace", "Lovelace", "Ada Grace Lovelace");
        account.Http.Request.Method = HttpMethods.Get;
        var component = account.Render<Profile>(context);
        await component.Find("input[name='Input.PhoneNumber']").ChangeAsync(new ChangeEventArgs { Value = "not a phone number" });

        await component.Find("form").SubmitAsync();

        component.Find(".error-text[role='alert']").TextContent.ShouldContain("not a valid phone number");
        await account.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await account.Users.DidNotReceiveWithAnyArgs().SetPhoneNumberAsync(default!, default);
        await account.SignIn.DidNotReceiveWithAnyArgs().RefreshSignInAsync(default!);
        account.StatusCookie.ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingRequiredNameDoesNotWriteOrRefreshSessionAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        var user = account.Authenticate();
        SetNames(user, "Ada", "Grace", "Lovelace", "Ada Grace Lovelace");
        account.Http.Request.Method = HttpMethods.Get;
        var component = account.Render<Profile>(context);
        await component.Find("input[name='Input.LastName']").ChangeAsync(new ChangeEventArgs { Value = "" });

        await component.Find("form").SubmitAsync();

        component.Find(".error-text[role='alert']").TextContent.ShouldContain("Last name");
        await account.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await account.Users.DidNotReceiveWithAnyArgs().SetPhoneNumberAsync(default!, default);
        await account.SignIn.DidNotReceiveWithAnyArgs().RefreshSignInAsync(default!);
    }

    [Fact]
    public async Task MissingUserIsRejectedOnInitializationAndSubmissionWithoutReadingOrWritingProfileAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureAccount();
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        var component = account.Render<Profile>(context);
        navigation.Uri.ShouldBe("http://localhost/Account/InvalidUser");
        navigation.NavigateTo("Account/Manage");
        // The required-name validation runs before the missing-user check, so supply
        // valid posted values to exercise the handler.
        component.Instance.SetInputValue("FirstName", "Ada");
        component.Instance.SetInputValue("MiddleName", "Grace");
        component.Instance.SetInputValue("LastName", "Lovelace");

        await component.Find("form").SubmitAsync();

        navigation.Uri.ShouldBe("http://localhost/Account/InvalidUser");
        account.StatusCookie.ShouldContain("Unable to load user");
        await account.Users.DidNotReceiveWithAnyArgs().GetUserNameAsync(default!);
        await account.Users.DidNotReceiveWithAnyArgs().GetPhoneNumberAsync(default!);
        await account.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await account.Users.DidNotReceiveWithAnyArgs().SetPhoneNumberAsync(default!, default);
        await account.SignIn.DidNotReceiveWithAnyArgs().RefreshSignInAsync(default!);
    }

    private static void SetNames(
        ApplicationUser user,
        string firstName,
        string middleName,
        string lastName,
        string displayName)
    {
        user.FirstName = firstName;
        user.MiddleName = middleName;
        user.LastName = lastName;
        user.DisplayName = displayName;
    }
}
