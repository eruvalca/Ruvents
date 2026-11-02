namespace Ruvents.Features.Account.Services;

internal sealed class PlatformAdminOptions
{
    public const string SectionName = "Platform";

    /// <summary>
    /// Account promoted to the platform Admin role. The account must already be registered;
    /// promotion happens on registration and after a successful password sign-in.
    /// </summary>
    public string? AdminEmail { get; set; }
}
