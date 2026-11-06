# Service defaults options

These optional examples were originally commented out in
`src/Ruvents.ServiceDefaults/Extensions.cs`. Add the relevant code and dependencies
only when adopting an option. The Azure Monitor exporter is already active; it is
described at the end.

## Restrict service discovery to HTTPS

In `AddServiceDefaults`, after registering service discovery, configure the
allowed schemes. Add `using Microsoft.Extensions.ServiceDiscovery;` to the file.

```csharp
builder.Services.Configure<ServiceDiscoveryOptions>(options =>
{
    options.AllowedSchemes = ["https"];
});
```

## Instrument outgoing gRPC calls

Add the `OpenTelemetry.Instrumentation.GrpcNetClient` package to the service
defaults project. In `ConfigureOpenTelemetry`, add `AddGrpcClientInstrumentation`
to the tracing builder chain inside `WithTracing`, alongside the existing ASP.NET
Core and HTTP client instrumentation:

```csharp
.AddGrpcClientInstrumentation()
```

## Export to Azure Monitor

The `Azure.Monitor.OpenTelemetry.AspNetCore` package and its `UseAzureMonitor()`
registration are active in `AddOpenTelemetryExporters`. The deployment injects
`APPLICATIONINSIGHTS_CONNECTION_STRING`; local runs have none and continue to export
over OTLP only. Do not add a second exporter registration or read the connection
string anywhere else.
