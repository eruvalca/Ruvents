using Cropper.Blazor.Extensions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Ruvents.Components;
using Ruvents.Data;
using Ruvents.Features.Account.Endpoints;
using Ruvents.Features.Account.Services;
using Ruvents.Features.Administration.Contracts;
using Ruvents.Features.Administration.Services;
using Ruvents.Features.Events.Contracts;
using Ruvents.Features.Events.Endpoints;
using Ruvents.Features.Events.Services;
using Ruvents.Features.Families.Contracts;
using Ruvents.Features.Families.Services;
using Ruvents.Features.Notifications.Contracts;
using Ruvents.Features.Notifications.Services;
using Ruvents.Features.PlanningApi;
using Ruvents.Features.Potluck.Contracts;
using Ruvents.Features.Potluck.Services;
using Ruvents.ServiceDefaults;
using Ruvents.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Application code reads the clock from DI so RS0030 can ban direct system-clock access.
builder.Services.AddSingleton(TimeProvider.System);
// Standard HTTP contracts: problem details for errors and a document for the API surface.
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

// Profile photos and event covers are stored in Blob Storage (Azurite locally). The
// client is only registered when an endpoint is configured so EF design-time hosts,
// which reference only the database, can build the host.
var blobsConfigured = !string.IsNullOrEmpty(builder.Configuration.GetConnectionString("blobs"))
    || !string.IsNullOrEmpty(builder.Configuration["Aspire:Azure:Storage:Blobs:ServiceUri"]);
if (blobsConfigured)
{
    builder.AddAzureBlobServiceClient("blobs");
}

builder.Services.AddSingleton(provider =>
    new BlobStorage(provider.GetService<Azure.Storage.Blobs.BlobServiceClient>()));
