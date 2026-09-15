# ==============================================================================
# Script: start-backend.ps1
# Descripción: Valida herramientas/paquetes e inicia PostgreSQL y Backend (.NET 10)
# ==============================================================================

param (
    [switch]$Yes = $false
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BackendDir = Join-Path $ScriptDir "RTecNM_V2_Backend"


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

function Check-And-Confirm-Process([string]$processName) {
    $procs = Get-Process -Name $processName -ErrorAction SilentlyContinue
    if ($procs) {
        foreach ($proc in $procs) {
            Write-Host "`n⚠️  ALERTA: Se detectó una instancia previa en ejecución: $($proc.ProcessName) (PID: $($proc.Id))" -ForegroundColor Yellow
            if (Prompt-User "👉 ¿Deseas detener el proceso $($proc.ProcessName) (PID: $($proc.Id)) para evitar bloqueos?" $true) {
                Write-Host "   🔄 Deteniendo proceso $($proc.ProcessName) (PID $($proc.Id))..." -ForegroundColor Yellow
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                Start-Sleep -Milliseconds 500
            }
        }
    }
}

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "  🏛️  TecNM Residencias - Iniciando Backend API (.NET 10)" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Verificar .NET SDK
Write-Host "`n🔍 Verificando .NET SDK..." -ForegroundColor Yellow
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCmd) {
    Write-Host "   ⚠️  .NET SDK no está instalado o no se encuentra en el PATH." -ForegroundColor Yellow
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        if (Prompt-User "👉 ¿Deseas descargar e instalar .NET SDK 10 mediante winget automáticamente?" $true) {
            Write-Host "   ⬇️  Instalando Microsoft.DotNet.SDK.10 vía winget..." -ForegroundColor Cyan
            winget install Microsoft.DotNet.SDK.10 --accept-package-agreements --accept-source-agreements
            Refresh-Path
        } else {
            Write-Host "❌ .NET SDK 10 es requerido para el Backend. Por favor instálalo manualmente." -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "❌ ERROR: .NET SDK no disponible y winget no fue encontrado." -ForegroundColor Red
        exit 1
    }
}

if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $dotnetVersion = dotnet --version
    Write-Host "   ✅ .NET SDK detectado: v$dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "❌ ERROR: .NET SDK aún no está disponible en la sesión actual. Reinicia la terminal tras instalarlo." -ForegroundColor Red
    exit 1
}

# 2. Verificar/Iniciar PostgreSQL con Docker
Write-Host "`n🐘 Verificando Base de Datos PostgreSQL (Puerto 5439)..." -ForegroundColor Yellow
$dockerCmd = Get-Command docker -ErrorAction SilentlyContinue

if (-not $dockerCmd) {
    Write-Host "   ⚠️  Docker no está instalado en el sistema." -ForegroundColor Yellow
    if (Get-Command winget -ErrorAction SilentlyContinue) {
        if (Prompt-User "👉 ¿Deseas descargar e instalar Docker Desktop mediante winget?" $false) {
            Write-Host "   ⬇️  Instalando Docker Desktop vía winget..." -ForegroundColor Cyan
            winget install Docker.DockerDesktop --accept-package-agreements --accept-source-agreements
            Refresh-Path
            Write-Host "   ℹ️  Nota: Tras instalar Docker Desktop, suele ser necesario reiniciar sesión." -ForegroundColor DarkCyan
        } else {
            Write-Host "   ℹ️  Asegúrate de tener un servidor PostgreSQL activo en localhost:5439 (postgre_recidencias)." -ForegroundColor DarkGray
        }
    }
} else {
    # Verificar si el demonio de Docker está respondiendo
    $dockerInfo = docker info 2>$null
    if (-not $dockerInfo) {
        Write-Host "   ⚠️  El motor de Docker no parece estar en ejecución." -ForegroundColor Yellow
        $dockerDesktopPaths = @(
            "$env:ProgramFiles\Docker\Docker\Docker Desktop.exe",
            "$env:ProgramFiles(x86)\Docker\Docker\Docker Desktop.exe"
        )
        $dockerExe = $dockerDesktopPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
        if ($dockerExe -and (Prompt-User "👉 ¿Deseas intentar arrancar Docker Desktop automáticamente?" $true)) {
            Write-Host "   🚀 Iniciando Docker Desktop..." -ForegroundColor Cyan
            Start-Process $dockerExe
            Write-Host "   ⏳ Esperando 10 segundos a que inicialice Docker..." -ForegroundColor DarkGray
            Start-Sleep -Seconds 10
        }
    }

    # Verificar estado del contenedor postgres
    $pgRunning = docker ps --filter "name=residencia-v2-db" --filter "status=running" --format "{{.Names}}" 2>$null
    if (-not $pgRunning) {
        Write-Host "   ⚙️  El contenedor PostgreSQL 'residencia-v2-db' no está activo." -ForegroundColor Yellow
        if (Prompt-User "👉 ¿Deseas levantar el contenedor PostgreSQL con Docker Compose?" $true) {
            Push-Location $ScriptDir
            if (docker compose version 2>$null) {
                docker compose up -d postgres
            } else {
                docker-compose up -d postgres
            }
            Pop-Location
            Write-Host "   ✅ Contenedor PostgreSQL iniciado." -ForegroundColor Green
            Start-Sleep -Seconds 2
        } else {
            Write-Host "   ⚠️  Continuando sin levantar contenedor PostgreSQL." -ForegroundColor Yellow
        }
    } else {
        Write-Host "   ✅ Contenedor PostgreSQL (residencia-v2-db) ya está en ejecución." -ForegroundColor Green
    }
}

