using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;

namespace Ruvents.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
[SuppressMessage("Major Code Smell", "S2094:Classes should not be empty",
    Justification = "Provides the application's Identity/EF user type while inheriting its current members from IdentityUser.")]
internal sealed class ApplicationUser : IdentityUser
{
    [PersonalData]
    public string FirstName { get; set; } = string.Empty;

    [PersonalData]
    public string MiddleName { get; set; } = string.Empty;

    [PersonalData]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Name shown on membership, attendee, and comment lists. Defaults to the
    /// user's full name and falls back to the email local part for older accounts.
    /// </summary>
    [PersonalData]
    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset? SuspendedAtUtc { get; set; }

    public string? SuspendedReason { get; set; }

    public string? ProfilePhotoBlobName { get; set; }

    public string? ProfilePhotoContentType { get; set; }

    [NotMapped]
    public bool IsSuspended => SuspendedAtUtc is not null;
}

