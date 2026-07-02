param(
    [ValidateSet("Menu", "Start", "Stop", "Status", "Migrate", "SeedTestData", "ClearTasks", "ResetPostgres", "LocalInfo", "RemoveLocalSqlite")]
    [string] $Action = "Menu",
    [switch] $Yes
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$envFilePath = Join-Path $repoRoot ".env"
$postgresContainerNames = @("dumptether-postgres", "dumptether-postgres-local")
$localSqlitePath = Join-Path $env:APPDATA "DumpTether\dumptether.db"

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

function Get-DockerCommand {
    return Get-DumpTetherDockerCommand
}

function Invoke-AtRepoRoot {
    param([scriptblock] $Command)

    Invoke-DumpTetherAtRepoRoot -RepoRoot $repoRoot -Command $Command
}

function Confirm-DestructiveAction {
    param(
        [string] $Message,
        [string] $RequiredText
    )

    if ($Yes) {
        return
    }

    Write-Host ""
    Write-Host $Message -ForegroundColor Yellow
    $typed = Read-Host "Type '$RequiredText' to continue"

    if ($typed -ne $RequiredText) {
        throw "Cancelled."
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

function Set-DumpTetherEfEnvironment {
    $env:ASPNETCORE_ENVIRONMENT = Get-EnvValue "ASPNETCORE_ENVIRONMENT" "Development"
    $env:Database__Provider = Get-EnvValue "DUMPTETHER_DATABASE_PROVIDER" "Postgres"

    if ($env:Database__Provider -ieq "Sqlite") {
        $sqlitePath = Get-EnvValue "DUMPTETHER_SQLITE_PATH" ""

        if (-not [string]::IsNullOrWhiteSpace($sqlitePath)) {
            $env:Database__Sqlite__Path = $sqlitePath
        }

        $env:ConnectionStrings__DumpTether = $null
    }
    else {
        $env:ConnectionStrings__DumpTether = New-LocalConnectionString
    }
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

function Invoke-Migrations {
    Invoke-AtRepoRoot {
        Set-DumpTetherEfEnvironment
        dotnet run --project src\DumpTether.Database\DumpTether.Database.csproj --no-launch-profile -- migrate
        if ($LASTEXITCODE -ne 0) {
            throw "DumpTether.Database migrate failed with exit code $LASTEXITCODE."
        }
    }
}

function Invoke-SeedTestData {
    Set-DumpTetherEfEnvironment

    Invoke-AtRepoRoot {
        dotnet run --project src\DumpTether.Database\DumpTether.Database.csproj --no-launch-profile -- seed-test-data
        if ($LASTEXITCODE -ne 0) {
            throw "DumpTether.Database seed-test-data failed with exit code $LASTEXITCODE."
        }
    }
}

function Get-RunningPostgresContainerName {
    $docker = Get-DockerCommand

    foreach ($containerName in $postgresContainerNames) {
        $id = & $docker ps -q -f "name=^/$containerName$"

        if (-not [string]::IsNullOrWhiteSpace($id)) {
            return $containerName
        }
    }

    return $null
}

function Invoke-PostgresSql {
    param([string] $Sql)

    $containerName = Get-RunningPostgresContainerName

    if ([string]::IsNullOrWhiteSpace($containerName)) {
        throw "No DumpTether PostgreSQL container is running. Run scripts/db.ps1 -Action Start first."
    }

    $docker = Get-DockerCommand
    $database = Get-EnvValue "POSTGRES_DB" "dumptether"
    $username = Get-EnvValue "POSTGRES_USER" "dumptether"

    & $docker exec -i $containerName psql -v ON_ERROR_STOP=1 -U $username -d $database -c $Sql

    if ($LASTEXITCODE -ne 0) {
        throw "psql failed with exit code $LASTEXITCODE."
    }
}

function Clear-TaskData {
    Confirm-DestructiveAction `
        -Message "This clears tasks, notes, field values and task shares. Users, boards, categories, templates and settings stay." `
        -RequiredText "CLEAR TASKS"

    Invoke-PostgresSql "TRUNCATE TABLE task_timeline_entry_field_values, task_timeline_entries, field_values, task_item_shares, task_items RESTART IDENTITY CASCADE;"
}

function Reset-PostgresData {
    Confirm-DestructiveAction `
        -Message "This stops PostgreSQL and deletes the local Docker volume. All local PostgreSQL data is removed." `
        -RequiredText "RESET POSTGRES"

    Invoke-AtRepoRoot {
        $docker = Get-DockerCommand
        & $docker compose down -v
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose down -v failed with exit code $LASTEXITCODE."
        }
    }
}

function Show-Status {
    $docker = Get-DockerCommand
    Write-Host "PostgreSQL containers:"
    & $docker ps -a --filter "name=dumptether-postgres" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
    Write-Host ""
    Write-Host "Local SQLite:"
    Show-LocalInfo
}

function Show-LocalInfo {
    $configuredPath = Get-EnvValue "DUMPTETHER_SQLITE_PATH" ""
    $path = if ([string]::IsNullOrWhiteSpace($configuredPath)) {
        $localSqlitePath
    }
    else {
        [Environment]::ExpandEnvironmentVariables($configuredPath)
    }

    Write-Host "SQLite path: $path"
    Write-Host "Exists: $(Test-Path $path)"
}

function Remove-LocalSqlite {
    $configuredPath = Get-EnvValue "DUMPTETHER_SQLITE_PATH" ""
    $path = if ([string]::IsNullOrWhiteSpace($configuredPath)) {
        $localSqlitePath
    }
    else {
        [Environment]::ExpandEnvironmentVariables($configuredPath)
    }

    Confirm-DestructiveAction `
        -Message "This deletes the local SQLite database at $path." `
        -RequiredText "DELETE SQLITE"

    if (Test-Path $path) {
        Remove-Item -LiteralPath $path -Force
        Write-Host "Deleted $path"
    }
    else {
        Write-Host "No SQLite database found at $path"
    }
}

function Show-Menu {
    while ($true) {
        Write-Host ""
        Write-Host "DumpTether database tools"
        Write-Host "1. Start PostgreSQL"
        Write-Host "2. Stop PostgreSQL"
        Write-Host "3. Status"
        Write-Host "4. Apply EF migrations"
        Write-Host "5. Seed development test data"
        Write-Host "6. Clear task data only"
        Write-Host "7. Reset local PostgreSQL volume"
        Write-Host "8. Show local SQLite path"
        Write-Host "9. Delete local SQLite database"
        Write-Host "Q. Quit"
        $choice = Read-Host "Choose"

        switch ($choice.ToUpperInvariant()) {
            "1" { Start-Database }
            "2" { Stop-Database }
            "3" { Show-Status }
            "4" { Start-Database; Invoke-Migrations }
            "5" { Start-Database; Invoke-SeedTestData }
            "6" { Clear-TaskData }
            "7" { Reset-PostgresData }
            "8" { Show-LocalInfo }
            "9" { Remove-LocalSqlite }
            "Q" { return }
            default { Write-Host "Unknown choice." -ForegroundColor Yellow }
        }
    }
}

$dotenvValues = Read-DotEnvFile -Path $envFilePath
Set-ProcessEnvironmentFromDotEnv -Values $dotenvValues

switch ($Action) {
    "Menu" { Show-Menu }
    "Start" { Start-Database }
    "Stop" { Stop-Database }
    "Status" { Show-Status }
    "Migrate" { Start-Database; Invoke-Migrations }
    "SeedTestData" { Start-Database; Invoke-SeedTestData }
    "ClearTasks" { Clear-TaskData }
    "ResetPostgres" { Reset-PostgresData }
    "LocalInfo" { Show-LocalInfo }
    "RemoveLocalSqlite" { Remove-LocalSqlite }
}
