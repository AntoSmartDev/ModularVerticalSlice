[CmdletBinding()]
param(
    [switch]$StartDatabase,
    [switch]$SkipIntegrationTests
)

$ErrorActionPreference = 'Stop'
$defaultConnectionString =
    'Host=localhost;Port=5432;Database=modularverticalslice;Username=postgres;Password=postgres'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solution = Join-Path $repositoryRoot 'ModularVerticalSlice.slnx'
$unitTests = Join-Path $repositoryRoot 'tests/ModularVerticalSlice.UnitTests/ModularVerticalSlice.UnitTests.csproj'
$architectureTests = Join-Path $repositoryRoot 'tests/ModularVerticalSlice.ArchitectureTests/ModularVerticalSlice.ArchitectureTests.csproj'
$integrationTests = Join-Path $repositoryRoot 'tests/ModularVerticalSlice.IntegrationTests/ModularVerticalSlice.IntegrationTests.csproj'

function Write-Step {
    param([string]$Name)

    Write-Host "`n==> $Name" -ForegroundColor Cyan
}

function Invoke-CommandChecked {
    param(
        [string]$Command,
        [string[]]$Arguments
    )

    Write-Host "    $Command $($Arguments -join ' ')" -ForegroundColor DarkGray
    & $Command @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $Command $($Arguments -join ' ')"
    }
}

function Assert-CommandAvailable {
    param([string]$Command)

    if (-not (Get-Command $Command -ErrorAction SilentlyContinue)) {
        throw "Required command '$Command' was not found."
    }
}

function Start-Postgres {
    Write-Step 'Start PostgreSQL'
    Assert-CommandAvailable 'docker'
    Invoke-CommandChecked 'docker' @('compose', 'up', '-d', 'postgres')

    Write-Host '    Waiting for PostgreSQL to accept connections...' -ForegroundColor DarkGray
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        & docker compose exec -T postgres pg_isready -U postgres -d modularverticalslice *> $null
        if ($LASTEXITCODE -eq 0) {
            Write-Host '    PostgreSQL is ready.' -ForegroundColor Green
            return
        }

        Start-Sleep -Seconds 2
    }

    throw 'PostgreSQL did not become ready within 60 seconds.'
}

Set-Location $repositoryRoot

try {
    Write-Step 'Check prerequisites'
    Assert-CommandAvailable 'dotnet'

    if (-not $env:ConnectionStrings__Database) {
        $env:ConnectionStrings__Database = $defaultConnectionString
        Write-Host '    Using the disposable local PostgreSQL connection string.' -ForegroundColor DarkGray
    }
    else {
        Write-Host '    Using ConnectionStrings__Database from the environment.' -ForegroundColor DarkGray
    }

    if ($StartDatabase) {
        Start-Postgres
    }

    Write-Step 'Restore dependencies'
    Invoke-CommandChecked 'dotnet' @('restore', $solution)

    Write-Step 'Build solution'
    Invoke-CommandChecked 'dotnet' @('build', $solution, '--no-restore')

    Write-Step 'Run unit tests'
    Invoke-CommandChecked 'dotnet' @('test', $unitTests, '--no-build', '--no-restore')

    Write-Step 'Run architecture tests'
    Invoke-CommandChecked 'dotnet' @('test', $architectureTests, '--no-build', '--no-restore')

    if ($SkipIntegrationTests) {
        Write-Host "`nWARNING: Integration tests were skipped. Verification is incomplete." -ForegroundColor Yellow
    }
    else {
        Write-Step 'Run integration tests'
        Invoke-CommandChecked 'dotnet' @('test', $integrationTests, '--no-build', '--no-restore')
    }

    Write-Host "`nVerification completed successfully." -ForegroundColor Green
}
catch {
    Write-Error $_
    exit 1
}
finally {
    Set-Location $repositoryRoot
}
