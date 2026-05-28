param(
    [ValidateSet("Db", "DbDown", "Migrate", "Api", "Backend", "Web", "Frontend", "All", "Both")]
    [string] $Target = "All",
    [switch] $OpenBrowser,
    [string] $WindowTitle = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$webRoot = Join-Path $repoRoot "apps\web"
$apiProject = Join-Path $repoRoot "src\DumpTether.Api\DumpTether.Api.csproj"
$connectionString = "Host=localhost;Port=5432;Database=dumptether;Username=dumptether;Password=dumptether_dev_password"
$apiHealthUrl = "http://localhost:55868/health"
$webUrl = "http://localhost:5173"

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
        Start-DevWindow "DumpTether Web" "Web"

        Write-Host "DumpTether API: $apiHealthUrl"
        Write-Host "DumpTether Web: $webUrl"

        if ($OpenBrowser) {
            Open-UrlWhenReady -Url $webUrl -Name "DumpTether Web"
        }
    }
}
