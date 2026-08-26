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

SKIP_PROMPT=false
for arg in "$@"; do
    if [ "$arg" = "-y" ] || [ "$arg" = "--yes" ]; then
        SKIP_PROMPT=true
    fi
done

check_and_confirm_port() {
    local port=$1
    local service_name=$2
    local pids=""
    local proc_details=""

    if command -v lsof >/dev/null 2>&1; then
        pids=$(lsof -t -i :"$port" 2>/dev/null || true)
        if [ -n "$pids" ]; then
            proc_details=$(ps -p "$pids" -o pid,comm,args --no-headers 2>/dev/null || echo "PID $pids")
        fi
    elif command -v fuser >/dev/null 2>&1; then
        pids=$(fuser "${port}/tcp" 2>/dev/null | xargs || true)
        if [ -n "$pids" ]; then
            proc_details="PID $pids"
        fi
    fi

    if [ -n "$pids" ]; then
        echo -e "\n${YELLOW}⚠️  ALERTA: El puerto $port ($service_name) ya está en uso por un proceso local:${NC}"
        echo -e "   ${WHITE}$proc_details${NC}"

        if [ "$SKIP_PROMPT" = false ] && [ -t 0 ]; then
            read -r -p "👉 ¿Deseas detener el proceso ocupante (PID: $pids) para continuar? [S/n]: " response
            case "$response" in
                [nN][oO]|[nN])
                    echo -e "${RED}❌ Operación cancelada por el usuario.${NC}"
                    exit 1
                    ;;
                *)
                    echo -e "   ${CYAN}🔄 Liberando puerto $port (Deteniendo PID $pids)...${NC}"
                    kill -9 $pids 2>/dev/null || true
                    sleep 1
                    echo -e "   ${GREEN}✅ Puerto $port liberado.${NC}"
                    ;;
            esac
        else
            echo -e "   ${YELLOW}🔄 Deteniendo PID $pids en puerto $port...${NC}"
            kill -9 $pids 2>/dev/null || true
            sleep 1
        fi
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

# 2. Verificación de puertos ocupados
check_and_confirm_port 5439 "PostgreSQL Base de Datos"
check_and_confirm_port 5185 "Backend API .NET"

# 3. Verificar/Iniciar PostgreSQL con Docker Compose
echo -e "\n${YELLOW}🐘 Verificando Base de Datos PostgreSQL...${NC}"
if command -v docker >/dev/null 2>&1; then
    PG_RUNNING=$(docker ps --filter "name=residencia-v2-db" --filter "status=running" --format "{{.Names}}" 2>/dev/null || true)
    if [ -z "$PG_RUNNING" ]; then
        echo -e "   ${CYAN}⚙️  Levantando contenedor PostgreSQL (residencia-v2-db en puerto 5439 / BD: postgre_recidencias)...${NC}"
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
    echo -e "   ${YELLOW}⚠️  Docker no detectado. Asegúrate de tener PostgreSQL corriendo localmente en el puerto 5439.${NC}"
fi

# 4. Iniciar Backend
echo -e "\n${GREEN}🚀 Iniciando Backend Web API en http://localhost:5185...${NC}"
echo -e "   ${DARKCYAN}📖 Swagger / OpenAPI disponible en: http://localhost:5185/swagger${NC}\n"

if [ ! -d "$BACKEND_DIR" ]; then
    echo -e "${RED}❌ ERROR: No se encontró la carpeta $BACKEND_DIR${NC}"
    exit 1
fi

cd "$BACKEND_DIR"
dotnet run --launch-profile http
