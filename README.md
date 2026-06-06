# ModularVerticalSlice.NET

Repository baseline for the `ModularVerticalSlice.NET` starter kit and reference architecture.

The current workspace contains:

- the internal architectural blueprint in `_private/docs/`
- the baseline solution structure under `src/` and `tests/`

Implementation starts from a modular-first .NET 10 solution with:

- `ModularVerticalSlice.SharedKernel`
- `ModularVerticalSlice.Application`
- `ModularVerticalSlice.Persistence`
- `ModularVerticalSlice.WebApi`
- `ModularVerticalSlice.UnitTests`
- `ModularVerticalSlice.IntegrationTests`
- `ModularVerticalSlice.ArchitectureTests`

## Local database configuration

The public development baseline uses the disposable PostgreSQL credentials from
`docker-compose.yml`, configured in `appsettings.Development.json`.

Start the local database and WebApi:

```powershell
docker compose up -d
dotnet run --project .\src\ModularVerticalSlice.WebApi
```

For CI, production, and EF Core design-time commands, override the development
value through environment configuration or a secret manager:

```powershell
$env:ConnectionStrings__Database = "Host=localhost;Port=5432;Database=modularverticalslice;Username=<user>;Password=<password>"
```

