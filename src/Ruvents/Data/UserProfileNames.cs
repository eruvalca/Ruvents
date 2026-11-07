namespace Ruvents.Data;

/// <summary>
/// Resolves the name shown on membership, attendee, and comment lists from the
/// user's profile names, keeping the optional display-name override in one place.
/// </summary>
internal static class UserProfileNames
{
    internal const int MaxNameLength = 50;
    internal const int MaxDisplayNameLength = 100;

    internal static string ResolveDisplayName(
        string? requested,
        string? firstName,
        string? middleName,
        string? lastName,
        string fallback)
    {
        var displayName = string.IsNullOrWhiteSpace(requested)
            ? BuildDisplayName(firstName, middleName, lastName, fallback)
            : requested.Trim();

        return displayName.Length <= MaxDisplayNameLength
            ? displayName
            : TrimWithoutSplittingPair(displayName, MaxDisplayNameLength);
    }

    private static string TrimWithoutSplittingPair(string value, int maxLength)
    {
        // A hard UTF-16 slice can leave an unmatched high surrogate, which cannot be
        // encoded losslessly. Include neither half of the pair when the cut lands there.
        var length = char.IsHighSurrogate(value[maxLength - 1]) ? maxLength - 1 : maxLength;
        return value[..length];
    }

    internal static string BuildDisplayName(string? firstName, string? middleName, string? lastName, string fallback)
    {
        var joined = string.Join(
            ' ',
            new[] { firstName, middleName, lastName }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim()));

        return string.IsNullOrEmpty(joined) ? fallback : joined;
    }

    internal static string DisplayNameFromEmail(string email)
    {
        var separator = email.IndexOf('@', StringComparison.Ordinal);
        var localPart = separator > 0 ? email[..separator] : email;
        return localPart.Length <= MaxDisplayNameLength ? localPart : localPart[..MaxDisplayNameLength];
    }
}
