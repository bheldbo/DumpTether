param(
    [ValidateSet("Menu", "Help", "Db", "DbDown", "Mail", "Migrate", "Api", "Backend", "Web", "Frontend", "Desktop", "All", "Both", "LocalApi", "LocalBackend", "LocalAll", "LocalBoth")]
    [string] $Target = "Menu",
    [switch] $OpenBrowser,
    [string] $WindowTitle = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$webRoot = Join-Path $repoRoot "apps\web"
$apiProject = Join-Path $repoRoot "src\DumpTether.Api\DumpTether.Api.csproj"
$desktopScript = Join-Path $PSScriptRoot "desktop.ps1"
$webUrl = "http://127.0.0.1:5173"
$mailpitUrl = "http://127.0.0.1:8025"
$postgresContainerName = "dumptether-postgres"
$envFilePaths = @(
    (Join-Path $repoRoot ".env"),
    (Join-Path $repoRoot ".env.local")
)

Import-Module (Join-Path $PSScriptRoot "DumpTether.DevTools.psm1") -Force

function Repair-ProcessPathEnvironment {
    $processVariables = [Environment]::GetEnvironmentVariables(
        [EnvironmentVariableTarget]::Process)
    $hasPath = $processVariables.Contains("Path")
    $hasUpperPath = $processVariables.Contains("PATH")

    if (-not ($hasPath -and $hasUpperPath)) {
        return
    }

    $pathValue = [string] $processVariables["Path"]
    [Environment]::SetEnvironmentVariable(
        "PATH",
        $null,
        [EnvironmentVariableTarget]::Process)
    [Environment]::SetEnvironmentVariable(
        "Path",
        $pathValue,
        [EnvironmentVariableTarget]::Process)
}

Repair-ProcessPathEnvironment

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

function Assert-DockerEngine {
    $docker = Get-DockerCommand
    $serverVersion = & $docker info --format "{{.ServerVersion}}" 2>&1

    if ($LASTEXITCODE -ne 0) {
        $detail = ($serverVersion | Out-String).Trim()
        throw @"
Docker CLI was found, but the Docker engine is not available.
Start Docker Desktop and wait until the engine reports that it is running.
Docker response: $detail
"@
    }

    Write-Host "Docker engine: $serverVersion" -ForegroundColor DarkGray
    return $docker
}

function Wait-ForPostgres {
    param([int] $TimeoutSeconds = 60)

    $docker = Get-DockerCommand
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not found"

    while ((Get-Date) -lt $deadline) {
        $containerId = & $docker compose ps -q postgres 2>$null

        if (-not [string]::IsNullOrWhiteSpace($containerId)) {
            $lastStatus = (& $docker inspect `
                --format "{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}" `
                $containerId 2>$null | Select-Object -First 1).Trim()

            if ($lastStatus -eq "healthy") {
                Write-Host "PostgreSQL: healthy ($postgresContainerName)" -ForegroundColor Green
                return
            }

            if ($lastStatus -eq "exited" -or $lastStatus -eq "dead") {
                break
            }
        }

        Start-Sleep -Seconds 1
    }

    throw @"
PostgreSQL did not become healthy within $TimeoutSeconds seconds.
Looked for Docker Compose service 'postgres' / container '$postgresContainerName'.
Last container status: $lastStatus
Inspect it with: docker compose logs postgres
"@
}

function Start-Database {
    Invoke-AtRepoRoot {
        $docker = Assert-DockerEngine
        & $docker compose up -d postgres
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose could not start PostgreSQL (service 'postgres'). Exit code: $LASTEXITCODE."
        }

        Wait-ForPostgres
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
        dotnet run --project src\DumpTether.Database\DumpTether.Database.csproj --no-launch-profile --no-restore -- migrate
        if ($LASTEXITCODE -ne 0) {
            throw @"
DumpTether.Database migrate failed with exit code $LASTEXITCODE.
If this checkout has not been restored yet, run: dotnet restore DumpTether.sln
"@
        }
    }
}

function Start-Api {
    Stop-ExistingApiProcesses
    Set-DumpTetherRuntimeEnvironment
    dotnet run --project $apiProject --no-launch-profile --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw @"
DumpTether API exited with code $LASTEXITCODE.
If this checkout has not been restored yet, run: dotnet restore DumpTether.sln
"@
    }
}