builder.Services.AddScoped<ProfilePhotoService>();
builder.Services.AddScoped<IProfilePhotoStore>(provider => provider.GetRequiredService<ProfilePhotoService>());
builder.Services.AddScoped<IProfilePhotoUploadReader, HttpFormPhotoReader>();
builder.Services.AddScoped<ProfilePhotoWriter>();
builder.Services.AddScoped<AccountDeletionService>();
builder.Services.AddScoped<IAccountDeletionData>(provider => provider.GetRequiredService<AccountDeletionService>());
builder.Services.AddCropper();
// The avatar cropper returns a data URL through the interactive circuit; the default
// 32 KiB limit is far too small for an image.
builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
    options.MaximumReceiveMessageSize = 512 * 1024);

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<IdentityCancellation>();
builder.Services.AddScoped<IAdminPromotionService, AdminPromotionService>();
builder.Services.AddScoped<AccountSignInService>();
builder.Services.AddScoped<AccountPasskeyService>();
builder.Services.AddScoped<AccountRegistrationService>();
builder.Services.AddScoped<AccountEmailChangeService>();
builder.Services.AddScoped<AccountTwoFactorService>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// Family, event, potluck, notification, and administration application services.
builder.Services.AddScoped<IFamilyAccess, FamilyAccess>();
builder.Services.AddScoped<IFamilyGroups, FamilyService>();
builder.Services.AddScoped<IJoinRequests, JoinRequestService>();
builder.Services.AddScoped<IHouseholdDirectory, HouseholdService>();
builder.Services.AddScoped<IEventAccess, EventAccessService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<IEventDirectory>(provider => provider.GetRequiredService<EventService>());
builder.Services.AddScoped<IRsvpDirectory, RsvpService>();
builder.Services.AddScoped<IEventPostDirectory, EventPostService>();
builder.Services.AddScoped<CoverImageService>();
builder.Services.AddScoped<IEventCovers>(provider => provider.GetRequiredService<CoverImageService>());
builder.Services.AddScoped<IPotluckBoard, PotluckService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationPublisher>(provider => provider.GetRequiredService<NotificationService>());
builder.Services.AddScoped<INotificationQueries>(provider => provider.GetRequiredService<NotificationService>());
builder.Services.AddScoped<IAdministration, AdministrationService>();
builder.Services.AddScoped<Ruvents.UI.Planning.IPlanningApi, ServerPlanningApi>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("ruventsdb");
if (string.IsNullOrEmpty(connectionString) && builder.Configuration.GetValue<bool>("Ruvents:DesignTime"))
{
    // The deployment pipeline builds and bundles the model before Azure resolves the
    // database connection string. The AppHost sets this flag on the migration tool only,
    // so a running application never falls back to a placeholder.
    connectionString = "Host=localhost;Database=ruvents";
}

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'ruventsdb' not found. Start the application through Aspire or configure ConnectionStrings:ruventsdb.");
}
// The factory supports one context per Blazor operation and also registers the scoped context used by Identity.
builder.Services.AddDbContextFactory<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
builder.EnrichNpgsqlDbContext<ApplicationDbContext>();
// Bound readiness independently of EF's transient retries for normal application operations.
builder.Services.PostConfigure<HealthCheckServiceOptions>(options =>
{
    var databaseCheck = options.Registrations.SingleOrDefault(registration =>
        string.Equals(registration.Name, nameof(ApplicationDbContext), StringComparison.Ordinal));
    if (databaseCheck is not null)
    {
        databaseCheck.Timeout = TimeSpan.FromSeconds(5);
    }
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.Configure<PlatformAdminOptions>(builder.Configuration.GetSection(PlatformAdminOptions.SectionName));
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Accounts activate immediately: the deployment does not send email, and password
        // resets are an administrator action. The confirmation hook is still enabled
        // because SignInManager consults it on every sign-in path (password, passkey,
        // external, two-factor); SuspensionAwareUserConfirmation treats every
        // unsuspended account as confirmed and is what makes a suspension block sign-in.
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddClaimsPrincipalFactory<ProfilePhotoClaimsPrincipalFactory>()
    .AddSignInManager()
    .AddUserConfirmation<SuspensionAwareUserConfirmation>()
    .AddUserManager<CancellableUserManager>()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

// The deployment runs this image as a one-shot Container App Job to confirm the
// configured platform administrator. The flag is never set on the web application,
// and the job exits without starting the server.
if (builder.Configuration.GetValue<bool>(PlatformAdminBootstrap.ConfirmConfigurationKey))
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    try
    {
        Environment.ExitCode = await PlatformAdminBootstrap.RunAsync(app.Services, builder.Configuration, app.Logger, timeout.Token)
            ? 0
            : 1;
    }
    finally
    {
        // The job never calls RunAsync, so dispose the host explicitly to flush logs.
        await app.DisposeAsync();
    }
    return;
}

// Request scopes and interactive circuit scopes are distinct. Background revalidation
// supplies its own token in its fresh scope instead of capturing a request here.
app.Use(async (context, next) =>
{
    context.RequestServices.GetRequiredService<IdentityCancellation>().Token = context.RequestAborted;
    await next(context);
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Register these explicitly so authentication runs after the token initialization above.
// Cookie security-stamp validation then uses the request token instead of None.
app.UseAuthentication();

// Every account must have a profile photo. The claim is issued at sign-in and refreshed
// after an upload, so this check does not query the database per request.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true
        && !context.User.HasClaim(ProfilePhotoClaimsPrincipalFactory.PhotoClaimType, "1")
        && !ProfilePhotoGate.IsExempt(context.Request.Path))
    {
        var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/account/photo?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return;
    }

    await next(context);
});

app.UseAuthorization();

app.UseAntiforgery();

// The document is a development aid. Authorizing or exposing it in a deployed
// application is an application-specific decision.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Ruvents.UI.UiAssemblyMarker).Assembly);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();
app.MapProfilePhotoEndpoints();
app.MapEventCoverEndpoints();
app.MapPlanningEndpoints();
app.MapDefaultEndpoints();

await app.RunAsync();
