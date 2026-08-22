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
echo -e "${CYAN}  🏛️  TecNM Residencias - Iniciando Frontend (Vite + Vue 3)${NC}"
echo -e "${CYAN}======================================================${NC}"

# 1. Verificar pnpm
echo -e "\n${YELLOW}🔍 Verificando pnpm...${NC}"
if command -v pnpm >/dev/null 2>&1; then
    PNPM_VERSION=$(pnpm -v)
    echo -e "   ${GREEN}✅ pnpm detectado: v${PNPM_VERSION}${NC}"
else
    echo -e "   ${RED}❌ ERROR: pnpm no está instalado o no se encuentra en el PATH.${NC}"
    exit 1
fi

# 2. Liberar puerto 5000 si está ocupado
free_port 5000

# 3. Iniciar Frontend Vite
echo -e "\n${GREEN}🚀 Iniciando Frontend Vite en http://localhost:5000...${NC}"
echo -e "   ${DARKCYAN}🌐 Acceso al sistema: http://localhost:5000/auth/login${NC}"
echo -e "   ${DARKGRAY}🔗 Conectado a Backend: http://localhost:5144${NC}\n"

if [ ! -d "$FRONTEND_DIR" ]; then
    echo -e "${RED}❌ ERROR: No se encontró la carpeta $FRONTEND_DIR${NC}"
    exit 1
fi

cd "$FRONTEND_DIR"
pnpm dev
