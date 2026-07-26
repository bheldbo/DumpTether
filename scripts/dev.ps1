param(
    [ValidateSet("Menu", "Help", "Db", "DbDown", "Mail", "Migrate", "Api", "Backend", "Web", "Frontend", "All", "Both", "LocalApi", "LocalBackend", "LocalAll", "LocalBoth")]
    [string] $Target = "Menu",
    [switch] $OpenBrowser,
    [string] $WindowTitle = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$webRoot = Join-Path $repoRoot "apps\web"
$apiProject = Join-Path $repoRoot "src\DumpTether.Api\DumpTether.Api.csproj"
$webUrl = "http://127.0.0.1:5173"
$envFilePaths = @(
    (Join-Path $repoRoot ".env"),
    (Join-Path $repoRoot ".env.local")
)

Import-Module (Join-Path $PSScriptRoot "DumpTether.DevTools.psm1") -Force

function Get-EnvValue {
    param(
        [string] $Name,
        [string] $DefaultValue
    )

    return Get-DumpTetherEnvValue -Name $Name -DefaultValue $DefaultValue
}

function New-LocalConnectionString {
    $explicitConnectionString = [Environment]::GetEnvironmentVariable(
        "ConnectionStrings__DumpTether",
        "Process")

    if (-not [string]::IsNullOrWhiteSpace($explicitConnectionString)) {
        return $explicitConnectionString
    }

    $hostName = Get-EnvValue "POSTGRES_HOST" "localhost"
    $port = Get-EnvValue "POSTGRES_PORT" "5432"
    $database = Get-EnvValue "POSTGRES_DB" "dumptether"
    $username = Get-EnvValue "POSTGRES_USER" "dumptether"
    $password = Get-EnvValue "POSTGRES_PASSWORD" "dumptether_dev_password"

    return "Host=$hostName;Port=$port;Database=$database;Username=$username;Password=$password"
}

function Set-DumpTetherRuntimeEnvironment {
    Set-DumpTetherAspNetConfigurationAliases

    $databaseProvider = Get-EnvValue "Database__Provider" "Postgres"

    if ($databaseProvider -ieq "Sqlite") {
        if ($env:ConnectionStrings__DumpTether -and
            $env:ConnectionStrings__DumpTether -notmatch "(?i)(Data Source|Filename)\s*=") {
            $env:ConnectionStrings__DumpTether = $null
        }
    }
    else {
        $env:ConnectionStrings__DumpTether = New-LocalConnectionString
    }

    $env:ASPNETCORE_ENVIRONMENT = Get-EnvValue "ASPNETCORE_ENVIRONMENT" "Development"

    if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_URLS)) {
        $apiPort = Get-EnvValue "DUMPTETHER_API_PORT" "55868"
        $env:ASPNETCORE_URLS = "http://localhost:$apiPort"
    }
}

$dotenvValues = Read-DumpTetherDotEnvFiles -Paths $envFilePaths
Set-DumpTetherProcessEnvironmentFromDotEnv -Values $dotenvValues
$apiPort = Get-EnvValue "DUMPTETHER_API_PORT" "55868"
$apiHealthUrl = "http://127.0.0.1:$apiPort/health"

try {
    $consoleTitle = if ([string]::IsNullOrWhiteSpace($WindowTitle)) {
        "DumpTether $Target"
    }
    else {
        $WindowTitle
    }

    $Host.UI.RawUI.WindowTitle = $consoleTitle
}
catch {
    # Some hosts do not expose a writable console title.
}

function Get-DockerCommand {
    return Get-DumpTetherDockerCommand
}

function Invoke-AtRepoRoot {
    param([scriptblock] $Command)

    Invoke-DumpTetherAtRepoRoot -RepoRoot $repoRoot -Command $Command
}

function Start-Database {
    Invoke-AtRepoRoot {
        $docker = Get-DockerCommand
        & $docker compose up -d
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose up failed with exit code $LASTEXITCODE."
        }
    }
}

function Stop-Database {
    Invoke-AtRepoRoot {
        $docker = Get-DockerCommand
        & $docker compose down
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose down failed with exit code $LASTEXITCODE."
        }
    }
}

