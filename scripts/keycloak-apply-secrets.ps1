<#
scripts/keycloak-apply-secrets.ps1 - Apply secrets from .env to the running Keycloak
(realm Next-Step). Each one is applied only if it is set in .env:

  GOOGLE_CLIENT_SECRET (+ optional GOOGLE_CLIENT_ID) -> "Sign in with Google" identity provider
  SMTP_PASSWORD                                      -> email server (account linking, password reset)

Needed once for a realm that already exists: realm-export.json (which reads these
placeholders) is only imported when the realm is first created.

Usage (from the repository root, with the dev stack running):
  .\scripts\keycloak-apply-secrets.ps1
#>
# kcadm writes info messages to stderr; failures are detected via $LASTEXITCODE.
$ErrorActionPreference = 'Continue'
$root = Split-Path $PSScriptRoot -Parent

function Get-EnvValue([string]$name) {
    $line = Get-Content (Join-Path $root '.env') | Where-Object { $_ -match "^$name=" } | Select-Object -Last 1
    if (-not $line) { return '' }
    return ($line -replace "^$name=", '').Trim().Trim('"').Trim("'")
}

$googleSecret = Get-EnvValue 'GOOGLE_CLIENT_SECRET'
$googleClientId = Get-EnvValue 'GOOGLE_CLIENT_ID'
$smtpPassword = Get-EnvValue 'SMTP_PASSWORD'
if (-not $googleSecret -and -not $smtpPassword) {
    throw 'Nothing to apply: set GOOGLE_CLIENT_SECRET and/or SMTP_PASSWORD in .env (see .env.example).'
}

$compose = @('compose', '-f', (Join-Path $root 'docker-compose.yml'), '-f', (Join-Path $root 'docker-compose.dev.yml'))
$kcadm = $compose + @('exec', '-T', 'keycloak', '/opt/keycloak/bin/kcadm.sh')

& docker @kcadm config credentials --server http://localhost:8080 --realm master `
    --user (Get-EnvValue 'KEYCLOAK_ADMIN') --password (Get-EnvValue 'KEYCLOAK_ADMIN_PASSWORD')
if ($LASTEXITCODE -ne 0) { throw 'Could not log in to Keycloak admin (check KEYCLOAK_ADMIN / KEYCLOAK_ADMIN_PASSWORD).' }

if ($googleSecret) {
    $updateArgs = @('update', 'identity-provider/instances/google', '-r', 'Next-Step', '-s', "config.clientSecret=$googleSecret")
    if ($googleClientId) { $updateArgs += @('-s', "config.clientId=$googleClientId") }
    & docker @kcadm @updateArgs
    if ($LASTEXITCODE -ne 0) { throw 'Could not update the google identity provider.' }
    Write-Host '[OK] Google identity provider updated.'
}

if ($smtpPassword) {
    $smtp = ((& docker @kcadm get realms/Next-Step) | ConvertFrom-Json).smtpServer
    if (-not $smtp -or -not $smtp.host) { throw 'The realm has no SMTP server configured.' }
    # --merge keeps the other smtpServer settings (host, port, user, from...).
    & docker @kcadm update realms/Next-Step --merge -s "smtpServer.password=$smtpPassword"
    if ($LASTEXITCODE -ne 0) { throw 'Could not update the SMTP settings.' }
    Write-Host "[OK] SMTP password updated ($($smtp.user) via $($smtp.host))."
}
