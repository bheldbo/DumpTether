function Read-DumpTetherDotEnvFile {
    param([string] $Path)

    $values = @{}

    if (-not (Test-Path $Path)) {
        return $values
    }

    foreach ($line in Get-Content -Path $Path) {
        $trimmed = $line.Trim()

        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
            continue
        }

        $match = [regex]::Match($trimmed, "^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)\s*$")

        if (-not $match.Success) {
            continue
        }

        $key = $match.Groups[1].Value
        $value = Remove-DumpTetherInlineDotEnvComment $match.Groups[2].Value.Trim()

        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or
            ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        $values[$key] = $value
    }

    return $values
}

function Remove-DumpTetherInlineDotEnvComment {
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

function Set-DumpTetherProcessEnvironmentFromDotEnv {
    param([hashtable] $Values)

    foreach ($item in $Values.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($item.Key, $item.Value, "Process")
    }
}

function Get-DumpTetherEnvValue {
    param(
        [string] $Name,
        [string] $DefaultValue
    )

    $value = [Environment]::GetEnvironmentVariable($Name, "Process")

    if ([string]::IsNullOrWhiteSpace($value)) {
        return $DefaultValue
    }

    return $value
}

function Get-DumpTetherDockerCommand {
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

function Invoke-DumpTetherAtRepoRoot {
    param(
        [string] $RepoRoot,
        [scriptblock] $Command
    )

    Push-Location $RepoRoot
    try {
        & $Command
    }
    finally {
        Pop-Location
    }
}

Export-ModuleMember -Function `
    Read-DumpTetherDotEnvFile, `
    Set-DumpTetherProcessEnvironmentFromDotEnv, `
    Get-DumpTetherEnvValue, `
    Get-DumpTetherDockerCommand, `
    Invoke-DumpTetherAtRepoRoot