function Set-DumpTetherLocalRuntimeEnvironment {
    $env:ASPNETCORE_ENVIRONMENT = "Desktop"
    $env:Database__Provider = "Sqlite"
    $env:Database__ApplyMigrationsOnStartup = "true"
    $env:Auth__RequireAuthentication = "true"
    $env:Auth__AllowGuestSessions = "false"
    $env:Auth__SignupMode = "Closed"
    $env:Auth__EnableDevelopmentLogin = "false"
    $env:Auth__EnableLocalDesktopLogin = "true"
    $env:EmailConfirmation__Enabled = "false"
    $env:Email__Provider = "None"
    $env:OAuth__Microsoft__Enabled = "false"
    $env:ConnectionStrings__DumpTether = $null

    if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_URLS)) {
        $apiPort = Get-EnvValue "DUMPTETHER_API_PORT" "55868"
        $env:ASPNETCORE_URLS = "http://localhost:$apiPort"
    }
}

function Stop-ExistingApiProcesses {
    $apiOutputRoot = Join-Path $repoRoot "src\DumpTether.Api\bin"
    $apiProcesses = Get-Process -Name "DumpTether.Api" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Path -and
            $_.Path.StartsWith($apiOutputRoot, [System.StringComparison]::OrdinalIgnoreCase)
        }

    foreach ($process in $apiProcesses) {
        Write-Host "Stopping existing DumpTether.Api process $($process.Id) so build/run can replace locked files."
        Stop-Process -Id $process.Id -Force
    }
}

function Invoke-Migrations {
    Invoke-AtRepoRoot {
        Set-DumpTetherRuntimeEnvironment
        dotnet run --project src\DumpTether.Database\DumpTether.Database.csproj --no-launch-profile -- migrate
        if ($LASTEXITCODE -ne 0) {
            throw "DumpTether.Database migrate failed with exit code $LASTEXITCODE."
        }
    }
}

function Start-Api {
    Stop-ExistingApiProcesses
    Set-DumpTetherRuntimeEnvironment
    dotnet run --project $apiProject --no-launch-profile
    if ($LASTEXITCODE -ne 0) {
        throw "DumpTether API exited with code $LASTEXITCODE."
    }
}

function Start-LocalApi {
    Stop-ExistingApiProcesses
    Set-DumpTetherLocalRuntimeEnvironment
    dotnet run --project $apiProject --no-launch-profile
    if ($LASTEXITCODE -ne 0) {
        throw "DumpTether local API exited with code $LASTEXITCODE."
    }
}

