using System.Diagnostics.CodeAnalysis;
using Ruvents.SharedKernel;
using Ruvents.UI;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using Shouldly;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Ruvents.UnitTests.Architecture;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class AssemblyDependencyTests
{
    private static readonly Assembly _sharedKernel = typeof(SharedKernelAssemblyMarker).Assembly;
    private static readonly Assembly _sharedUi = typeof(UiAssemblyMarker).Assembly;
    private static readonly Assembly _browserHost = Assembly.Load("Ruvents.Client");
    private static readonly ArchUnitNET.Domain.Architecture _architecture = new ArchLoader()
        .LoadAssemblies(_sharedKernel, _sharedUi, _browserHost)
        .Build();

    [Fact]
    public void SharedKernelDoesNotDependOnOtherApplicationAssemblies()
    {
        AssertOnlyApplicationDependencies(_sharedKernel);
    }

    [Fact]
    public void SharedUiDependsOnlyOnSharedKernel()
    {
        AssertOnlyApplicationDependencies(_sharedUi, _sharedKernel);
    }

    [Fact]
    public void BrowserHostDependsOnlyOnSharedUiAndSharedKernel()
    {
        AssertOnlyApplicationDependencies(_browserHost, _sharedUi, _sharedKernel);
    }

    [Theory]
    [InlineData(typeof(SharedKernelAssemblyMarker))]
    [InlineData(typeof(UiAssemblyMarker))]
    [InlineData("Ruvents.Client")]
    public void BrowserCompatibleAssembliesDoNotDependOnServerInfrastructure(object assemblyMarker)
    {
        ArgumentNullException.ThrowIfNull(assemblyMarker);
        var subject = assemblyMarker switch
        {
            System.Type marker => marker.Assembly,
            string name => Assembly.Load(name),
            _ => throw new ArgumentException("Expected a type marker or an assembly name.", nameof(assemblyMarker)),
        };

        var infrastructure = Types(includeReferenced: true).That().ResideInNamespaceMatching(
            @"^(Microsoft\.EntityFrameworkCore|Microsoft\.AspNetCore\.Identity|Npgsql|Aspire)(\.|$)");

        AssertNoDependencies(subject, infrastructure,
            "EF Core, PostgreSQL, server Identity, and Aspire belong in server or hosting projects.");
    }

    [Fact]
    public void SharedKernelDoesNotDependOnPresentationFrameworks()
    {
        var presentation = Types(includeReferenced: true).That().ResideInNamespaceMatching(
            @"^(Microsoft\.AspNetCore|Microsoft\.JSInterop)(\.|$)");

        AssertNoDependencies(_sharedKernel, presentation,
            "Shared contracts and calculations must be independent of web and UI frameworks.");
    }

    private static void AssertOnlyApplicationDependencies(Assembly subject, params Assembly[] allowedDependencies)
    {
        var forbidden = Types(includeReferenced: true).That()
            .ResideInAssemblyMatching(@"^Ruvents(\.|,|$)")
            .And().DoNotResideInAssembly(subject, allowedDependencies);

        AssertNoDependencies(subject, forbidden,
            $"{subject.GetName().Name} must respect the application project dependency direction.");
    }

    private static void AssertNoDependencies(Assembly subject, IObjectProvider<IType> forbidden, string reason)
    {
        var subjectTypes = Types().That().ResideInAssembly(subject);
        subjectTypes.GetObjects(_architecture).ShouldNotBeEmpty($"No types were loaded from {subject.GetName().Name}.");

        var rule = subjectTypes.Should().NotDependOnAny(forbidden).Because(reason);
        rule.Evaluate(_architecture)
            .Where(result => !result.Passed)
            .Select(result => result.Description)
            .ShouldBeEmpty(reason);
    }
}
