# ==============================================================================
# Script: start-frontend.ps1
# Descripción: Inicia el Frontend Vite + Vue 3 (pnpm dev)
# ==============================================================================

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$FrontendDir = Join-Path $ScriptDir "RTecNM_V2_Frontend"

param (
    [switch]$Yes = $false
)

function Check-And-Confirm-Port([int]$port, [string]$serviceName) {
    $connections = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    if ($connections) {
        $pids = $connections | Select-Object -ExpandProperty OwningProcess -Unique
        foreach ($p in $pids) {
            if ($p -and $p -ne 0) {
                $proc = Get-Process -Id $p -ErrorAction SilentlyContinue
                if ($proc) {
                    Write-Host "`n⚠️  ALERTA: El puerto $port ($serviceName) ya está en uso por PID $($p) ($($proc.ProcessName))" -ForegroundColor Yellow
                    if (-not $Yes -and $Host.UI.RawUI) {
                        $ans = Read-Host "👉 ¿Deseas detener el proceso $($proc.ProcessName) (PID: $p) para continuar? [S/N]"
                        if ($ans -notmatch "^[sSyY]") {
                            Write-Host "❌ Operación cancelada por el usuario." -ForegroundColor Red
                            exit 1
                        }
                    }
                    Write-Host "   🔄 Liberando puerto $port (cerrando PID $($p))..." -ForegroundColor Yellow
                    Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
                    Start-Sleep -Milliseconds 500
                }
            }
        }
    }
}

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  🏛️  TecNM Residencias - Iniciando Frontend (Vite + Vue 3)" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Verificar Gestor de Paquetes (pnpm o npm)
Write-Host "`n🔍 Verificando gestor de paquetes (pnpm / npm)..." -ForegroundColor Yellow
$pkgManager = ""
if (Get-Command pnpm -ErrorAction SilentlyContinue) {
    $pnpmVersion = pnpm -v
    Write-Host "   ✅ pnpm detectado: v$pnpmVersion" -ForegroundColor Green
    $pkgManager = "pnpm"
} elseif (Get-Command npm -ErrorAction SilentlyContinue) {
    $npmVersion = npm -v
    Write-Host "   ✅ npm detectado: v$npmVersion (usando npm)" -ForegroundColor Green
    $pkgManager = "npm"
} else {
    Write-Host "   ❌ ERROR: Ni pnpm ni npm están instalados o disponibles en el PATH." -ForegroundColor Red
    exit 1
}

# 2. Verificar puerto 5085 ocupado
Check-And-Confirm-Port 5085 "Frontend Vite Vue 3"

# 3. Iniciar Frontend Vite
Write-Host "`n🚀 Iniciando Frontend Vite en http://localhost:5085..." -ForegroundColor Green
Write-Host "   🌐 Acceso al sistema: http://localhost:5085/auth/login" -ForegroundColor DarkCyan
Write-Host "   🔗 Conectado a Backend: http://localhost:5185`n" -ForegroundColor DarkGray

if (-not (Test-Path $FrontendDir)) {
    Write-Host "❌ ERROR: No se encontró la carpeta $FrontendDir" -ForegroundColor Red
    exit 1
}

Set-Location $FrontendDir

if (-not (Test-Path "node_modules")) {
    Write-Host "   📦 Instalando dependencias del Frontend ($pkgManager install)..." -ForegroundColor Yellow
    if ($pkgManager -eq "pnpm") {
        pnpm install
    } else {
        npm install
    }
}

if ($pkgManager -eq "pnpm") {
    pnpm dev
} else {
    npm run dev
}
