using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Ruvents.Data;
using Ruvents.Features.Account.Extensions;
using Ruvents.Features.Account.Services;

namespace Ruvents.Features.Account.Pages.Manage;

public sealed partial class Index
{
    [Inject] private IdentityCancellation Cancellation { get; set; } = default!;

    private ApplicationUser? _user;
    private string? _username;
    private string? _phoneNumber;
    private string _defaultDisplayName = string.Empty;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    private string PhotoUrl => ProfilePhotoService.UrlFor(_user?.Id);

    protected override async Task OnInitializedAsync()
    {
        Input ??= new();

        _user = await UserManager.GetUserAsync(HttpContext.User);
        if (_user is null)
        {
            RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
            return;
        }

        _username = await UserManager.GetUserNameAsync(_user);
        _phoneNumber = await UserManager.GetPhoneNumberAsync(_user);
        _defaultDisplayName = UserProfileNames.BuildDisplayName(
            _user.FirstName,
            _user.MiddleName,
            _user.LastName,
            fallback: string.Empty);

        // Keep posted values on a validation or update failure; only load the stored
        // profile for a fresh GET.
        if (HttpContext.Request.IsGet)
        {
            Input.FirstName = _user.FirstName;
            Input.MiddleName = _user.MiddleName;
            Input.LastName = _user.LastName;
            Input.DisplayName = _user.DisplayName;
            Input.PhoneNumber = _phoneNumber;
        }
    }

    private async Task OnValidSubmitAsync()
    {
        if (_user is null)
        {
            RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
            return;
        }

        var firstName = Input.FirstName?.Trim() ?? string.Empty;
        var middleName = Input.MiddleName?.Trim() ?? string.Empty;
        var lastName = Input.LastName?.Trim() ?? string.Empty;
        var displayName = UserProfileNames.ResolveDisplayName(
            KeepsDefaultDisplayName() ? null : Input.DisplayName,
            firstName,
            middleName,
            lastName,
            UserProfileNames.DisplayNameFromEmail(_username ?? string.Empty));
        var phoneNumber = string.IsNullOrWhiteSpace(Input.PhoneNumber) ? null : Input.PhoneNumber.Trim();

        var changed = false;
        changed |= SetValue(_user.FirstName, firstName, value => _user.FirstName = value ?? string.Empty);
        changed |= SetValue(_user.MiddleName, middleName, value => _user.MiddleName = value ?? string.Empty);
        changed |= SetValue(_user.LastName, lastName, value => _user.LastName = value ?? string.Empty);
        changed |= SetValue(_user.DisplayName, displayName, value => _user.DisplayName = value ?? string.Empty);
        changed |= SetValue(_user.PhoneNumber, phoneNumber, value => _user.PhoneNumber = value);

        if (changed)
        {
            var result = await UserManager.UpdateAsync(_user);
            if (!result.Succeeded)
            {
                RedirectManager.RedirectToCurrentPageWithStatus($"Error: {result.Errors.FormatDescriptions(", ")}", HttpContext);
                return;
            }

            // The profile write is committed, so refreshing the sign-in cookie must run
            // to completion even if the request disconnects.
            Cancellation.CompleteWrite();
        }

        await SignInManager.RefreshSignInAsync(_user);
        RedirectManager.RedirectToCurrentPageWithStatus("Your profile has been updated", HttpContext);
    }

    private bool KeepsDefaultDisplayName()
    {
        if (string.IsNullOrWhiteSpace(Input.DisplayName))
        {
            return true;
        }

        var posted = Input.DisplayName.Trim();
        // Accounts created before names were collected defaulted to the email local part;
        // that value should follow the names once they are filled in.
        var legacyDefault = _defaultDisplayName.Length == 0
            ? UserProfileNames.DisplayNameFromEmail(_username ?? string.Empty)
            : _defaultDisplayName;
        return string.Equals(posted, legacyDefault, StringComparison.Ordinal);
    }

    private static bool SetValue(string? current, string? value, Action<string?> apply)
    {
        if (string.Equals(current, value, StringComparison.Ordinal))
        {
            return false;
        }

        apply(value);
        return true;
    }

    private sealed class InputModel
    {
        [Required]
        [StringLength(UserProfileNames.MaxNameLength)]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = "";

        [StringLength(UserProfileNames.MaxNameLength)]
        [Display(Name = "Middle name")]
        public string MiddleName { get; set; } = "";

        [Required]
        [StringLength(UserProfileNames.MaxNameLength)]
        [Display(Name = "Last name")]
        public string LastName { get; set; } = "";

        [StringLength(UserProfileNames.MaxDisplayNameLength)]
        [Display(Name = "Display name")]
        public string? DisplayName { get; set; }

        [OptionalPhone]
        [Display(Name = "Phone number")]
        public string? PhoneNumber { get; set; }
    }

    /// <summary>
    /// Applies the normal phone-number check but accepts an empty value so the user can
    /// remove a saved number. <see cref="PhoneAttribute"/> rejects an empty string even
    /// though it accepts null.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    private sealed class OptionalPhoneAttribute : ValidationAttribute
    {
        private static readonly PhoneAttribute _phone = new();

        public OptionalPhoneAttribute()
            : base("The {0} field is not a valid phone number.")
        {
        }

        public override bool IsValid(object? value) =>
            value is not string text || string.IsNullOrWhiteSpace(text) || _phone.IsValid(text);
    }
}
