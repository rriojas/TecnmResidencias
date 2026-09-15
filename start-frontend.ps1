# ==============================================================================
# Script: start-frontend.ps1
# Descripción: Valida herramientas/paquetes e inicia el Frontend Vite + Vue 3
# ==============================================================================

param (
    [switch]$Yes = $false
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$FrontendDir = Join-Path $ScriptDir "RTecNM_V2_Frontend"


function Prompt-User([string]$question, [bool]$defaultYes = $true) {
    if ($Yes -or (-not $Host.UI.RawUI)) {
        return $true
    }
    $suffix = if ($defaultYes) { "[S/n]" } else { "[s/N]" }
    $ans = Read-Host "$question $suffix"
    if ([string]::IsNullOrWhiteSpace($ans)) {
        return $defaultYes
    }
    return ($ans -match "^[sSyY]")
}

function Refresh-Path() {
    $machinePath = [System.Environment]::GetEnvironmentVariable("Path", "Machine")
    $userPath = [System.Environment]::GetEnvironmentVariable("Path", "User")
    $env:Path = "$machinePath;$userPath"
}

function Check-And-Confirm-Port([int]$port, [string]$serviceName) {
    $connections = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    if ($connections) {
        $pids = $connections | Select-Object -ExpandProperty OwningProcess -Unique
        foreach ($p in $pids) {
            if ($p -and $p -ne 0) {
                $proc = Get-Process -Id $p -ErrorAction SilentlyContinue
                if ($proc) {
                    Write-Host "`n⚠️  ALERTA: El puerto $port ($serviceName) ya está en uso por PID $($p) ($($proc.ProcessName))" -ForegroundColor Yellow
                    if (Prompt-User "👉 ¿Deseas detener el proceso $($proc.ProcessName) (PID: $p) para liberar el puerto?" $false) {
                        Write-Host "   🔄 Liberando puerto $port (cerrando PID $($p))..." -ForegroundColor Yellow
                        Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
                        Start-Sleep -Milliseconds 500
                    } else {
                        Write-Host "❌ Operación cancelada por el usuario o puerto ocupado." -ForegroundColor Red
                        exit 1
                    }
                }
            }
        }
    }
}

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  🏛️  TecNM Residencias - Iniciando Frontend (Vite + Vue 3)" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Verificar Node.js
Write-Host "`n🔍 Verificando Node.js..." -ForegroundColor Yellow
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Host "   ⚠️  Node.js no está instalado o no se encuentra en el PATH." -ForegroundColor Yellow
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        if (Prompt-User "👉 ¿Deseas descargar e instalar Node.js LTS mediante winget automáticamente?" $true) {
            Write-Host "   ⬇️  Instalando Node.js LTS vía winget..." -ForegroundColor Cyan
            winget install OpenJS.NodeJS.LTS --accept-package-agreements --accept-source-agreements
            Refresh-Path
        } else {
            Write-Host "❌ Node.js es requerido para el Frontend. Por favor instálalo manualmente." -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "❌ ERROR: Node.js no está disponible y winget no fue encontrado." -ForegroundColor Red
        exit 1
    }
}

if (Get-Command node -ErrorAction SilentlyContinue) {
    $nodeVer = node -v
    Write-Host "   ✅ Node.js detectado: $nodeVer" -ForegroundColor Green
} else {
    Write-Host "❌ ERROR: Node.js aún no está disponible en la sesión actual. Reinicia la terminal tras instalarlo." -ForegroundColor Red
    exit 1
}

# 2. Verificar Gestor de Paquetes (pnpm preferido, npm fallback)
Write-Host "`n🔍 Verificando gestor de paquetes (pnpm / npm)..." -ForegroundColor Yellow
$pkgManager = ""

if (Get-Command pnpm -ErrorAction SilentlyContinue) {
    $pnpmVersion = pnpm -v
    Write-Host "   ✅ pnpm detectado: v$pnpmVersion" -ForegroundColor Green
    $pkgManager = "pnpm"
} else {
    Write-Host "   ⚠️  pnpm no está instalado (el proyecto incluye pnpm-lock.yaml)." -ForegroundColor Yellow
    if (Get-Command npm -ErrorAction SilentlyContinue) {
        if (Prompt-User "👉 ¿Deseas instalar pnpm globalmente ejecutando 'npm install -g pnpm'?" $true) {
            Write-Host "   ⬇️  Instalando pnpm globalmente..." -ForegroundColor Cyan
            npm install -g pnpm
            Refresh-Path
            if (Get-Command pnpm -ErrorAction SilentlyContinue) {
                Write-Host "   ✅ pnpm instalado correctamente." -ForegroundColor Green
                $pkgManager = "pnpm"
            } else {
                Write-Host "   ⚠️  No se detectó pnpm en el PATH actual. Se usará npm como alternativa." -ForegroundColor Yellow
                $pkgManager = "npm"
            }
        } else {
            Write-Host "   ℹ️  Usando npm como gestor de paquetes." -ForegroundColor Cyan
            $pkgManager = "npm"
        }
    } else {
        Write-Host "❌ ERROR: Ni pnpm ni npm están disponibles." -ForegroundColor Red
        exit 1
    }
}

# 3. Validar directorio del Frontend
if (-not (Test-Path $FrontendDir)) {
    Write-Host "❌ ERROR: No se encontró la carpeta $FrontendDir" -ForegroundColor Red
    exit 1
}

Set-Location $FrontendDir

# 4. Verificar dependencias (node_modules)
$nodeModulesPath = Join-Path $FrontendDir "node_modules"
if (-not (Test-Path $nodeModulesPath)) {
    Write-Host "`n📦 Paquetes de Frontend no encontrados (falta node_modules)." -ForegroundColor Yellow
    if (Prompt-User "👉 ¿Deseas descargar e instalar todas las dependencias ahora con '$pkgManager install'?" $true) {
        Write-Host "   ⬇️  Descargando e instalando paquetes del Frontend ($pkgManager install)..." -ForegroundColor Cyan
        if ($pkgManager -eq "pnpm") {
            pnpm install
        } else {
            npm install
        }
        Write-Host "   ✅ Dependencias instaladas correctamente." -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  Dependencias omitidas. El frontend podría fallar al iniciar." -ForegroundColor Yellow
        if (-not (Prompt-User "👉 ¿Deseas intentar arrancar sin instalar paquetes?" $false)) {
            Write-Host "🛑 Operación cancelada por el usuario." -ForegroundColor Yellow
            exit 0
        }
    }
} else {
    Write-Host "   ✅ Dependencias (node_modules) detectadas." -ForegroundColor Green
}

# 5. Verificar puerto 5085 ocupado
Check-And-Confirm-Port 5085 "Frontend Vite Vue 3"

# 6. Iniciar Frontend Vite
Write-Host "`n🚀 Iniciando Frontend Vite en http://localhost:5085..." -ForegroundColor Green
Write-Host "   🌐 Acceso al sistema: http://localhost:5085/auth/login" -ForegroundColor DarkCyan
Write-Host "   🔗 Conectado a Backend: http://localhost:5185`n" -ForegroundColor DarkGray

if ($pkgManager -eq "pnpm") {
    pnpm dev
} else {
    npm run dev
}
