# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build the solution
dotnet build POSI.sln

# Run the API
dotnet run --project Src/POSI.Api/POSI.Api.csproj

# Run tests (when test projects are added)
dotnet test POSI.sln

# Run a single test
dotnet test --filter "FullyQualifiedName~TestName"
```

Swagger UI is available at `https://localhost:{port}/swagger` when running in Development mode.

## Architecture

This is a .NET 8 ASP.NET Core Web API using a layered clean architecture. Dependency flows inward:

```
POSI.Api → POSI.Services → POSI.Data → POSI.Domain
                         ↗
              POSI.Api  →
```

- **POSI.Domain** — Core entities and domain models. No dependencies on other projects.
- **POSI.Data** — Data access layer. References Domain.
- **POSI.Services** — Business logic. References Domain and Data.
- **POSI.Api** — ASP.NET Core entry point (controllers/minimal API). References Services and Data directly.

Note: There is a `POSSI.Domain` folder (typo) that appears to be a duplicate/leftover — it also contains a `POSI.Domain.csproj` but is not included in the solution.

## Key Packages (POSI.Api)

- `Serilog.AspNetCore` — structured logging
- `Swashbuckle.AspNetCore` — Swagger/OpenAPI
- `System.IdentityModel.Tokens.Jwt` — JWT authentication