function Start-Web {
    $deploymentTarget = Get-EnvValue "DUMPTETHER_DEPLOYMENT_TARGET" "development"

    Push-Location $repoRoot
    try {
        & node scripts/configure-client.mjs --target $deploymentTarget
        if ($LASTEXITCODE -ne 0) {
            throw "Client configuration failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }

    Push-Location $webRoot
    try {
        if (-not (Test-Path "node_modules")) {
            npm.cmd ci
            if ($LASTEXITCODE -ne 0) {
                throw "npm ci failed with exit code $LASTEXITCODE."
            }
        }

        npm.cmd run dev
        if ($LASTEXITCODE -ne 0) {
            throw "Vite exited with code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function Start-DevWindow {
    param(
        [string] $WindowTitle,
        [string] $RunTarget
    )

    Start-Process -FilePath powershell.exe -WorkingDirectory $repoRoot -ArgumentList @(
        "-NoExit",
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        "`"$PSCommandPath`"",
        "-WindowTitle",
        "`"$WindowTitle`"",
        "-Target",
        $RunTarget
    )
}

function Wait-ForUrl {
    param(
        [string] $Url,
        [string] $Name,
        [int] $TimeoutSeconds = 60
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 2

            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                return $true
            }
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }

    Write-Warning "$Name did not become ready at $Url within $TimeoutSeconds seconds."
    return $false
}

function Open-UrlWhenReady {
    param(
        [string] $Url,
        [string] $Name
    )

    if (Wait-ForUrl -Url $Url -Name $Name) {
        Start-Process $Url
    }
}

function Start-BrowserWatcher {
    param(
        [string] $Url,
        [string] $Name
    )

    Start-Job -ScriptBlock {
        param(
            [string] $WatcherUrl,
            [string] $WatcherName
        )

        $deadline = (Get-Date).AddSeconds(60)

        while ((Get-Date) -lt $deadline) {
            try {
                $response = Invoke-WebRequest -UseBasicParsing -Uri $WatcherUrl -TimeoutSec 2

                if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                    Start-Process $WatcherUrl
                    return
                }
            }
            catch {
                Start-Sleep -Seconds 1
            }
        }

        Write-Warning "$WatcherName did not become ready at $WatcherUrl within 60 seconds."
    } -ArgumentList $Url, $Name | Out-Null
}

function Start-Mailpit {
    Invoke-AtRepoRoot {
        $docker = Get-DockerCommand
        & $docker compose --profile mail up -d mailpit
        if ($LASTEXITCODE -ne 0) {
            throw "Mailpit startup failed with exit code $LASTEXITCODE."
        }

        Write-Host "Mailpit inbox: http://127.0.0.1:8025"
        Write-Host "Mailpit SMTP: 127.0.0.1:1025"
    }
}

function Show-Help {
    Write-Host ""
    Write-Host "DumpTether development runner"
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  .\scripts\dev.ps1 -Target <target> [-OpenBrowser]"
    Write-Host ""
    Write-Host "Common targets:"
    Write-Host "  All / Both       Start PostgreSQL, apply migrations, run API and Vite in separate windows."
    Write-Host "  Backend          Start PostgreSQL, apply migrations and run only the hosted API."
    Write-Host "  Api              Run only the hosted API. Assumes PostgreSQL is already available."
    Write-Host "  Web / Frontend   Run only the Vite web client."
    Write-Host "  LocalAll         Run local SQLite API and Vite. No Docker/PostgreSQL."
    Write-Host "  LocalApi         Run only the local SQLite API."
    Write-Host "  Migrate          Start PostgreSQL and apply EF migrations."
    Write-Host "  Db               Start PostgreSQL with docker compose."
    Write-Host "  Mail             Start the local Mailpit SMTP capture inbox."
    Write-Host "  DbDown           Stop PostgreSQL docker compose services."
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  .\scripts\dev.ps1 -Target All -OpenBrowser"
    Write-Host "  .\scripts\dev.ps1 -Target LocalAll"
    Write-Host "  .\scripts\dev.ps1 -Target Backend"
}

function Show-Menu {
    while ($true) {
        Write-Host ""
        Write-Host "DumpTether development runner"
        Write-Host "1. Web dev with PostgreSQL - DB, migrations, API and Vite"
        Write-Host "2. Offline-style web dev - local SQLite API and Vite"
        Write-Host "3. Hosted backend only - DB, migrations and API"
        Write-Host "4. Hosted API only"
        Write-Host "5. Web/Vite only"
        Write-Host "6. Local SQLite API only"
        Write-Host "7. Start PostgreSQL"
        Write-Host "8. Start Mailpit email capture"
        Write-Host "9. Stop PostgreSQL/Mailpit"
        Write-Host "10. Apply migrations"
        Write-Host "H. Help"
        Write-Host "Q. Quit"
        $choice = Read-Host "Choose"

        if ($null -eq $choice) {
            return
        }

        if ([string]::IsNullOrWhiteSpace($choice)) {
            continue
        }

        switch ($choice.ToUpperInvariant()) {
            "1" { Start-Database; Invoke-Migrations; Start-DevWindow "DumpTether API" "Api"; Wait-ForUrl -Url $apiHealthUrl -Name "DumpTether API" -TimeoutSeconds 90 | Out-Null; Start-DevWindow "DumpTether Web" "Web"; Write-Host "DumpTether API: $apiHealthUrl"; Write-Host "DumpTether Web: $webUrl" }
            "2" { Start-DevWindow "DumpTether Local API" "LocalApi"; Wait-ForUrl -Url $apiHealthUrl -Name "DumpTether Local API" -TimeoutSeconds 90 | Out-Null; Start-DevWindow "DumpTether Web" "Web"; Write-Host "DumpTether Local API: $apiHealthUrl"; Write-Host "DumpTether Web: $webUrl" }
            "3" { Start-Database; Invoke-Migrations; Start-Api; return }
            "4" { Start-Api; return }
            "5" { Start-Web; return }
            "6" { Start-LocalApi; return }
            "7" { Start-Database }
            "8" { Start-Mailpit }
            "9" { Stop-Database }
            "10" { Start-Database; Invoke-Migrations }
            "H" { Show-Help }
            "Q" { return }
            default { Write-Host "Unknown choice." -ForegroundColor Yellow }
        }
    }
}

switch ($Target) {
    "Menu" {
        Show-Menu
    }
    "Help" {
        Show-Help
    }
    "Db" {
        Start-Database
    }
    "DbDown" {
        Stop-Database
    }
    "Mail" {
        Start-Mailpit
    }
    "Migrate" {
        Start-Database
        Invoke-Migrations
    }
    "Api" {
        if ($OpenBrowser) {
            Start-BrowserWatcher -Url $apiHealthUrl -Name "DumpTether API"
        }

        Start-Api
    }
    "Backend" {
        Start-Database
        Invoke-Migrations

        if ($OpenBrowser) {
            Start-BrowserWatcher -Url $apiHealthUrl -Name "DumpTether API"
        }

        Start-Api
    }
    "Web" {
        if ($OpenBrowser) {
            Start-BrowserWatcher -Url $webUrl -Name "DumpTether Web"
        }

        Start-Web
    }
    "Frontend" {
        if ($OpenBrowser) {
            Start-BrowserWatcher -Url $webUrl -Name "DumpTether Web"
        }

        Start-Web
    }
    "All" {
        Start-Database
        Invoke-Migrations
        Start-DevWindow "DumpTether API" "Api"
        Wait-ForUrl -Url $apiHealthUrl -Name "DumpTether API" -TimeoutSeconds 90 | Out-Null
        Start-DevWindow "DumpTether Web" "Web"

        Write-Host "DumpTether API: $apiHealthUrl"
        Write-Host "DumpTether Web: $webUrl"

        if ($OpenBrowser) {
            Open-UrlWhenReady -Url $webUrl -Name "DumpTether Web"
        }
    }
    "Both" {
        Start-Database
        Invoke-Migrations
        Start-DevWindow "DumpTether API" "Api"
        Wait-ForUrl -Url $apiHealthUrl -Name "DumpTether API" -TimeoutSeconds 90 | Out-Null
        Start-DevWindow "DumpTether Web" "Web"

        Write-Host "DumpTether API: $apiHealthUrl"
        Write-Host "DumpTether Web: $webUrl"

        if ($OpenBrowser) {
            Open-UrlWhenReady -Url $webUrl -Name "DumpTether Web"
        }
    }
    "LocalApi" {
        if ($OpenBrowser) {
            Start-BrowserWatcher -Url $apiHealthUrl -Name "DumpTether Local API"
        }

        Start-LocalApi
    }
    "LocalBackend" {
        if ($OpenBrowser) {
            Start-BrowserWatcher -Url $apiHealthUrl -Name "DumpTether Local API"
        }

        Start-LocalApi
    }
    "LocalAll" {
        Start-DevWindow "DumpTether Local API" "LocalApi"
        Wait-ForUrl -Url $apiHealthUrl -Name "DumpTether Local API" -TimeoutSeconds 90 | Out-Null
        Start-DevWindow "DumpTether Web" "Web"

        Write-Host "DumpTether Local API: $apiHealthUrl"
        Write-Host "DumpTether Web: $webUrl"

        if ($OpenBrowser) {
            Open-UrlWhenReady -Url $webUrl -Name "DumpTether Web"
        }
    }
    "LocalBoth" {
        Start-DevWindow "DumpTether Local API" "LocalApi"
        Wait-ForUrl -Url $apiHealthUrl -Name "DumpTether Local API" -TimeoutSeconds 90 | Out-Null
        Start-DevWindow "DumpTether Web" "Web"

        Write-Host "DumpTether Local API: $apiHealthUrl"
        Write-Host "DumpTether Web: $webUrl"

        if ($OpenBrowser) {
            Open-UrlWhenReady -Url $webUrl -Name "DumpTether Web"
        }
    }
}
