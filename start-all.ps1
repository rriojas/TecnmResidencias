# ==============================================================================
# Script: start-all.ps1
# Descripción: Inicia Backend y Frontend utilizando start-backend.ps1 y start-frontend.ps1
# ==============================================================================

param (
    [switch]$Inline = $false,
    [switch]$Yes = $false
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BackendScript = Join-Path $ScriptDir "start-backend.ps1"
$FrontendScript = Join-Path $ScriptDir "start-frontend.ps1"
$GitHooksScript = Join-Path $ScriptDir "setup-git-hooks.ps1"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  🏛️  TecNM Residencias - Iniciando Stack Completo" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 0. Asegurar protección de skills-lock.json en Git
if (Test-Path $GitHooksScript) {
    & $GitHooksScript
}

if (-not (Test-Path $BackendScript)) {
    Write-Host "❌ ERROR: No se encontró $BackendScript" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $FrontendScript)) {
    Write-Host "❌ ERROR: No se encontró $FrontendScript" -ForegroundColor Red
    exit 1
}

if (-not $Yes -and $Host.UI.RawUI) {
    Write-Host "`n📋 Resumen del Entorno de Desarrollo a Iniciar:" -ForegroundColor Cyan
    Write-Host "   🐘 PostgreSQL (Docker)  : BD: postgre_recidencias (Puerto: 5439)" -ForegroundColor White
    Write-Host "   ⚙️  Backend API (.NET)   : http://localhost:5185" -ForegroundColor White
    Write-Host "   🌐 Frontend SPA (Vite)  : http://localhost:5085`n" -ForegroundColor White
    
    $ans = Read-Host "👉 ¿Deseas verificar dependencias e iniciar el Stack Completo? [S/N]"
    if ($ans -notmatch "^[sSyY]") {
        Write-Host "🛑 Inicio cancelado por el usuario." -ForegroundColor Yellow
        exit 0
    }
}

$psExe = if (Get-Command pwsh -ErrorAction SilentlyContinue) { "pwsh" } else { "powershell" }
$yesFlag = if ($Yes) { "-Yes" } else { "" }

if ($Inline) {
    Write-Host "`n🚀 Iniciando servicios en segundo plano en esta misma terminal..." -ForegroundColor Yellow
    Write-Host "   (Presiona Ctrl+C para detener ambos servicios)`n" -ForegroundColor DarkGray

    $backendArgs = if ($yesFlag) { @{ FilePath = $BackendScript; ArgumentList = @("-Yes") } } else { @{ FilePath = $BackendScript } }
    $frontendArgs = if ($yesFlag) { @{ FilePath = $FrontendScript; ArgumentList = @("-Yes") } } else { @{ FilePath = $FrontendScript } }

    $backendJob = Start-Job @backendArgs
    Start-Sleep -Seconds 3
    $frontendJob = Start-Job @frontendArgs

    try {
        while ($true) {
            Receive-Job -Job $backendJob | Write-Host
            Receive-Job -Job $frontendJob | Write-Host
            Start-Sleep -Milliseconds 500
        }
    } finally {
        Write-Host "`n🛑 Deteniendo Backend y Frontend..." -ForegroundColor Yellow
        Stop-Job -Job $backendJob, $frontendJob -ErrorAction SilentlyContinue
        Remove-Job -Job $backendJob, $frontendJob -ErrorAction SilentlyContinue
        Write-Host "✅ Servicios detenidos." -ForegroundColor Green
    }
} else {
    Write-Host "`n🚀 Levantando Backend y Frontend en ventanas independientes..." -ForegroundColor Yellow

    # 1. Iniciar Backend usando start-backend.ps1
    Write-Host "   ⚙️  Ejecutando start-backend.ps1..." -ForegroundColor Cyan
    $bArgs = @("-NoExit", "-ExecutionPolicy", "Bypass", "-File", "`"$BackendScript`"")
    if ($Yes) { $bArgs += "-Yes" }
    Start-Process $psExe -ArgumentList $bArgs

    # Esperar 3 segundos para que PostgreSQL y Backend inicien
    Start-Sleep -Seconds 3

    # 2. Iniciar Frontend usando start-frontend.ps1
    Write-Host "   ⚙️  Ejecutando start-frontend.ps1..." -ForegroundColor Cyan
    $fArgs = @("-NoExit", "-ExecutionPolicy", "Bypass", "-File", "`"$FrontendScript`"")
    if ($Yes) { $fArgs += "-Yes" }
    Start-Process $psExe -ArgumentList $fArgs

    Write-Host "`n======================================================" -ForegroundColor Green
    Write-Host "  ✅ SERVICIOS EN EJECUCIÓN" -ForegroundColor Green
    Write-Host "======================================================" -ForegroundColor Green
    Write-Host "  🌐 Frontend UI : http://localhost:5085/auth/login" -ForegroundColor White
    Write-Host "  ⚙️  Backend API : http://localhost:5185" -ForegroundColor White
    Write-Host "  📖 Swagger API : http://localhost:5185/swagger" -ForegroundColor White
    Write-Host "  🐘 PostgreSQL  : localhost:5439 (postgre_recidencias)" -ForegroundColor White
    Write-Host "======================================================" -ForegroundColor Green
    Write-Host "`n💡 Puedes cerrar las ventanas individuales cuando termines de trabajar." -ForegroundColor DarkGray
}
