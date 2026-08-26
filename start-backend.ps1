# ==============================================================================
# Script: start-backend.ps1
# Descripción: Inicia la base de datos PostgreSQL (Docker) y el Backend API (.NET 10)
# ==============================================================================

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BackendDir = Join-Path $ScriptDir "RTecNM_V2_Backend"

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
Write-Host "  🏛️  TecNM Residencias - Iniciando Backend API" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Verificar .NET SDK
Write-Host "`n🔍 Verificando .NET SDK..." -ForegroundColor Yellow
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetCmd) {
    $dotnetVersion = dotnet --version
    Write-Host "   ✅ .NET SDK detectado: v$dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "   ❌ ERROR: .NET SDK no está instalado o no se encuentra en el PATH." -ForegroundColor Red
    exit 1
}

# 2. Verificar/Iniciar PostgreSQL con Docker Compose
Write-Host "`n🐘 Verificando Base de Datos PostgreSQL..." -ForegroundColor Yellow
$dockerCmd = Get-Command docker -ErrorAction SilentlyContinue

if ($dockerCmd) {
    $pgRunning = docker ps --filter "name=residencia-v2-db" --filter "status=running" --format "{{.Names}}" 2>$null
    if (-not $pgRunning) {
        Write-Host "   ⚙️  Levantando contenedor PostgreSQL (residencia-v2-db)..." -ForegroundColor Cyan
        Push-Location $ScriptDir
        if (docker compose version 2>$null) {
            docker compose up -d postgres
        } else {
            docker-compose up -d postgres
        }
        Pop-Location
        Write-Host "   ✅ Contenedor PostgreSQL iniciado correctamente." -ForegroundColor Green
    } else {
        Write-Host "   ✅ Contenedor PostgreSQL ya se encuentra en ejecución." -ForegroundColor Green
    }
} else {
    Write-Host "   ⚠️  Docker no detectado. Asegúrate de tener PostgreSQL corriendo localmente en el puerto 5439." -ForegroundColor Yellow
}

# 2. Verificar disponibilidad de puertos
Check-And-Confirm-Port 5439 "PostgreSQL Base de Datos"
Check-And-Confirm-Port 5185 "Backend API .NET"

# 3. Verificar/Iniciar PostgreSQL con Docker Compose
Write-Host "`n🐘 Verificando Base de Datos PostgreSQL..." -ForegroundColor Yellow
Write-Host "`n🚀 Iniciando Backend Web API en http://localhost:5185..." -ForegroundColor Green
Write-Host "   📖 Swagger / OpenAPI disponible en: http://localhost:5185/swagger`n" -ForegroundColor DarkCyan

if (-not (Test-Path $BackendDir)) {
    Write-Host "❌ ERROR: No se encontró la carpeta $BackendDir" -ForegroundColor Red
    exit 1
}

Set-Location $BackendDir
dotnet run --launch-profile http