function Start-LocalApi {
    Stop-ExistingApiProcesses
    Set-DumpTetherLocalRuntimeEnvironment
    dotnet run --project $apiProject --no-launch-profile --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw @"
DumpTether local API exited with code $LASTEXITCODE.
If this checkout has not been restored yet, run: dotnet restore DumpTether.sln
"@
    }
}

function Start-Desktop {
    & $desktopScript -Target Dev
    if ($LASTEXITCODE -ne 0) {
        throw "DumpTether desktop development shell exited with code $LASTEXITCODE."
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

function Test-UrlReady {
    param([string] $Url)

    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 2
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 500
    }
    catch {
        return $false
    }
}

function Test-DesktopRunning {
    return [bool] (Get-Process -Name "dumptether-desktop" -ErrorAction SilentlyContinue)
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
        $docker = Assert-DockerEngine
        & $docker compose --profile mail up -d mailpit
        if ($LASTEXITCODE -ne 0) {
            throw "Mailpit startup failed with exit code $LASTEXITCODE."
        }

        if (-not (Wait-ForUrl -Url $mailpitUrl -Name "Mailpit" -TimeoutSeconds 45)) {
            throw "Mailpit container started, but its inbox did not become reachable. Inspect it with: docker compose logs mailpit"
        }

        Write-Host "Mailpit inbox: $mailpitUrl" -ForegroundColor Green
        Write-Host "Mailpit SMTP: 127.0.0.1:1025"
    }
}

function Start-AllDevelopmentServices {
    Start-Database
    Start-Mailpit

    if (Test-UrlReady -Url $apiHealthUrl) {
        Write-Host "Hosted API is already running: $apiHealthUrl" -ForegroundColor DarkGray
    }
    else {
        Invoke-Migrations
        Start-DevWindow "DumpTether Hosted API" "Api"

        if (-not (Wait-ForUrl -Url $apiHealthUrl -Name "DumpTether hosted API" -TimeoutSeconds 90)) {
            throw @"
The hosted API did not become ready.
Review the 'DumpTether Hosted API' terminal for the startup exception.
For configuration errors, check .env and any .env.local override.
"@
        }
    }

    if (Test-UrlReady -Url $webUrl) {
        Write-Host "Vite is already running: $webUrl" -ForegroundColor DarkGray
    }
    else {
        Start-DevWindow "DumpTether Web" "Web"

        if (-not (Wait-ForUrl -Url $webUrl -Name "DumpTether Web" -TimeoutSeconds 90)) {
            throw "Vite did not become ready. Review the 'DumpTether Web' terminal."
        }
    }

    if (Test-DesktopRunning) {
        Write-Host "Desktop development shell is already running." -ForegroundColor DarkGray
    }
    else {
        Start-DevWindow "DumpTether Desktop" "Desktop"
    }

    Write-Host ""
    Write-Host "DumpTether development services" -ForegroundColor Green
    Write-Host "  Hosted API: $apiHealthUrl"
    Write-Host "  Web client: $webUrl"
    Write-Host "  Mailpit:    $mailpitUrl"
    Write-Host "  PostgreSQL: Docker container $postgresContainerName"
    Write-Host "  Desktop:    Tauri development window (local SQLite sidecar)"
}

function Show-Help {
    Write-Host ""
    Write-Host "DumpTether development runner"
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  .\scripts\dev.ps1 -Target <target> [-OpenBrowser]"
    Write-Host ""
    Write-Host "Common targets:"
    Write-Host "  All / Both       Start PostgreSQL, Mailpit, migrations, hosted API, Vite and desktop."
    Write-Host "  Backend          Start PostgreSQL, apply migrations and run only the hosted API."
    Write-Host "  Api              Run only the hosted API. Assumes PostgreSQL is already available."
    Write-Host "  Web / Frontend   Run only the Vite web client."
    Write-Host "  Desktop          Run Tauri with its local SQLite API sidecar."
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
        Write-Host "1. Everything - PostgreSQL, Mailpit, API, Vite and desktop"
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
            "1" { Start-AllDevelopmentServices }
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
    "Desktop" {
        Start-Desktop
    }
    "All" {
        Start-AllDevelopmentServices

        if ($OpenBrowser) {
            Open-UrlWhenReady -Url $webUrl -Name "DumpTether Web"
            Open-UrlWhenReady -Url $mailpitUrl -Name "Mailpit"
        }
    }
    "Both" {
        Start-AllDevelopmentServices

        if ($OpenBrowser) {
            Open-UrlWhenReady -Url $webUrl -Name "DumpTether Web"
            Open-UrlWhenReady -Url $mailpitUrl -Name "Mailpit"
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
