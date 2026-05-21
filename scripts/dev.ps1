param(
    [ValidateSet("Db", "DbDown", "Migrate", "Api", "Web", "All")]
    [string] $Target = "All"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$webRoot = Join-Path $repoRoot "apps\web"
$apiProject = Join-Path $repoRoot "src\DumpTether.Api\DumpTether.Api.csproj"
$connectionString = "Host=localhost;Port=5432;Database=dumptether;Username=dumptether;Password=dumptether_dev_password"

function Get-DockerCommand {
    $docker = Get-Command docker -ErrorAction SilentlyContinue

    if ($docker) {
        return $docker.Source
    }

    $defaultDocker = "C:\Program Files\Docker\Docker\resources\bin\docker.exe"

    if (Test-Path $defaultDocker) {
        return $defaultDocker
    }

    throw "Docker CLI was not found. Start Docker Desktop and make sure docker.exe is on PATH."
}

function Invoke-AtRepoRoot {
    param([scriptblock] $Command)

    Push-Location $repoRoot
    try {
        & $Command
    }
    finally {
        Pop-Location
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
        dotnet tool restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet tool restore failed with exit code $LASTEXITCODE."
        }

        dotnet tool run dotnet-ef database update --project src\DumpTether.Data --startup-project src\DumpTether.Data
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet ef database update failed with exit code $LASTEXITCODE."
        }
    }
}

function Start-Api {
    $env:ConnectionStrings__DumpTether = $connectionString
    dotnet run --project $apiProject --launch-profile DumpTether.Api
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

    Start-Process powershell.exe -ArgumentList @(
        "-NoExit",
        "-ExecutionPolicy",
        "Bypass",
        "-Command",
        "title $WindowTitle; & '$PSCommandPath' -Target $RunTarget"
    )
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
        Start-Api
    }
    "Web" {
        Start-Web
    }
    "All" {
        Start-Database
        Invoke-Migrations
        Start-DevWindow "DumpTether API" "Api"
        Start-DevWindow "DumpTether Web" "Web"

        Write-Host "DumpTether API: http://localhost:55868/health"
        Write-Host "DumpTether Web: http://localhost:5173"
    }
}
