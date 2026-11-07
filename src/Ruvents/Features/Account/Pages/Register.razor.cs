using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Ruvents.Data;
using Ruvents.Features.Account.Extensions;
using Ruvents.Features.Account.Models;
using Ruvents.Features.Account.Services;

namespace Ruvents.Features.Account.Pages;

public sealed partial class Register
{
    private IEnumerable<IdentityError>? _identityErrors;
    private string? _photoError;
    private string? _photoPreview;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    private string? Message => _identityErrors is null ? null : $"Error: {_identityErrors.FormatDescriptions(", ")}";

    protected override void OnInitialized()
    {
        Input ??= new();

        // A failed post re-renders this page statically, so keep a validated crop in
        // view instead of asking the user to position the photo again.
        if (HttpMethods.IsPost(HttpContext.Request.Method))
        {
            _photoPreview = ProfilePhotoWriter.ReadPostedPreview(HttpContext);
        }
    }

    public async Task RegisterUserAsync(EditContext editContext)
    {
        _photoError = null;

        if (!await TryReadPhotoAsync())
        {
            return;
        }

        var result = await AccountRegistration.PasswordAsync(
            Input.Email,
            Input.Password,
            new ProfileNameInput(Input.FirstName, Input.MiddleName, Input.LastName, Input.DisplayName));
        await result.Match(
            created => CompleteRegistrationAsync(created.User),
            rejected =>
            {
                _identityErrors = rejected.Errors;
                return Task.CompletedTask;
            });
    }

    private async Task<bool> TryReadPhotoAsync()
    {
        // Only read the form when the request actually carries one; the account flows
        // are also reachable from component tests and non-form requests.
        var cropped = HttpContext.Request.HasFormContentType
            ? HttpContext.Request.Form["PhotoData"].ToString()
            : string.Empty;
        if (!string.IsNullOrEmpty(cropped))
        {
            if (!ProfilePhotoService.TryDecodeDataUrl(cropped, out var bytes, out var contentType, out var dataError))
            {
                _photoError = dataError;
                return false;
            }

            if (!ProfilePhotos.TryValidate(contentType, bytes.Length, out var sizeError))
            {
                _photoError = sizeError;
                return false;
            }

            _photoBytes = bytes;
            _photoContentType = contentType;
            return true;
        }

        // No JavaScript: the plain file input in the form supplies the photo instead.
        var upload = PhotoUploads.Read(HttpContext);
        if (upload is null)
        {
            _photoError = "Choose a profile photo, position it, and select “Use this crop”.";
            return false;
        }

        if (!ProfilePhotos.TryValidate(upload.ContentType, upload.Length, out var photoError))
        {
            _photoError = photoError;
            return false;
        }

        using var buffer = new MemoryStream();
        await using var stream = upload.OpenReadStream();
        await stream.CopyToAsync(buffer, HttpContext.RequestAborted);
        _photoBytes = buffer.ToArray();
        _photoContentType = upload.ContentType;
        return true;
    }

    private byte[]? _photoBytes;
    private string? _photoContentType;

    private async Task CompleteRegistrationAsync(ApplicationUser user)
    {
        LogUserCreatedWithPassword(Logger);

        var userId = await UserManager.GetUserIdAsync(user);
        // The account write already committed; the required photo must not be abandoned
        // by request cancellation.
        await using var content = new MemoryStream(_photoBytes!);
        var blobName = await ProfilePhotos.SaveAsync(userId, content, _photoContentType!, CancellationToken.None);
        user.ProfilePhotoBlobName = blobName;
        user.ProfilePhotoContentType = _photoContentType;
        await UserManager.UpdateAsync(user);

        await SignInManager.SignInAsync(user, isPersistent: false);
        RedirectManager.RedirectTo(ReturnUrl);
    }

    [LoggerMessage(EventId = 1011, Level = LogLevel.Information, Message = "User created a new account with password.")]
    private static partial void LogUserCreatedWithPassword(ILogger logger);

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

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = "";

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = "";
    }
}
