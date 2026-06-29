param(
    [ValidateSet("Db", "DbDown", "Migrate", "Api", "Backend", "Web", "Frontend", "All", "Both", "LocalApi", "LocalBackend", "LocalAll", "LocalBoth")]
    [string] $Target = "All",
    [switch] $OpenBrowser,
    [string] $WindowTitle = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$webRoot = Join-Path $repoRoot "apps\web"
$apiProject = Join-Path $repoRoot "src\DumpTether.Api\DumpTether.Api.csproj"
$webUrl = "http://127.0.0.1:5173"
$envFilePath = Join-Path $repoRoot ".env"

Import-Module (Join-Path $PSScriptRoot "DumpTether.DevTools.psm1") -Force

function Read-DotEnvFile {
    param([string] $Path)

    return Read-DumpTetherDotEnvFile -Path $Path
}

function Remove-InlineDotEnvComment {
    param([string] $Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Value
    }

    $quote = [char]0

    for ($index = 0; $index -lt $Value.Length; $index++) {
        $character = $Value[$index]

        if ($quote -ne [char]0) {
            if ($character -eq $quote) {
                $quote = [char]0
            }

            continue
        }

        if ($character -eq '"' -or $character -eq "'") {
            $quote = $character
            continue
        }

        if ($character -eq '#' -and
            ($index -eq 0 -or [char]::IsWhiteSpace($Value[$index - 1]))) {
            return $Value.Substring(0, $index).TrimEnd()
        }
    }

    return $Value
}

function Set-ProcessEnvironmentFromDotEnv {
    param([hashtable] $Values)

    Set-DumpTetherProcessEnvironmentFromDotEnv -Values $Values
}

function Get-EnvValue {
    param(
        [string] $Name,
        [string] $DefaultValue
    )

    return Get-DumpTetherEnvValue -Name $Name -DefaultValue $DefaultValue
}

function Set-DumpTetherConfigAliases {
    $aliases = @{
        "DUMPTETHER_APPLY_MIGRATIONS_ON_STARTUP" = "Database__ApplyMigrationsOnStartup"
        "DUMPTETHER_DATABASE_PROVIDER" = "Database__Provider"
        "DUMPTETHER_SQLITE_PATH" = "Database__Sqlite__Path"
        "DUMPTETHER_REQUIRE_AUTHENTICATION" = "Auth__RequireAuthentication"
        "DUMPTETHER_ALLOW_GUEST_SESSIONS" = "Auth__AllowGuestSessions"
        "DUMPTETHER_ENABLE_DEVELOPMENT_LOGIN" = "Auth__EnableDevelopmentLogin"
        "DUMPTETHER_DEVELOPMENT_EMAIL" = "Auth__DevelopmentEmail"
        "DUMPTETHER_DEVELOPMENT_PASSWORD" = "Auth__DevelopmentPassword"
        "DUMPTETHER_DEVELOPMENT_DISPLAY_NAME" = "Auth__DevelopmentDisplayName"
        "DUMPTETHER_AUTH_SESSION_DAYS" = "Auth__SessionDays"
        "DUMPTETHER_AUTH_SESSION_CLEANUP_DAYS" = "Auth__SessionCleanupDays"
        "DUMPTETHER_AUTH_SESSION_CLEANUP_INTERVAL_HOURS" = "Auth__SessionCleanupIntervalHours"
        "DUMPTETHER_ARCHIVE_RETENTION_DAYS" = "Archive__RetentionDays"
        "DUMPTETHER_CORS_ALLOWED_ORIGIN_0" = "Cors__AllowedOrigins__0"
        "DUMPTETHER_CORS_ALLOWED_ORIGIN_1" = "Cors__AllowedOrigins__1"
        "DUMPTETHER_CORS_ALLOWED_ORIGIN_2" = "Cors__AllowedOrigins__2"
        "DUMPTETHER_EMAIL_CONFIRMATION_ENABLED" = "EmailConfirmation__Enabled"
        "DUMPTETHER_EMAIL_CONFIRMATION_PUBLIC_BASE_URL" = "EmailConfirmation__PublicBaseUrl"
        "DUMPTETHER_EMAIL_FROM" = "Email__FromEmail"
        "DUMPTETHER_EMAIL_FROM_NAME" = "Email__FromName"
        "DUMPTETHER_EMAIL_SMTP_ENABLED" = "Email__Smtp__Enabled"
        "DUMPTETHER_EMAIL_SMTP_HOST" = "Email__Smtp__Host"
        "DUMPTETHER_EMAIL_SMTP_PORT" = "Email__Smtp__Port"
        "DUMPTETHER_EMAIL_SMTP_USERNAME" = "Email__Smtp__Username"
        "DUMPTETHER_EMAIL_SMTP_PASSWORD" = "Email__Smtp__Password"
        "DUMPTETHER_EMAIL_BREVO_API_ENABLED" = "Email__BrevoApi__Enabled"
        "DUMPTETHER_EMAIL_BREVO_API_KEY" = "Email__BrevoApi__ApiKey"
        "DUMPTETHER_EMAIL_MFA_ENABLED" = "Mfa__Email__Enabled"
        "DUMPTETHER_OAUTH_GOOGLE_ENABLED" = "OAuth__Google__Enabled"
        "DUMPTETHER_OAUTH_GOOGLE_CLIENT_ID" = "OAuth__Google__ClientId"
        "DUMPTETHER_OAUTH_GOOGLE_CLIENT_SECRET" = "OAuth__Google__ClientSecret"
        "DUMPTETHER_OAUTH_MICROSOFT_ENABLED" = "OAuth__Microsoft__Enabled"
        "DUMPTETHER_OAUTH_MICROSOFT_CLIENT_ID" = "OAuth__Microsoft__ClientId"
        "DUMPTETHER_OAUTH_MICROSOFT_CLIENT_SECRET" = "OAuth__Microsoft__ClientSecret"
        "DUMPTETHER_OAUTH_FACEBOOK_ENABLED" = "OAuth__Facebook__Enabled"
        "DUMPTETHER_OAUTH_FACEBOOK_CLIENT_ID" = "OAuth__Facebook__ClientId"
        "DUMPTETHER_OAUTH_FACEBOOK_CLIENT_SECRET" = "OAuth__Facebook__ClientSecret"
        "DUMPTETHER_MAX_ACTIVE_TASKS_PER_WORKSPACE" = "Usage__MaxActiveTasksPerWorkspace"
        "DUMPTETHER_MAX_TOTAL_TASKS_PER_WORKSPACE" = "Usage__MaxTotalTasksPerWorkspace"
    }

    foreach ($alias in $aliases.GetEnumerator()) {
        $value = [Environment]::GetEnvironmentVariable($alias.Key, "Process")

        if (-not [string]::IsNullOrWhiteSpace($value)) {
            [Environment]::SetEnvironmentVariable($alias.Value, $value, "Process")
        }
    }
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
    Set-DumpTetherConfigAliases

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

$dotenvValues = Read-DotEnvFile -Path $envFilePath
Set-ProcessEnvironmentFromDotEnv -Values $dotenvValues
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
    Set-DumpTetherConfigAliases
    $env:Database__Provider = "Sqlite"
    $env:Database__ApplyMigrationsOnStartup = "true"
    $env:ConnectionStrings__DumpTether = $null
    $env:ASPNETCORE_ENVIRONMENT = Get-EnvValue "ASPNETCORE_ENVIRONMENT" "Development"

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
}

function Start-LocalApi {
    Stop-ExistingApiProcesses
    Set-DumpTetherLocalRuntimeEnvironment
    dotnet run --project $apiProject --no-launch-profile
}

function Start-Web {
    Push-Location $webRoot
    try {
        if (-not (Test-Path "node_modules")) {
            npm.cmd ci
            if ($LASTEXITCODE -ne 0) {
                throw "npm ci failed with exit code $LASTEXITCODE."
            }
        }

        npm.cmd run dev
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

switch ($Target) {
    "Db" {
        Start-Database
    }
    "DbDown" {
        Stop-Database
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
