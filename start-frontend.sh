#!/usr/bin/env bash
# ==============================================================================
# Script: start-frontend.sh
# Descripción: Inicia el Frontend Vite + Vue 3 (pnpm dev)
# ==============================================================================

set -e

# Colores para la terminal
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
DARKCYAN='\033[0;34m'
DARKGRAY='\033[1;30m'
NC='\033[0m' # No Color

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FRONTEND_DIR="$SCRIPT_DIR/RTecNM_V2_Frontend"

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
echo -e "${CYAN}  🏛️  TecNM Residencias - Iniciando Frontend (Vite + Vue 3)${NC}"
echo -e "${CYAN}======================================================${NC}"

# 1. Verificar Gestor de Paquetes (pnpm o npm)
echo -e "\n${YELLOW}🔍 Verificando gestor de paquetes (pnpm / npm)...${NC}"
PKG_MANAGER=""
if command -v pnpm >/dev/null 2>&1; then
    PNPM_VERSION=$(pnpm -v)
    echo -e "   ${GREEN}✅ pnpm detectado: v${PNPM_VERSION}${NC}"
    PKG_MANAGER="pnpm"
elif command -v npm >/dev/null 2>&1; then
    NPM_VERSION=$(npm -v)
    echo -e "   ${GREEN}✅ npm detectado: v${NPM_VERSION} (usando npm)${NC}"
    PKG_MANAGER="npm"
else
    echo -e "   ${RED}❌ ERROR: Ni pnpm ni npm están instalados o disponibles en el PATH.${NC}"
    exit 1
fi

# 2. Verificar puerto 5085 ocupado
check_and_confirm_port 5085 "Frontend Vite Vue 3"

# 3. Iniciar Frontend Vite
echo -e "\n${GREEN}🚀 Iniciando Frontend Vite en http://localhost:5085...${NC}"
echo -e "   ${DARKCYAN}🌐 Acceso al sistema: http://localhost:5085/auth/login${NC}"
echo -e "   ${DARKGRAY}🔗 Conectado a Backend: http://localhost:5185${NC}\n"

if [ ! -d "$FRONTEND_DIR" ]; then
    echo -e "${RED}❌ ERROR: No se encontró la carpeta $FRONTEND_DIR${NC}"
    exit 1
fi

cd "$FRONTEND_DIR"

if [ ! -d "node_modules" ]; then
    echo -e "   ${YELLOW}📦 Instalando dependencias del Frontend (${PKG_MANAGER} install)...${NC}"
    if [ "$PKG_MANAGER" = "pnpm" ]; then
        pnpm install
    else
        npm install
    fi
fi

if [ "$PKG_MANAGER" = "pnpm" ]; then
    pnpm dev
else
    npm run dev
fi
