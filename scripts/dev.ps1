<#
scripts/dev.ps1 - Lance tout le stack NextStep en local (sans Docker).

  - Agents FastAPI   : http://localhost:8000  (docs /docs, health /health)
  - Backend  .NET    : http://localhost:5000  (swagger /swagger)
  - Frontend Angular : http://localhost:4200

Prérequis :
  - PostgreSQL demarré sur localhost:5432 (base nextstep_db, user postgres, mdp said1234)
  - Node / npm installés (frontend)
  - .NET SDK installé (backend)

Chaque service s'ouvre dans sa propre fenêtre PowerShell.
Ferme les 3 fenêtres pour tout arrêter.
#>

param(
    [switch]$NoReload,         # agents sans --reload
    [string]$PythonPath,       # chemin du python à utiliser pour le venv (auto sinon)
    [switch]$NoKillPorts,      # désactiver la libération automatique des ports
    [switch]$KillOnly          # libérer uniquement les ports sans relancer les services
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
if (-not $root) { $root = (Get-Item .).FullName }
$backendDir  = Join-Path $root 'backend'
$frontendDir = Join-Path $root 'frontend'
$agentsDir   = Join-Path $root 'agents'

function Free-Port {
    param([int[]]$Ports)
    foreach ($port in $Ports) {
        # Protection stricte pour la base de données
        if ($port -eq 5432) {
            Write-Host "[!] Port 5432 (PostgreSQL) protege - ignore." -ForegroundColor Cyan
            continue
        }

        $pidsToKill = [System.Collections.Generic.HashSet[int]]::new()

        # 1. Libération via Get-NetTCPConnection
        try {
            $conns = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
            if ($conns) {
                foreach ($c in $conns) {
                    $pidNum = [int]$c.OwningProcess
                    if ($pidNum -gt 0 -and $pidNum -ne $PID) {
                        $null = $pidsToKill.Add($pidNum)
                    }
                }
            }
        } catch { }

        # 2. Securite de secours via netstat
        try {
            $netstatMatches = netstat -ano | Select-String ":$port\s+"
            foreach ($match in $netstatMatches) {
                $parts = ($match.ToString().Trim() -split '\s+')
                if ($parts.Count -ge 5) {
                    $pidStr = $parts[-1]
                    if ($pidStr -match '^\d+$') {
                        $pidNum = [int]$pidStr
                        if ($pidNum -gt 0 -and $pidNum -ne $PID) {
                            $null = $pidsToKill.Add($pidNum)
                        }
                    }
                }
            }
        } catch { }

        if ($pidsToKill.Count -gt 0) {
            foreach ($p in $pidsToKill) {
                $proc = Get-Process -Id $p -ErrorAction SilentlyContinue
                $procName = if ($proc) { $proc.ProcessName } else { "PID $p" }
                Write-Host "[!] Port $port occupe par $procName (PID $p) - Arret force (process tree)..." -ForegroundColor Yellow
                
                # Arrêt de l'arbre de processus (force + sous-processus node/python/etc.)
                taskkill /F /T /PID $p 2>$null | Out-Null
                Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
            }

            # Attente active de la libération du port par l'OS
            $freed = $false
            for ($attempt = 0; $attempt -lt 12; $attempt++) {
                Start-Sleep -Milliseconds 250
                $stillBusy = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
                if (-not $stillBusy) {
                    $freed = $true
                    break
                }
            }
            if ($freed) {
                Write-Host "[+] Port $port libere avec succes" -ForegroundColor Green
            } else {
                Write-Host "[!] Port $port : processus arrete, socket en cours de relachement OS" -ForegroundColor Yellow
            }
        } else {
            Write-Host "[ok] Port $port libre" -ForegroundColor DarkGray
        }
    }
}

function Get-PythonExe {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Python\Python310\python.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Python\Python313\python.exe'),
        'python'
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }
    throw 'Python introuvable. Passe le chemin via -PythonPath.'
}

