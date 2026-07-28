param(
    [ValidateSet("Menu", "Status", "DesktopDebug", "DesktopRelease", "DesktopTarget", "Generated", "NodeModules", "All")]
    [string] $Target = "Menu",
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
    param([string] $RequestedTarget = $Target)

    switch ($RequestedTarget) {
        "Menu" { return @() }
        "Status" { return Get-GeneratedArtifactPaths }
        "DesktopDebug" { return @((Resolve-RepoPath "apps/desktop/src-tauri/target/debug")) | Where-Object { Test-Path -LiteralPath $_ } }
        "DesktopRelease" { return @((Resolve-RepoPath "apps/desktop/src-tauri/target/release")) | Where-Object { Test-Path -LiteralPath $_ } }
        "DesktopTarget" { return @((Resolve-RepoPath "apps/desktop/src-tauri/target")) | Where-Object { Test-Path -LiteralPath $_ } }
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
    param(
        [string] $TargetName,
        [string[]] $Paths
    )

    if ($Yes) {
        return
    }

    Write-Host ""
    Write-Host "This will remove generated files only. Source, migrations, appsettings and databases are not targeted." -ForegroundColor Yellow
    Write-Host "Selected cleanup target: $TargetName"
    Write-Host "Type CLEAN to continue."
    $confirmation = Read-Host "Confirm"

    if ($confirmation -ne "CLEAN") {
        throw "Clean cancelled."
    }
}

function Invoke-CleanTarget {
    param([string] $TargetName)

    $paths = @(Get-CleanTargets -RequestedTarget $TargetName)

    if ($paths.Count -eq 0) {
        Write-Host "No matching cleanup targets found for '$TargetName'."
        return
    }

    $totalBytes = 0L
    Write-Host ""
    Write-Host "Cleanup targets for '$TargetName':"

    foreach ($path in $paths) {
        $size = Get-ApproximateSize -Path $path
        $totalBytes += $size
        Write-Host ("- {0} ({1})" -f $path, (Format-Size $size))
    }

    Write-Host ("Approximate total: {0}" -f (Format-Size $totalBytes))

    if ($TargetName -eq "Status") {
        Write-Host ""
        Write-Host "Status only. Cleanup options:"
        Write-Host "  .\scripts\clean.ps1                     Show an interactive cleanup menu."
        Write-Host "  .\scripts\clean.ps1 -Target DesktopDebug    Remove apps/desktop/src-tauri/target/debug."
        Write-Host "  .\scripts\clean.ps1 -Target DesktopRelease  Remove apps/desktop/src-tauri/target/release."
        Write-Host "  .\scripts\clean.ps1 -Target DesktopTarget   Remove all Tauri/Rust target output."
        Write-Host "  .\scripts\clean.ps1 -Target Generated       Remove build output, bin/obj, Vite dist/cache, Tauri target and sidecar publish output."
        Write-Host "  .\scripts\clean.ps1 -Target NodeModules     Remove frontend and desktop node_modules only."
        Write-Host "  .\scripts\clean.ps1 -Target All             Remove generated output and node_modules."
        Write-Host ""
        Write-Host "Add -Yes to skip the CLEAN confirmation prompt for destructive targets."
        return
    }

    Confirm-Clean -TargetName $TargetName -Paths $paths

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
}

function Show-Menu {
    while ($true) {
        Write-Host ""
        Write-Host "DumpTether cleanup"
        Write-Host "1. Status only - show generated output and sizes"
        Write-Host "2. Remove desktop Tauri debug target"
        Write-Host "3. Remove desktop Tauri release target"
        Write-Host "4. Remove all desktop Tauri target output"
        Write-Host "5. Remove all generated output"
        Write-Host "6. Remove node_modules"
        Write-Host "7. Remove generated output and node_modules"
        Write-Host "Q. Quit"
        $choice = Read-Host "Choose"

        if ($null -eq $choice) {
            return
        }

        if ([string]::IsNullOrWhiteSpace($choice)) {
            continue
        }

        switch ($choice.ToUpperInvariant()) {
            "1" { Invoke-CleanTarget -TargetName "Status" }
            "2" { Invoke-CleanTarget -TargetName "DesktopDebug" }
            "3" { Invoke-CleanTarget -TargetName "DesktopRelease" }
            "4" { Invoke-CleanTarget -TargetName "DesktopTarget" }
            "5" { Invoke-CleanTarget -TargetName "Generated" }
            "6" { Invoke-CleanTarget -TargetName "NodeModules" }
            "7" { Invoke-CleanTarget -TargetName "All" }
            "Q" { return }
            default { Write-Host "Unknown choice." -ForegroundColor Yellow }
        }
    }
}

if ($Target -eq "Menu") {
    Show-Menu
}
else {
    Invoke-CleanTarget -TargetName $Target
}
