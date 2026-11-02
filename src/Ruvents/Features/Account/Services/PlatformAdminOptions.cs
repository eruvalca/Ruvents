namespace Ruvents.Features.Account.Services;

internal sealed class PlatformAdminOptions
{
    public const string SectionName = "Platform";

    /// <summary>
    /// Account promoted to the platform Admin role. The account must already be registered
    /// and its email confirmed by the operator: open registration cannot prove ownership of
    /// an address, so a matching address alone must not grant administrator privileges.
    /// Promotion happens on registration and after a successful password sign-in.
    /// </summary>
    public string? AdminEmail { get; set; }
}
