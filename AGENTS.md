# Repository Guidelines

## Project Structure & Modules
- Solution: BlindTreasure.API.sln (.NET 8, C# 12).
- API: BlindTreasure.API/ (Program.cs, controllers, Swagger assets, ppsettings*.json).
- Application: BlindTreasure.Application/ (services, interfaces, SignalR integration).
- Domain: BlindTreasure.Domain/ (entities, DTOs, core models).
- Infrastructure: BlindTreasure.Infrastructure/ (EF Core, persistence, integrations).
- Tests: BlindTreasure.UnitTest/ (xUnit tests by feature folder).

## Build, Test, and Run
- Build: dotnet build BlindTreasure.API.sln
- Run (dev): dotnet run --project BlindTreasure.API
- Test: dotnet test BlindTreasure.API.sln
- Docker (recommended): docker compose up -d (see docker-compose.yml). Use --build after code changes.
- Logs: docker compose logs -f blindtreasure.api | Stop: docker compose down

## Coding Style & Naming
- Indent 4 spaces; braces on new lines.
- PascalCase: classes, public methods; camelCase: locals/params; _camelCase: private fields.
- Async suffix: Async; DTO suffix: Dto or DTO.
- Use ILogger<T>; avoid Console.WriteLine.
- Run dotnet format before PRs.

## Testing Guidelines
- Framework: xUnit in BlindTreasure.UnitTest.
- Names: Method_State_Result (e.g., CreateAsync_InvalidInput_Throws).
- Mirror folder structure of source (e.g., Services/PaymentServiceTests.cs).
- Coverage (optional): dotnet test /p:CollectCoverage=true (requires coverlet).

## Commit & Pull Requests
- Commits: imperative, scoped (e.g., pi: add Stripe client factory).
- PRs: clear description, linked issues, steps to verify, screenshots for Swagger/UI changes, and notes on migrations.
- CI gates: ensure build/test pass and formatting is clean.

## Security & Configuration
- Prefer User Secrets for local dev; never commit secrets.
- Config via ppsettings.json + ppsettings.Development.json; env vars override.
- In Compose, use service names in connections (e.g., Host=postgres;, Redis=redis:6379).
- CORS policy AllowFrontend lists allowed origins; update when adding UIs.

## Architecture Notes
- Clean layering: API ? Application ? Domain ? Infrastructure.
- SignalR hubs under /hubs/*.
- Migrations run on startup via pp.ApplyMigrations(...); ensure DB reachable.