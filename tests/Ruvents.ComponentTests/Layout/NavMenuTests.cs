using System.Diagnostics.CodeAnalysis;
using Ruvents.UI.Layout;
using Bogus;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Ruvents.ComponentTests.Layout;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class NavMenuTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthenticationStateSelectsGuestLinksOrEncodedIdentityAndLogoutAsync(bool authenticated)
    {
        await using var context = new BunitContext();
        var authorization = context.AddAuthorization();
        var faker = new Faker { Random = new Randomizer(7214) };
        var userName = faker.Internet.UserName() + "<admin>";
        if (authenticated)
        {
            authorization.SetAuthorized(userName);
        }

        var component = context.Render<NavMenu>();

        var links = component.FindAll("nav a");
        links.ShouldNotBeEmpty();
        foreach (var link in links)
        {
            link.Closest("[data-enhance-nav='false']").ShouldBeNull();
            // Native anchors handle their own focus order; no tabindex override.
            link.GetAttribute("tabindex").ShouldBeNull();
        }

        // .NET 10's NavLink takes its target from the routed href attribute and
        // must still report the active page to assistive technology.
        component.Find("a[href='/']").GetAttribute("aria-current").ShouldBe("page");
        component.FindAll("a[aria-current='page']").Count.ShouldBe(1);

        if (authenticated)
        {
            component.Find("a[href='Account/Manage']").TextContent.Trim().ShouldBe(userName);
            component.FindAll("admin, a[href='Account/Login'], a[href='Account/Register']").ShouldBeEmpty();
            component.Find("form[action='Account/Logout']").GetAttribute("method").ShouldBe("post");
        }
        else
        {
            component.Find("a[href='Account/Login']").TextContent.Trim().ShouldBe("Login");
            component.Find("a[href='Account/Register']").TextContent.Trim().ShouldBe("Register");
            component.FindAll("form[action='Account/Logout'], a[href='Account/Manage']").ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task LogoutReturnUrlTracksNavigationAndStopsUpdatingAfterDisposalAsync()
    {
        await using var context = new BunitContext();
        context.AddAuthorization().SetAuthorized("member");
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("events?search=a%26b#upcoming");
        var component = context.Render<NavMenu>();
        component.Find("input[name='ReturnUrl']").GetAttribute("value").ShouldBe("events?search=a%26b#upcoming");

        await component.InvokeAsync(() => navigation.NavigateTo("counter?count=2&mode=short#value"));

        component.Find("input[name='ReturnUrl']").GetAttribute("value").ShouldBe("counter?count=2&mode=short#value");
        await component.InvokeAsync(component.Instance.Dispose);

        await component.InvokeAsync(() => navigation.NavigateTo("auth"));

        component.Find("input[name='ReturnUrl']").GetAttribute("value").ShouldBe("counter?count=2&mode=short#value");
    }
}
