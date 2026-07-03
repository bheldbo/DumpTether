param(
    [ValidateSet("Status", "Generated", "NodeModules", "All")]
    [string] $Target = "Status",
    [switch] $Yes
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

function Resolve-RepoPath {
    param([string] $Path)

    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))

    if (-not $fullPath.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to touch a path outside the repository: $fullPath"
    }

    return $fullPath
}

function Find-NamedDirectories {
    param([string[]] $Names)

    foreach ($relativeRoot in @("src", "tests")) {
        $searchRoot = Resolve-RepoPath $relativeRoot

        if (-not (Test-Path -LiteralPath $searchRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $searchRoot -Directory -Recurse -Force -ErrorAction SilentlyContinue |
            Where-Object { $Names -contains $_.Name } |
            Select-Object -ExpandProperty FullName
    }
}

function Get-GeneratedArtifactPaths {
    $paths = New-Object System.Collections.Generic.List[string]

    foreach ($relativePath in @(
        ".tmp",
        "apps/web/dist",
        "apps/web/.vite",
        "apps/web/.tsbuildinfo",
        "apps/desktop/src-tauri/target",
        "apps/desktop/src-tauri/binaries/publish"
    )) {
        $paths.Add((Resolve-RepoPath $relativePath))
    }

    foreach ($path in Find-NamedDirectories -Names @("bin", "obj")) {
        $paths.Add($path)
    }

    $desktopBinaryRoot = Resolve-RepoPath "apps/desktop/src-tauri/binaries"
    if (Test-Path -LiteralPath $desktopBinaryRoot) {
        Get-ChildItem `
            -LiteralPath $desktopBinaryRoot `
            -File `
            -Force `
            -Filter "dumptether-api-*" `
            -ErrorAction SilentlyContinue |
            ForEach-Object {
                $path = $_.FullName
                $paths.Add($path)
            }
    }

    foreach ($relativePath in @(
        "dumptether-api.exe",
        "dumptether-desktop.exe",
        "uninstall.exe"
    )) {
        $paths.Add((Resolve-RepoPath $relativePath))
    }

    return $paths |
        Sort-Object -Unique |
        Where-Object { Test-Path -LiteralPath $_ }
}

function Get-NodeModulePaths {
    return @(
        (Resolve-RepoPath "apps/web/node_modules"),
        (Resolve-RepoPath "apps/desktop/node_modules")
    ) | Where-Object { Test-Path -LiteralPath $_ }
}

function Get-CleanTargets {
    switch ($Target) {
        "Status" { return Get-GeneratedArtifactPaths }
        "Generated" { return Get-GeneratedArtifactPaths }
        "NodeModules" { return Get-NodeModulePaths }
        "All" { return @((Get-GeneratedArtifactPaths) + (Get-NodeModulePaths)) | ForEach-Object { $_ } | Sort-Object -Unique }
    }
}

function Get-ApproximateSize {
    param([string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return 0L
    }

    $item = Get-Item -LiteralPath $Path -Force

    if (-not $item.PSIsContainer) {
        return [int64]$item.Length
    }

    $sum = 0L
    Get-ChildItem -LiteralPath $Path -File -Recurse -Force -ErrorAction SilentlyContinue |
        ForEach-Object { $sum += $_.Length }

    return $sum
}

function Format-Size {
    param([int64] $Bytes)

    if ($Bytes -ge 1GB) {
        return "{0:N2} GB" -f ($Bytes / 1GB)
    }

    if ($Bytes -ge 1MB) {
        return "{0:N2} MB" -f ($Bytes / 1MB)
    }

    if ($Bytes -ge 1KB) {
        return "{0:N2} KB" -f ($Bytes / 1KB)
    }

    return "$Bytes B"
}

function Confirm-Clean {
    param([string[]] $Paths)

    if ($Yes) {
        return
    }

    Write-Host ""
    Write-Host "This will remove generated files only. Source, migrations, appsettings and databases are not targeted." -ForegroundColor Yellow
    Write-Host "Type CLEAN to continue."
    $confirmation = Read-Host "Confirm"

    if ($confirmation -ne "CLEAN") {
        throw "Clean cancelled."
    }
}

$paths = @(Get-CleanTargets)

if ($paths.Count -eq 0) {
    Write-Host "No matching cleanup targets found."
    return
}

$totalBytes = 0L
Write-Host "Cleanup targets for '$Target':"

foreach ($path in $paths) {
    $size = Get-ApproximateSize -Path $path
    $totalBytes += $size
    Write-Host ("- {0} ({1})" -f $path, (Format-Size $size))
}

Write-Host ("Approximate total: {0}" -f (Format-Size $totalBytes))

if ($Target -eq "Status") {
    Write-Host "Status only. Run scripts/clean.ps1 -Target Generated or -Target All to clean."
    return
}

Confirm-Clean -Paths $paths

foreach ($path in $paths) {
    if (-not (Test-Path -LiteralPath $path)) {
        continue
    }

    $resolvedPath = (Resolve-Path -LiteralPath $path).Path

    if (-not $resolvedPath.StartsWith($repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to delete outside repository: $resolvedPath"
    }

    Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    Write-Host "Removed $resolvedPath"
}
