#!/usr/bin/env bash
# ==============================================================================
# Script: start-all.sh
# Descripción: Inicia Backend y Frontend utilizando start-backend.sh y start-frontend.sh
# ==============================================================================

set -e

# Colores para la terminal
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
WHITE='\033[1;37m'
NC='\033[0m' # No Color

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACKEND_SCRIPT="$SCRIPT_DIR/start-backend.sh"
FRONTEND_SCRIPT="$SCRIPT_DIR/start-frontend.sh"

echo -e "${CYAN}======================================================${NC}"
echo -e "${CYAN}  🏛️  TecNM Residencias - Iniciando Stack Completo${NC}"
echo -e "${CYAN}======================================================${NC}"

if [ ! -f "$BACKEND_SCRIPT" ]; then
    echo -e "${RED}❌ ERROR: No se encontró $BACKEND_SCRIPT${NC}"
    exit 1
fi

if [ ! -f "$FRONTEND_SCRIPT" ]; then
    echo -e "${RED}❌ ERROR: No se encontró $FRONTEND_SCRIPT${NC}"
    exit 1
fi

# Dar permisos de ejecución si no los tienen
chmod +x "$BACKEND_SCRIPT" "$FRONTEND_SCRIPT" 2>/dev/null || true

BACKEND_PID=""
FRONTEND_PID=""

cleanup() {
    echo -e "\n\n${YELLOW}🛑 Deteniendo Backend y Frontend...${NC}"
    if [ -n "$BACKEND_PID" ]; then
        kill "$BACKEND_PID" 2>/dev/null || true
    fi
    if [ -n "$FRONTEND_PID" ]; then
        kill "$FRONTEND_PID" 2>/dev/null || true
    fi
    wait 2>/dev/null || true
    echo -e "${GREEN}✅ Servicios detenidos correctamente.${NC}"
    exit 0
}

trap cleanup SIGINT SIGTERM EXIT

# 1. Iniciar Backend usando start-backend.sh
echo -e "\n${CYAN}⚙️  Ejecutando start-backend.sh en segundo plano...${NC}"
"$BACKEND_SCRIPT" &
BACKEND_PID=$!

# Esperar 3 segundos para que PostgreSQL y Backend inicien
sleep 3

# 2. Iniciar Frontend usando start-frontend.sh
echo -e "\n${CYAN}⚙️  Ejecutando start-frontend.sh en segundo plano...${NC}"
"$FRONTEND_SCRIPT" &
FRONTEND_PID=$!

echo -e "\n${GREEN}======================================================${NC}"
echo -e "${GREEN}  ✅ SERVICIOS EN EJECUCIÓN${NC}"
echo -e "${GREEN}======================================================${NC}"
echo -e "  🌐 ${WHITE}Frontend UI${NC} : http://localhost:5000/auth/login"
echo -e "  ⚙️  ${WHITE}Backend API${NC} : http://localhost:5144"
echo -e "  📖 ${WHITE}Swagger API${NC} : http://localhost:5144/swagger"
echo -e "  🐘 ${WHITE}PostgreSQL${NC}  : localhost:5433 (residency_v2)"
echo -e "${GREEN}======================================================${NC}"
echo -e "💡 Presiona ${YELLOW}Ctrl + C${NC} en esta terminal para detener ambos servicios.\n"

# Esperar a que ambos procesos finalicen
wait "$BACKEND_PID" "$FRONTEND_PID" 2>/dev/null || true
