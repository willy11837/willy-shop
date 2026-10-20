---
paths:
  - "WillyShop/**/*.cs"
  - "WillyShop/**/*.cshtml"
  - "WillyShop/**/*.csproj"
  - "WillyShop/appsettings*.json"
  - "WillyShop/Properties/**"
  - "WillyShop/Data/Migrations/**"
  - "WillyShop.slnx"
  - "tools/IntegrationSmokeTests/**"
  - "tools/run-integration-tests.ps1"
---

# Willy Shop Backend Rules

These conditional rules apply to the .NET backend and its integration smoke tests.

## Technology and architecture

- Use the existing .NET 8, ASP.NET Core, nullable reference types, Entity Framework Core 8, SQL Server, and ASP.NET Core Identity patterns.
- Keep the root namespace and assembly identity as `WillyShop`.
- Preserve the existing API version prefix and established separation among controllers, contracts, services, repositories, entities, and persistence concerns.
- API controllers must use API-appropriate behavior and status codes. Do not return HTML redirects for API authentication or authorization failures.
- Keep transport DTOs separate from EF Core entities. Do not bind externally supplied payloads directly to persisted entities.
- Use dependency injection rather than service location in new backend code, except where an established framework integration requires otherwise.

## Async, data, and reliability

- Use asynchronous EF Core and I/O APIs for request-path work. Propagate `CancellationToken` through controllers, services, repositories, and EF Core calls when the operation supports cancellation.
- Avoid sync-over-async patterns such as `.Result`, `.Wait()`, and `GetAwaiter().GetResult()` in new request-path code.
- Use `AsNoTracking` for read-only EF Core queries where identity tracking is not needed.
- Prevent N+1 queries, unbounded result sets, duplicate submissions, partial writes, and non-idempotent retry hazards.
- Use explicit transaction boundaries when one business operation spans multiple dependent writes.
- Database schema changes require an EF Core migration, migration review, backward-compatible deployment consideration, and rollback guidance.
- Keep the existing database name `WebApp1Db` unless a separately approved data-migration plan changes it.

## Security and contracts

- Enforce authentication and role/policy authorization on the server.
- Preserve antiforgery protection for cookie-authenticated state-changing requests.
- Validate DTOs and domain invariants at the appropriate boundary; return consistent, non-sensitive problem details.
- Never commit real passwords, tokens, certificates, production connection strings, or personal data. Development seed credentials must remain clearly synthetic and must not be suitable for production.
- Changes to API DTOs, serialized names, enums, routes, validation, or status behavior require checking `frontend/src/api`, `frontend/src/types`, stores, and affected views.

## Validation

- Run `dotnet build WillyShop/WillyShop.csproj --configuration Release` after backend changes.
- Build `tools/IntegrationSmokeTests/IntegrationSmokeTests.csproj` when API contracts or smoke-test dependencies change.
- Run `powershell -NoProfile -ExecutionPolicy Bypass -File tools/run-integration-tests.ps1` for critical cross-boundary flows when the environment supports SQL Server LocalDB and local ports.
- Remove unused `using` directives, debug output, placeholder code, unrequested TODOs, generated `bin/obj` output, and migration artifacts not produced by the approved EF workflow.