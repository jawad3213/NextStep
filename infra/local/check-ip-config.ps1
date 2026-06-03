param(
    [string]$EnvFile = "infra/local/.env.prod",
    [string]$InventoryFile = "infra/local/ansible/inventory.ini",
    [string]$VagrantFile = "infra/local/Vagrantfile"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-IpFromPattern {
    param([string]$Text, [string]$Pattern)
    $m = [regex]::Match($Text, $Pattern)
    if ($m.Success) { return $m.Groups[1].Value }
    return $null
}

$vagrantRaw = Get-Content -Raw $VagrantFile
$inventoryRaw = Get-Content -Raw $InventoryFile
$envRaw = if (Test-Path $EnvFile) { Get-Content -Raw $EnvFile } else { "" }

$vagrantVm1 = Get-IpFromPattern $vagrantRaw 'vm1\.vm\.network\s+"private_network",\s+ip:\s+"([^"]+)"'
$vagrantVm2 = Get-IpFromPattern $vagrantRaw 'vm2\.vm\.network\s+"private_network",\s+ip:\s+"([^"]+)"'
$invVm1 = Get-IpFromPattern $inventoryRaw 'vm1\s+ansible_host=([0-9\.]+)'
$invVm2 = Get-IpFromPattern $inventoryRaw 'vm2\s+ansible_host=([0-9\.]+)'
$envVm1 = Get-IpFromPattern $envRaw 'VM1_IP=([0-9\.]+)'
$envVm2 = Get-IpFromPattern $envRaw 'VM2_IP=([0-9\.]+)'

$hostIps = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike "127.*" } |
    Select-Object -ExpandProperty IPAddress -Unique)

Write-Host "=== NextStep IP Consistency Check ==="
Write-Host "Vagrant VM1 : $vagrantVm1"
Write-Host "Vagrant VM2 : $vagrantVm2"
Write-Host "Inventory VM1: $invVm1"
Write-Host "Inventory VM2: $invVm2"
Write-Host "Env VM1      : $envVm1"
Write-Host "Env VM2      : $envVm2"
Write-Host ""
Write-Host "Host IPv4 adapters:"
$hostIps | ForEach-Object { Write-Host " - $_" }
Write-Host ""

$ok = $true
if ($vagrantVm1 -ne $invVm1 -or $vagrantVm2 -ne $invVm2) {
    Write-Host "Mismatch: Vagrant and Ansible inventory are not aligned." -ForegroundColor Yellow
    $ok = $false
}

if ($envVm1 -and $envVm2) {
    if ($envVm1 -ne $vagrantVm1 -or $envVm2 -ne $vagrantVm2) {
        Write-Host "Mismatch: .env VM IPs differ from Vagrant IPs." -ForegroundColor Yellow
        $ok = $false
    }
}

if ($ok) {
    Write-Host "OK: IP configuration is aligned across Vagrant, Ansible, and env." -ForegroundColor Green
} else {
    Write-Host "Action required: unify VM IP subnet in Vagrantfile + inventory.ini + .env.prod." -ForegroundColor Red
}
