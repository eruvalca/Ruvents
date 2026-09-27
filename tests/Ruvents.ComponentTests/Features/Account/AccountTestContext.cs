using Bunit;
using System.Reflection;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Ruvents.Data;
using Ruvents.Features.Account.Services;

namespace Ruvents.ComponentTests.Features.Account;

internal sealed class AccountTestContext
{
    public DefaultHttpContext Http { get; } = new();
    public UserManager<ApplicationUser> Users { get; private set; } = default!;
    public SignInManager<ApplicationUser> SignIn { get; private set; } = default!;
    public IEmailSender<ApplicationUser> Emails { get; } = Substitute.For<IEmailSender<ApplicationUser>>();

    public static AccountTestContext Configure(BunitContext context)
    {
        var account = new AccountTestContext();
        account.Http.Request.Method = HttpMethods.Post;
        var store = Substitute.For<IUserEmailStore<ApplicationUser>>();
        var options = Options.Create(new IdentityOptions());
        account.Users = Substitute.For<UserManager<ApplicationUser>>(
            store, options, new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(), Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(), NullLogger<UserManager<ApplicationUser>>.Instance);
        account.SignIn = Substitute.For<SignInManager<ApplicationUser>>(
            account.Users, new HttpContextAccessor { HttpContext = account.Http },
            Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(), options,
            NullLogger<SignInManager<ApplicationUser>>.Instance,
            Substitute.For<IAuthenticationSchemeProvider>(), Substitute.For<IUserConfirmation<ApplicationUser>>());
        account.SignIn.GetExternalAuthenticationSchemesAsync().Returns(Array.Empty<AuthenticationScheme>());
        context.Services.AddLogging();
        context.Services.AddSingleton(account.Users);
        context.Services.AddSingleton(account.SignIn);
        context.Services.AddSingleton<IUserStore<ApplicationUser>>(store);
        context.Services.AddSingleton(account.Emails);
        context.Services.AddScoped<AccountSignInService>();
        context.Services.AddScoped<AccountPasskeyService>();
        context.Services.AddScoped<AccountRegistrationService>();
        context.Services.AddScoped<AccountEmailChangeService>();
        context.Services.AddScoped<AccountTwoFactorService>();
        context.Services.AddSingleton(UrlEncoder.Default);
        context.Services.AddScoped<IdentityRedirectManager>();
        account.Http.RequestServices = context.Services;
        return account;
    }

    public IRenderedComponent<TComponent> Render<TComponent>(BunitContext context)
        where TComponent : IComponent => context.Render<TComponent>(parameters => parameters.AddCascadingValue<HttpContext>(Http));

    public ApplicationUser Authenticate()
    {
        var user = new ApplicationUser { Email = "member@example.test" };
        Users.GetUserAsync(Http.User).Returns(user);
        Users.GetUserIdAsync(user).Returns(user.Id);
        return user;
    }

    public string StatusCookie => Uri.UnescapeDataString(Http.Response.Headers.SetCookie.ToString());

    public static ILogger<TComponent> CaptureLogs<TComponent>(BunitContext context)
    {
        var logger = Substitute.For<ILogger<TComponent>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        context.Services.AddSingleton(logger);
        return logger;
    }

    public static IEnumerable<int> LoggedEventIds(ILogger logger) => logger.ReceivedCalls()
        .Where(call => string.Equals(call.GetMethodInfo().Name, "Log", StringComparison.Ordinal))
        .Select(call => ((EventId)call.GetArguments()[1]!).Id);

    // bUnit dispatches form events but does not execute static SSR's form-value mapper.
    public static void SetFormValue<TComponent>(TComponent page, string name, object? value)
        where TComponent : IComponent => typeof(TComponent).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, value);

    public static void SetInputValue<TComponent>(TComponent page, string name, object? value)
        where TComponent : IComponent
    {
        var input = typeof(TComponent).GetProperty("Input", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
        input.GetType().GetProperty(name)!.SetValue(input, value);
    }
}
