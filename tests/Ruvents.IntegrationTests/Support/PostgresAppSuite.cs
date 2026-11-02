using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Ruvents.IntegrationTests.Support;

[CollectionDefinition(PostgresAppFixture.CollectionName)]
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public collection definitions for discovery.")]
public sealed class PostgresAppSuite : ICollectionFixture<PostgresAppFixture>;
