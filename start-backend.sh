#!/usr/bin/env bash
# ==============================================================================
# Script: start-backend.sh
# Descripción: Inicia la base de datos PostgreSQL (Docker) y el Backend API (.NET 10)
# ==============================================================================

set -e

# Colores para la terminal
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
DARKCYAN='\033[0;34m'
NC='\033[0m' # No Color

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_DIR="$SCRIPT_DIR/RTecNM_V2_Backend"

free_port() {
    local port=$1
    if command -v lsof >/dev/null 2>&1; then
        local pids
        pids=$(lsof -ti :"$port" 2>/dev/null || true)
        if [ -n "$pids" ]; then
            echo -e "   ${YELLOW}🔄 Liberando puerto $port (PID: $pids)...${NC}"
            kill -9 $pids 2>/dev/null || true
            sleep 0.5
        fi
    elif command -v fuser >/dev/null 2>&1; then
        fuser -k "${port}/tcp" 2>/dev/null || true
    fi
}

echo -e "${CYAN}======================================================${NC}"
echo -e "${CYAN}  🏛️  TecNM Residencias - Iniciando Backend API${NC}"
echo -e "${CYAN}======================================================${NC}"

# 1. Verificar .NET SDK
echo -e "\n${YELLOW}🔍 Verificando .NET SDK...${NC}"
if command -v dotnet >/dev/null 2>&1; then
    DOTNET_VERSION=$(dotnet --version)
    echo -e "   ${GREEN}✅ .NET SDK detectado: v${DOTNET_VERSION}${NC}"
else
    echo -e "   ${RED}❌ ERROR: .NET SDK no está instalado o no se encuentra en el PATH.${NC}"
    exit 1
fi

# 2. Verificar/Iniciar PostgreSQL con Docker Compose
echo -e "\n${YELLOW}🐘 Verificando Base de Datos PostgreSQL...${NC}"
if command -v docker >/dev/null 2>&1; then
    PG_RUNNING=$(docker ps --filter "name=residencia-v2-db" --filter "status=running" --format "{{.Names}}" 2>/dev/null || true)
    if [ -z "$PG_RUNNING" ]; then
        echo -e "   ${CYAN}⚙️  Levantando contenedor PostgreSQL (residencia-v2-db)...${NC}"
        cd "$SCRIPT_DIR"
        if docker compose version >/dev/null 2>&1; then
            docker compose up -d postgres
        elif command -v docker-compose >/dev/null 2>&1; then
            docker-compose up -d postgres
        else
            echo -e "   ${YELLOW}⚠️  No se encontró docker compose/docker-compose.${NC}"
        fi
        echo -e "   ${GREEN}✅ Contenedor PostgreSQL iniciado correctamente.${NC}"
    else
        echo -e "   ${GREEN}✅ Contenedor PostgreSQL ya se encuentra en ejecución.${NC}"
    fi
else
    echo -e "   ${YELLOW}⚠️  Docker no detectado. Asegúrate de tener PostgreSQL corriendo localmente en el puerto 5432.${NC}"
fi

# 3. Liberar puerto 5144 si está ocupado
free_port 5144

# 4. Iniciar Backend
echo -e "\n${GREEN}🚀 Iniciando Backend Web API en http://localhost:5144...${NC}"
echo -e "   ${DARKCYAN}📖 Swagger / OpenAPI disponible en: http://localhost:5144/swagger${NC}\n"

if [ ! -d "$BACKEND_DIR" ]; then
    echo -e "${RED}❌ ERROR: No se encontró la carpeta $BACKEND_DIR${NC}"
    exit 1
fi

cd "$BACKEND_DIR"
dotnet run --launch-profile http
