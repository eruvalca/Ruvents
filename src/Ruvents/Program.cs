using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ruvents.Components;
using Ruvents.Data;
using Ruvents.Features.Account.Endpoints;
using Ruvents.Features.Account.Services;
using Ruvents.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("ruventsdb")
    ?? throw new InvalidOperationException("Connection string 'ruventsdb' not found. Start the application through Aspire or configure ConnectionStrings:ruventsdb.");
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

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Ruvents.UI.UiAssemblyMarker).Assembly);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();
app.MapDefaultEndpoints();

await app.RunAsync();