function Start-AppWindow {
    param([string]$Title, [string]$WorkDir, [string]$Command)
    $code = "`$host.UI.RawUI.WindowTitle = '$Title'; Set-Location -LiteralPath '$WorkDir'; " + $Command
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($code))
    Start-Process powershell -ArgumentList @('-NoLogo', '-NoExit', '-EncodedCommand', $encoded) -WorkingDirectory $WorkDir | Out-Null
    Write-Host "[+] $Title lance" -ForegroundColor Green
}

# --- 1. Libération systématique des ports applicatifs ---
if (-not $NoKillPorts) {
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host "  Liberation des ports NextStep (4200, 5000, 5001, 8000)  " -ForegroundColor Cyan
    Write-Host "==========================================================" -ForegroundColor Cyan
    Free-Port -Ports @(4200, 5000, 5001, 8000)
    Write-Host ""
}

if ($KillOnly) {
    Write-Host "[i] Option -KillOnly specifiee. Ports nettoyes, arret du script." -ForegroundColor Cyan
    return
}

if (-not $PythonPath) { $PythonPath = Get-PythonExe }
Write-Host "[i] Python : $PythonPath" -ForegroundColor Cyan

# --- 2. Verification des prérequis ---
if (Test-NetConnection -ComputerName localhost -Port 5432 -WarningAction SilentlyContinue -InformationLevel Quiet) {
    Write-Host '[+] PostgreSQL OK sur localhost:5432' -ForegroundColor Green
} else {
    Write-Warning 'PostgreSQL ne repond pas sur localhost:5432 - demarre-le avant de tester.'
}

if (-not (Test-Path (Join-Path $frontendDir 'node_modules'))) {
    Write-Host '[-] node_modules absent - npm install...' -ForegroundColor Yellow
    Push-Location $frontendDir
    npm install --legacy-peer-deps
    Pop-Location
}

# --- 3. Agents : venv + dependances (premiere fois) ---
$venvPython = Join-Path $agentsDir '.venv\Scripts\python.exe'
if (-not (Test-Path $venvPython)) {
    Write-Host '[-] venv agents absent - creation + installation des dependances...' -ForegroundColor Yellow
    & $PythonPath -m venv (Join-Path $agentsDir '.venv')
    & $venvPython -m pip install -r (Join-Path $agentsDir 'requirements.txt')
}

# --- 4. Lancement ordonné de la stack ---
Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Demarrage des services NextStep                         " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$reloadFlag = if ($NoReload) { '' } else { ' --reload' }
$agentsCmd = '$env:DATABASE_URL="postgresql+asyncpg://postgres:said1234@localhost:5432/nextstep_db"; ' +
             "& `"$venvPython`" -m uvicorn main:app --host 0.0.0.0 --port 8000$reloadFlag"
Start-AppWindow -Title 'NextStep - Agents (8000)' -WorkDir $agentsDir -Command $agentsCmd

$backendCmd = '$env:ASPNETCORE_URLS="http://localhost:5000"; $env:PythonAgents__Url="http://localhost:8000"; dotnet run --urls "http://localhost:5000"'
Start-AppWindow -Title 'NextStep - Backend (5000)' -WorkDir $backendDir -Command $backendCmd

$frontendCmd = 'npm start'
Start-AppWindow -Title 'NextStep - Frontend (4200)' -WorkDir $frontendDir -Command $frontendCmd

Write-Host ''
Write-Host 'Stack lancee avec succes :' -ForegroundColor Cyan
Write-Host '  Agents FastAPI   : http://localhost:8000  (docs: /docs, health: /health)' -ForegroundColor Green
Write-Host '  Backend .NET     : http://localhost:5000  (swagger: /swagger)' -ForegroundColor Green
Write-Host '  Frontend Angular : http://localhost:4200' -ForegroundColor Green
Write-Host '==========================================================' -ForegroundColor Cyan