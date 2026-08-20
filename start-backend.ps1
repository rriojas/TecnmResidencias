# ==============================================================================
# Script: start-backend.ps1
# Descripción: Inicia la base de datos PostgreSQL (Docker) y el Backend API (.NET 10)
# ==============================================================================

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BackendDir = Join-Path $ScriptDir "RTecNM_V2_Backend"

# Función para liberar puerto si ya está ocupado
function Free-Port([int]$port) {
    try {
        $connections = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
        if ($connections) {
            $pids = $connections | Select-Object -ExpandProperty OwningProcess -Unique
            foreach ($p in $pids) {
                if ($p -and $p -ne 0) {
                    $proc = Get-Process -Id $p -ErrorAction SilentlyContinue
                    if ($proc) {
                        Write-Host "   🔄 Liberando puerto $port (cerrando proceso previo PID $($p) - $($proc.ProcessName))..." -ForegroundColor Yellow
                        Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
                    }
                }
            }
            Start-Sleep -Milliseconds 500
        }
    } catch {
        # Continuar si no se pudo consultar
    }
}

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  🏛️  TecNM Residencias - Iniciando Backend API" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Verificar .NET SDK
Write-Host "`n🔍 Verificando .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "   ✅ .NET SDK detectado: v$dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "   ❌ ERROR: .NET SDK no está instalado o no se encuentra en el PATH." -ForegroundColor Red
    exit 1
}

# 2. Verificar/Iniciar PostgreSQL con Docker Compose
Write-Host "`n🐘 Verificando Base de Datos PostgreSQL..." -ForegroundColor Yellow
$dockerCmd = Get-Command docker -ErrorAction SilentlyContinue

if ($dockerCmd) {
    try {
        # Intentar verificar si el contenedor de postgres ya está corriendo
        $pgRunning = docker ps --filter "name=residencia-v2-db" --filter "status=running" --format "{{.Names}}"
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
    } catch {
        Write-Host "   ⚠️  Aviso: No se pudo verificar/iniciar Docker automáticamente. Si usas PostgreSQL local, asegúrate de que esté activo en el puerto 5432." -ForegroundColor Yellow
    }
} else {
    Write-Host "   ⚠️  Docker no detectado. Asegúrate de tener PostgreSQL corriendo localmente en el puerto 5432." -ForegroundColor Yellow
}

# 3. Verificar y liberar puerto 5144 si está ocupado
Free-Port 5144

# 4. Iniciar Backend
Write-Host "`n🚀 Iniciando Backend Web API en http://localhost:5144..." -ForegroundColor Green
Write-Host "   📖 Swagger / OpenAPI disponible en: http://localhost:5144/swagger`n" -ForegroundColor DarkCyan

if (-not (Test-Path $BackendDir)) {
    Write-Host "❌ ERROR: No se encontró la carpeta $BackendDir" -ForegroundColor Red
    exit 1
}

Set-Location $BackendDir
dotnet run --launch-profile http
