using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;

namespace Ruvents.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
[SuppressMessage("Major Code Smell", "S2094:Classes should not be empty",
    Justification = "Provides the application's Identity/EF user type while inheriting its current members from IdentityUser.")]
internal sealed class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Name shown on membership and attendee lists. Falls back to the email local part when empty.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset? SuspendedAtUtc { get; set; }

    public string? SuspendedReason { get; set; }

    public string? ProfilePhotoBlobName { get; set; }

    public string? ProfilePhotoContentType { get; set; }

    [NotMapped]
    public bool IsSuspended => SuspendedAtUtc is not null;
}

