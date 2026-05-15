# Script de Build et Push Docker pour NextStep
# Situé dans infra/scripts/build-and-deploy.ps1

param (
    [string]$Version = (Get-Date -Format "yyyyMMdd.HHmm")
)

# 1. Charger les variables d'environnement (Remonte d'un cran vers infra/local/.env.prod)
$envPath = Join-Path $PSScriptRoot "../local/.env.prod"
if (Test-Path $envPath) {
    Get-Content $envPath | Where-Object { $_ -match '=' -and $_ -notmatch '^#' } | ForEach-Object {
        $name, $value = $_.Split('=', 2)
        Set-Variable -Name "ENV_$name" -Value $value.Trim()
    }
} else {
    Write-Error "Erreur: Fichier $envPath introuvable."
    exit
}

$DockerUser = $ENV_DOCKER_HUB_USER
if (-not $DockerUser) {
    Write-Error "Erreur: DOCKER_HUB_USER non défini dans $envPath."
    exit
}

# Les dossiers sources sont deux crans plus haut (NextStep/frontend, etc.)
$rootPath = Join-Path $PSScriptRoot "../../"

$Images = @(
    @{ Name = "nextstep-frontend"; Path = "frontend" },
    @{ Name = "nextstep-backend";  Path = "backend" },
    @{ Name = "nextstep-agents";   Path = "agents" }
)

Write-Host "🚀 Démarrage du Build & Push (PROD MODE) - Version $Version..." -ForegroundColor Cyan

foreach ($Image in $Images) {
    $ImageName = "$DockerUser/$($Image.Name)"
    $SourcePath = Join-Path $rootPath $Image.Path

    Write-Host "---"
    Write-Host "Building image: $ImageName..." -ForegroundColor Yellow
    
    # On force le build de l'étape "production" (Ultra-Light)
    docker build --target production `
                 -t "${ImageName}:latest" `
                 -t "${ImageName}:${Version}" `
                 "$SourcePath"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Erreur lors du build de $($Image.Name)"
        exit
    }

    Write-Host "Pushing to Docker Hub (Tags: latest, $Version)..." -ForegroundColor Green
    docker push "${ImageName}:latest"
    docker push "${ImageName}:${Version}"
}

Write-Host "✅ Terminé ! Tes images versionnées sont sur Docker Hub." -ForegroundColor Cyan
