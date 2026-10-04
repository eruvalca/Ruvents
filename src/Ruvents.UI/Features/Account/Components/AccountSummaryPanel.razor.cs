using Ruvents.SharedKernel.Features.Account;
using Ruvents.UI.Components;

namespace Ruvents.UI.Features.Account.Components;

public sealed partial class AccountSummaryPanel(IAccountQueries queries)
{
    private readonly LatestOperation _loads = new();
    private AccountSummary? _summary;
    private string? _error;
    private bool _loading;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        using var operation = _loads.Begin(TimeSpan.FromSeconds(15), ComponentCancellationToken);
        _loading = true;
        _error = null;
        try
        {
            var summary = await queries.GetCurrentAsync(operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (operation.IsCurrent && !IsDisposed)
            {
                _summary = summary;
                _error = summary is null ? "Your account could not be found." : null;
            }
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested)
        {
            if (operation.IsCurrent && !IsDisposed)
            {
                _error = "Loading your account timed out. Please try again.";
            }
        }
        catch (HttpRequestException)
        {
            if (operation.IsCurrent && !IsDisposed)
            {
                _error = "Your account could not be loaded. Please try again.";
            }
        }
        catch (OperationCanceledException)
        {
            // A dependency's independent cancellation/timeout must not silently look like a successful load.
            if (operation.IsCurrent && !IsDisposed)
            {
                _error = "Your account request was interrupted. Please try again.";
            }
        }
        finally
        {
            if (operation.IsCurrent && !IsDisposed)
            {
                _loading = false;
            }
        }
    }

    protected override ValueTask OnDisposeAsync()
    {
        _loads.Dispose();
        return ValueTask.CompletedTask;
    }
}
