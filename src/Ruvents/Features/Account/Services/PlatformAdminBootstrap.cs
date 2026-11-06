using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ruvents.Data;

namespace Ruvents.Features.Account.Services;

/// <summary>
/// Confirms the configured platform administrator without an email provider. The
/// deployment runs this as a one-shot Container App Job after the operator registers
/// the account, so promotion still requires an explicit operator action rather than
/// self-confirmation.
/// </summary>
internal static partial class PlatformAdminBootstrap
{
    /// <summary>
    /// Configuration flag that turns the container into the one-shot administrator job.
    /// It is set only on the job, never on the web application.
    /// </summary>
    internal const string ConfirmConfigurationKey = "Ruvents:ConfirmAdmin";

    internal static async Task<bool> RunAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await ConfirmAsync(
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<IAdminPromotionService>(),
            configuration["Platform:AdminEmail"],
            logger,
            cancellationToken);
    }

    internal static async Task<bool> ConfirmAsync(
        UserManager<ApplicationUser> userManager,
        IAdminPromotionService adminPromotion,
        string? adminEmail,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            LogMissingConfiguration(logger);
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(adminEmail.Trim());
        if (user is null)
        {
            LogAccountNotFound(logger, adminEmail);
            return false;
        }

        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            // The no-op email sender cannot deliver a confirmation link in a hosted
            // environment. Running this operator-controlled job is the out-of-band
            // check that the configured address belongs to the intended administrator.
            user.EmailConfirmed = true;
            var update = await userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                LogConfirmationFailed(logger, string.Join(", ", update.Errors.Select(error => error.Description)));
                return false;
            }
        }

        var promoted = await adminPromotion.PromoteIfConfiguredAsync(user, cancellationToken);
        LogCompleted(logger, adminEmail, promoted);
        return true;
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "Platform:AdminEmail is not configured; the administrator account cannot be confirmed.")]
    private static partial void LogMissingConfiguration(ILogger logger);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "No account is registered for the configured platform administrator email '{Email}'. Register the account first, then run this job again.")]
    private static partial void LogAccountNotFound(ILogger logger, string email);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error, Message = "Confirming the platform administrator account failed: {Errors}")]
    private static partial void LogConfirmationFailed(ILogger logger, string errors);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Information, Message = "Platform administrator '{Email}' is confirmed. Promotion changed the role: {Promoted}.")]
    private static partial void LogCompleted(ILogger logger, string email, bool promoted);
}
