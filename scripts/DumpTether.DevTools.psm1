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

function Read-DumpTetherDotEnvFiles {
    param([string[]] $Paths)

    $values = @{}

    foreach ($path in $Paths) {
        $fileValues = Read-DumpTetherDotEnvFile -Path $path

        foreach ($item in $fileValues.GetEnumerator()) {
            $values[$item.Key] = $item.Value
        }
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

function Set-DumpTetherAspNetConfigurationAliases {
    $aliases = @{
        "DUMPTETHER_APPLY_MIGRATIONS_ON_STARTUP" = "Database__ApplyMigrationsOnStartup"
        "DUMPTETHER_DATABASE_PROVIDER" = "Database__Provider"
        "DUMPTETHER_SQLITE_PATH" = "Database__Sqlite__Path"
        "DUMPTETHER_REQUIRE_AUTHENTICATION" = "Auth__RequireAuthentication"
        "DUMPTETHER_ALLOW_GUEST_SESSIONS" = "Auth__AllowGuestSessions"
        "DUMPTETHER_SIGNUP_MODE" = "Auth__SignupMode"
        "DUMPTETHER_SIGNUP_WHITELIST_EMAIL_0" = "Auth__SignupWhitelistEmails__0"
        "DUMPTETHER_SIGNUP_WHITELIST_DOMAIN_0" = "Auth__SignupWhitelistDomains__0"
        "DUMPTETHER_SIGNUP_INVITE_CODE_0" = "Auth__SignupInviteCodes__0"
        "DUMPTETHER_ENABLE_DEVELOPMENT_LOGIN" = "Auth__EnableDevelopmentLogin"
        "DUMPTETHER_ENABLE_LOCAL_DESKTOP_LOGIN" = "Auth__EnableLocalDesktopLogin"
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
        "DUMPTETHER_PASSWORD_RECOVERY_ENABLED" = "PasswordRecovery__Enabled"
        "DUMPTETHER_PASSWORD_RECOVERY_PUBLIC_BASE_URL" = "PasswordRecovery__PublicBaseUrl"
        "DUMPTETHER_PASSWORD_RECOVERY_TOKEN_HOURS" = "PasswordRecovery__TokenHours"
        "DUMPTETHER_ACCOUNT_DELETION_ENABLED" = "AccountDeletion__Enabled"
        "DUMPTETHER_ACCOUNT_DELETION_GRACE_HOURS" = "AccountDeletion__GraceHours"
        "DUMPTETHER_ACCOUNT_DELETION_REMINDER_HOURS_BEFORE" = "AccountDeletion__ReminderHoursBefore"
        "DUMPTETHER_ACCOUNT_DELETION_SWEEP_INTERVAL_MINUTES" = "AccountDeletion__SweepIntervalMinutes"
        "DUMPTETHER_NOTIFICATIONS_ENABLED" = "Notifications__Enabled"
        "DUMPTETHER_NOTIFICATIONS_SWEEP_INTERVAL_MINUTES" = "Notifications__SweepIntervalMinutes"
        "DUMPTETHER_NOTIFICATIONS_DAILY_DIGEST_HOUR_UTC" = "Notifications__DailyDigestHourUtc"
        "DUMPTETHER_NOTIFICATIONS_FOLLOW_UP_WINDOW_HOURS" = "Notifications__FollowUpWindowHours"
        "DUMPTETHER_EMAIL_FROM" = "Email__FromEmail"
        "DUMPTETHER_EMAIL_FROM_NAME" = "Email__FromName"
        "DUMPTETHER_EMAIL_PROVIDER" = "Email__Provider"
        "DUMPTETHER_EMAIL_SMTP_HOST" = "Email__Smtp__Host"
        "DUMPTETHER_EMAIL_SMTP_PORT" = "Email__Smtp__Port"
        "DUMPTETHER_EMAIL_SMTP_USE_AUTHENTICATION" = "Email__Smtp__UseAuthentication"
        "DUMPTETHER_EMAIL_SMTP_ENABLE_SSL" = "Email__Smtp__EnableSsl"
        "DUMPTETHER_EMAIL_SMTP_USERNAME" = "Email__Smtp__Username"
        "DUMPTETHER_EMAIL_SMTP_PASSWORD" = "Email__Smtp__Password"
        "DUMPTETHER_EMAIL_BREVO_API_KEY" = "Email__BrevoApi__ApiKey"
        "DUMPTETHER_EMAIL_MFA_ENABLED" = "Mfa__Email__Enabled"
        "DUMPTETHER_OAUTH_MICROSOFT_ENABLED" = "OAuth__Microsoft__Enabled"
        "DUMPTETHER_OAUTH_MICROSOFT_CLIENT_ID" = "OAuth__Microsoft__ClientId"
        "DUMPTETHER_OAUTH_MICROSOFT_CLIENT_SECRET" = "OAuth__Microsoft__ClientSecret"
        "DUMPTETHER_OAUTH_MICROSOFT_TENANT_ID" = "OAuth__Microsoft__TenantId"
        "DUMPTETHER_LEGAL_REQUIRE_ACCEPTANCE" = "Legal__RequireAcceptance"
        "DUMPTETHER_LEGAL_TERMS_VERSION" = "Legal__TermsVersion"
        "DUMPTETHER_LEGAL_PRIVACY_NOTICE_VERSION" = "Legal__PrivacyNoticeVersion"
        "DUMPTETHER_LEGAL_OPERATOR_NAME" = "Legal__OperatorName"
        "DUMPTETHER_LEGAL_PRIVACY_CONTACT_EMAIL" = "Legal__PrivacyContactEmail"
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
    Read-DumpTetherDotEnvFiles, `
    Set-DumpTetherProcessEnvironmentFromDotEnv, `
    Get-DumpTetherEnvValue, `
    Set-DumpTetherAspNetConfigurationAliases, `
    Get-DumpTetherDockerCommand, `
    Invoke-DumpTetherAtRepoRoot
