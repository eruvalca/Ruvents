using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Ruvents.Features.Account.Services;
using Ruvents.SharedKernel.Features.Account;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class CancellationContractTests
{
    [Fact]
    public void SharedAsyncContractsAndApplicationServicesRequireExplicitFinalTokens()
    {
        // These adapters use Identity's tokenless APIs and AspNetUserManager's captured request token.
        Type[] requestBoundIdentityAdapters = [typeof(AccountSignInService), typeof(AccountPasskeyService),
            typeof(AccountEmailChangeService), typeof(AccountTwoFactorService), typeof(IdentityNoOpEmailSender)];
        var shared = typeof(IAccountQueries).Assembly.GetTypes().Where(type => type.IsInterface);
        var services = typeof(AccountQueries).Assembly.GetTypes().Where(type =>
            type.Namespace?.EndsWith(".Services", StringComparison.Ordinal) == true && !requestBoundIdentityAdapters.Contains(type));
        var methods = shared.Concat(services)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => IsAsync(method.ReturnType)).ToArray();
        methods.ShouldNotBeEmpty();

        foreach (var method in methods)
        {
            var token = method.GetParameters().LastOrDefault();
            token.ShouldNotBeNull($"{method.DeclaringType?.Name}.{method.Name} must accept a token");
            token.ParameterType.ShouldBe(typeof(CancellationToken), method.ToString());
            token.IsOptional.ShouldBeFalse(method.ToString());
        }
    }

    private static bool IsAsync(Type type) => typeof(Task).IsAssignableFrom(type)
        || type == typeof(ValueTask)
        || (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(ValueTask<>) || type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)));
}
