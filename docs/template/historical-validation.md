# Historical Ruvents validation

These results describe the original starter, not newly generated applications.
The test count below is historical and predates the expanded 413-test suite.

### Local Aspire verification

Verified on September 27, 2026 with the pinned SDK/packages and VS Code Aspire
extension 1.23.0:

| Check | Result |
| --- | --- |
| Environment | `aspire doctor`: 8 passed, 0 warnings, 0 failed. |
| CLI and IDE startup | PostgreSQL/database healthy; migrations finished; HTTPS web resource healthy in both runs. |
| VS Code debugging | **Aspire: Ruvents** launched the AppHost and web C# debug sessions. A breakpoint in `RegisterUserAsync` stopped at the Identity create call; variable inspection, stepping over the awaited database operation, and continue succeeded. |
| VS Code dashboard browser | Reproduced the `debugEdge` browser-attach failure while the web resource was healthy. With workspace `openExternalBrowser`, F5 and Ctrl+F5 opened the dashboard without the dialog; the web resource was healthy, and logs confirmed C# debugging enabled only for F5. |
| Identity / PostgreSQL | Disposable registration, scaffold confirmation, login/logout, profile update, and passkey-list query succeeded. Account and profile survived a full Aspire restart; the disposable account was deleted afterward. |
| Migrations | 1 applied, 0 pending; no pending model changes. |
| Readiness / liveness | Both returned 200 while healthy. With PostgreSQL stopped, `/health` returned 503 and `/alive` remained 200. Readiness recovered to 200 after database restart. |
| Developer tools | pgAdmin connected to `ruvents`; dashboard/MCP exposed HTTP and PostgreSQL traces and dashboard metrics. |
| Build / tests | `dotnet build Ruvents.slnx`: 0 warnings/errors. `dotnet test --solution Ruvents.slnx`: 21 passed, 0 failed, 0 skipped. |

Temporary breakpoints, test browsers, and Aspire verification runs were closed.
These are local acceptance checks, not a new integration-test suite. IDE breakpoint
verification covered the server-side registration handler; it did not verify
WebAssembly client breakpoints or Visual Studio debugging.