# 3. Comprobar conectividad con PostgreSQL (esperar hasta que responda)
Write-Host "`n🐘 Esperando disponibilidad de PostgreSQL en puerto 5439..." -ForegroundColor Yellow
$retries = 25
$pgReady = $false
while ($retries -gt 0) {
    if (Test-NetConnection -ComputerName localhost -Port 5439 -WarningAction SilentlyContinue -InformationLevel Quiet) {
        $pgReady = $true
        break
    }
    Start-Sleep -Seconds 1
    $retries--
}

if ($pgReady) {
    Write-Host "   ✅ Conexión con PostgreSQL en puerto 5439 confirmada y lista." -ForegroundColor Green
    
    # Asegurar que la base de datos postgre_recidencias exista y contenga el esquema actualizado
    if ($dockerCmd -and (docker ps --filter "name=residencia-v2-db" --filter "status=running" --format "{{.Names}}" 2>$null)) {
        $dbExists = docker exec residencia-v2-db psql -U residency_user -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='postgre_recidencias'" 2>$null
        if ($dbExists -ne "1") {
            Write-Host "   ⚙️  Creando base de datos 'postgre_recidencias'..." -ForegroundColor Cyan
            docker exec residencia-v2-db psql -U residency_user -d postgres -c "CREATE DATABASE postgre_recidencias OWNER residency_user;" 2>$null
        }

        # Verificar si las tablas principales existen
        $tablesCount = docker exec residencia-v2-db psql -U residency_user -d postgre_recidencias -tAc "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('users', 'modules', 'careers');" 2>$null
        $countVal = 0
        [int]::TryParse($tablesCount, [ref]$countVal) | Out-Null
        if ($countVal -lt 3) {
            Write-Host "   ⚙️  Aplicando esquema de base de datos y semillas esenciales..." -ForegroundColor Cyan
            $schemaFile = Join-Path $ScriptDir "docs\database\01_schema_and_essential_seeds.sql"
            if (Test-Path $schemaFile) {
                Get-Content $schemaFile -Raw | docker exec -i residencia-v2-db psql -U residency_user -d postgre_recidencias 2>$null
            }
            $demoFile = Join-Path $ScriptDir "docs\database\02_demo_data.sql"
            if (Test-Path $demoFile) {
                Get-Content $demoFile -Raw | docker exec -i residencia-v2-db psql -U residency_user -d postgre_recidencias 2>$null
            }
            Write-Host "   ✅ Esquema y semillas aplicadas correctamente." -ForegroundColor Green
        } else {
            Write-Host "   ✅ Base de datos 'postgre_recidencias' verificada y lista." -ForegroundColor Green
        }
    }
} else {
    Write-Host "   ❌ No se detectó respuesta en puerto 5439 tras esperar." -ForegroundColor Red
    if (-not (Prompt-User "👉 ¿Deseas intentar arrancar el backend de todos modos?" $false)) {
        exit 1
    }
}

# 4. Validar directorio del Backend
if (-not (Test-Path $BackendDir)) {
    Write-Host "❌ ERROR: No se encontró la carpeta $BackendDir" -ForegroundColor Red
    exit 1
}

Set-Location $BackendDir

# 5. Verificar dependencias NuGet
$projectAssets = Join-Path $BackendDir "obj\project.assets.json"
if (-not (Test-Path $projectAssets)) {
    Write-Host "`n📦 Dependencias NuGet no detectadas (falta obj/project.assets.json)." -ForegroundColor Yellow
    if (Prompt-User "👉 ¿Deseas descargar y restaurar los paquetes NuGet ahora con 'dotnet restore'?" $true) {
        Write-Host "   ⬇️  Restaurando paquetes NuGet (dotnet restore)..." -ForegroundColor Cyan
        dotnet restore
        Write-Host "   ✅ Paquetes NuGet restaurados exitosamente." -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  Restauración omitida. El proyecto intentará compilar con el estado actual." -ForegroundColor Yellow
    }
} else {
    Write-Host "   ✅ Dependencias NuGet restauradas detectadas." -ForegroundColor Green
}

# 6. Verificar puerto 5185 y procesos previos
Check-And-Confirm-Process "TecNM.Residency"
Check-And-Confirm-Port 5185 "Backend API .NET"

# 7. Iniciar Backend Web API
Write-Host "`n🚀 Iniciando Backend Web API en http://localhost:5185..." -ForegroundColor Green
Write-Host "   📖 Swagger / OpenAPI disponible en: http://localhost:5185/swagger" -ForegroundColor DarkCyan
Write-Host "   🐘 Base de Datos: localhost:5439 (postgre_recidencias)`n" -ForegroundColor DarkGray

dotnet run --launch-profile http
