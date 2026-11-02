using System.Text.Encodings.Web;
using Bunit;
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

internal static class BunitAccountExtensions
{
    extension(BunitContext context)
    {
        internal AccountTestContext ConfigureAccount()
        {
            var http = new DefaultHttpContext();
            var emails = Substitute.For<IEmailSender<ApplicationUser>>();
            http.Request.Method = HttpMethods.Post;
            var store = Substitute.For<IUserEmailStore<ApplicationUser>>();
            var options = Options.Create(new IdentityOptions());
            var users = Substitute.For<UserManager<ApplicationUser>>(
                store, options, new PasswordHasher<ApplicationUser>(),
                Array.Empty<IUserValidator<ApplicationUser>>(), Array.Empty<IPasswordValidator<ApplicationUser>>(),
                new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
                Substitute.For<IServiceProvider>(), NullLogger<UserManager<ApplicationUser>>.Instance);
            var signIn = Substitute.For<SignInManager<ApplicationUser>>(
                users, new HttpContextAccessor { HttpContext = http },
                Substitute.For<IUserClaimsPrincipalFactory<ApplicationUser>>(), options,
                NullLogger<SignInManager<ApplicationUser>>.Instance,
                Substitute.For<IAuthenticationSchemeProvider>(), Substitute.For<IUserConfirmation<ApplicationUser>>());
            signIn.GetExternalAuthenticationSchemesAsync().Returns(Array.Empty<AuthenticationScheme>());
            context.Services.AddLogging();
            context.Services.AddSingleton(users);
            context.Services.AddSingleton(signIn);
            context.Services.AddSingleton<IUserStore<ApplicationUser>>(store);
            context.Services.AddSingleton(emails);
            context.Services.AddScoped<IdentityCancellation>();
            context.Services.AddScoped(_ => Substitute.For<IAdminPromotionService>());
            var photos = Substitute.For<IProfilePhotoStore>();
            photos.TryValidate(Arg.Any<string>(), Arg.Any<long>(), out Arg.Any<string>()).Returns(true);
            photos.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("photo.png");
            context.Services.AddSingleton(photos);
            var uploads = Substitute.For<IProfilePhotoUploadReader>();
            var file = Substitute.For<IFormFile>();
            file.ContentType.Returns("image/png");
            file.Length.Returns(1024);
            file.OpenReadStream().Returns(_ => new MemoryStream([1, 2, 3]));
            uploads.Read(Arg.Any<HttpContext>()).Returns(file);
            context.Services.AddSingleton(uploads);
            context.Services.AddScoped<AccountSignInService>();
            context.Services.AddScoped<AccountPasskeyService>();
            context.Services.AddScoped<AccountRegistrationService>();
            context.Services.AddScoped<AccountEmailChangeService>();
            context.Services.AddScoped<AccountTwoFactorService>();
            context.Services.AddScoped<ProfilePhotoWriter>();
            context.Services.AddSingleton(UrlEncoder.Default);
            context.Services.AddScoped<IdentityRedirectManager>();
            http.RequestServices = context.Services;
            return new(http, users, signIn, emails, photos, uploads);
        }

        /// <summary>
        /// Configures the account services and declares an interactive renderer, which
        /// components such as <c>PhotoCropField</c> read through RendererInfo. bUnit
        /// throws if a component reads RendererInfo without it being set.
        /// </summary>
        internal AccountTestContext ConfigureInteractiveAccount()
        {
            var account = context.ConfigureAccount();
            context.Renderer.SetRendererInfo(new RendererInfo("Server", isInteractive: true));
            return account;
        }

        internal ILogger<TComponent> CaptureLogs<TComponent>()
        {
            var logger = Substitute.For<ILogger<TComponent>>();
            logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
            context.Services.AddSingleton(logger);
            return logger;
        }
    }
}
