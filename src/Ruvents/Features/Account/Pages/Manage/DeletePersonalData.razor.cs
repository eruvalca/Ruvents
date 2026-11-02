using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Ruvents.Data;
using Ruvents.Features.Account.Services;

namespace Ruvents.Features.Account.Pages.Manage;

public sealed partial class DeletePersonalData
{
    [Inject] private IdentityCancellation Cancellation { get; set; } = default!;

    [Inject] private IAccountDeletionData AccountData { get; set; } = default!;

    private string? _message;
    private ApplicationUser? _user;
    private bool _requirePassword;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromForm]
    private InputModel Input { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        Input ??= new();

        _user = await UserManager.GetUserAsync(HttpContext.User);
        if (_user is null)
        {
            RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
            return;
        }
        _requirePassword = await UserManager.HasPasswordAsync(_user);
    }

    private async Task OnValidSubmitAsync()
    {
        if (_user is null)
        {
            RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
            return;
        }

        if (_requirePassword && !await UserManager.CheckPasswordAsync(_user, Input.Password))
        {
            _message = "Error: Incorrect password.";
            return;
        }

        // Everything from here is irreversible, starting with the account's planning
        // data, so it must not be abandoned partway by request cancellation. The service
        // deletes the data and the account in one transaction.
        Cancellation.CompleteWrite();

        var userId = _user.Id;
        if (!await AccountData.DeleteAccountAsync(_user, Cancellation.Token))
        {
            throw new InvalidOperationException("Unexpected error occurred deleting user.");
        }

        await SignInManager.SignOutAsync();

        LogUserDeleted(Logger, userId);

        RedirectManager.RedirectToCurrentPage();
    }

    [LoggerMessage(EventId = 1013, Level = LogLevel.Information, Message = "User with ID '{UserId}' deleted themselves.")]
    private static partial void LogUserDeleted(ILogger logger, string userId);

    private sealed class InputModel
    {
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }
}
