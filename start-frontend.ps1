# ==============================================================================
# Script: start-frontend.ps1
# Descripción: Inicia el Frontend Razor Pages (.NET 10)
# ==============================================================================

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$FrontendDir = Join-Path $ScriptDir "RTecNM_V2_Frontend"

# Función para liberar puerto si ya está ocupado
function Free-Port([int]$port) {
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
}

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  🏛️  TecNM Residencias - Iniciando Frontend UI" -ForegroundColor Cyan
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

# 2. Verificar y liberar puerto 5000 si está ocupado
Free-Port 5000

# 3. Iniciar Frontend
Write-Host "`n🚀 Iniciando Frontend Razor Pages en http://localhost:5000..." -ForegroundColor Green
Write-Host "   🌐 Acceso al sistema: http://localhost:5000/auth/login" -ForegroundColor DarkCyan
Write-Host "   🔗 Conectado a Backend: http://localhost:5144`n" -ForegroundColor DarkGray

if (-not (Test-Path $FrontendDir)) {
    Write-Host "❌ ERROR: No se encontró la carpeta $FrontendDir" -ForegroundColor Red
    exit 1
}

Set-Location $FrontendDir
dotnet run --launch-profile http
