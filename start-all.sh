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

SKIP_PROMPT=false
for arg in "$@"; do
    if [ "$arg" = "-y" ] || [ "$arg" = "--yes" ]; then
        SKIP_PROMPT=true
    fi
done

if [ "$SKIP_PROMPT" = false ] && [ -t 0 ]; then
    echo -e "\n${CYAN}📋 Resumen del Entorno de Desarrollo a Iniciar:${NC}"
    echo -e "   🐘 PostgreSQL (Docker)  : ${WHITE}BD: postgre_recidencias (Puerto: 5439)${NC}"
    echo -e "   ⚙️  Backend API (.NET)   : ${WHITE}http://localhost:5185${NC}"
    echo -e "   🌐 Frontend SPA (Vite)  : ${WHITE}http://localhost:5085${NC}"
    echo ""
    read -r -p "👉 ¿Deseas verificar puertos e iniciar el Stack Completo? [S/n]: " main_response
    case "$main_response" in
        [nN][oO]|[nN])
            echo -e "${YELLOW}🛑 Inicio cancelado por el usuario.${NC}"
            exit 0
            ;;
        *)
            echo -e "   ${GREEN}🚀 Iniciando verificación y arranque de servicios...${NC}"
            ;;
    esac
fi

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

# Esperar a que el Backend API (.NET) escuche en el puerto 5185
echo -e "   ${YELLOW}⏳ Esperando a que el Backend API esté listo en puerto 5185...${NC}"
for i in {1..25}; do
    if curl -s http://localhost:5185 >/dev/null 2>&1 || (exec 3<>/dev/tcp/localhost/5185) 2>/dev/null; then
        echo -e "   ${GREEN}✅ Backend API listo y respondiendo.${NC}"
        break
    fi
    sleep 1
done

# 2. Iniciar Frontend usando start-frontend.sh
echo -e "\n${CYAN}⚙️  Ejecutando start-frontend.sh en segundo plano...${NC}"
"$FRONTEND_SCRIPT" &
FRONTEND_PID=$!

echo -e "\n${GREEN}======================================================${NC}"
echo -e "${GREEN}  ✅ SERVICIOS EN EJECUCIÓN${NC}"
echo -e "${GREEN}======================================================${NC}"
echo -e "  🌐 ${WHITE}Frontend UI${NC} : http://localhost:5085/auth/login"
echo -e "  ⚙️  ${WHITE}Backend API${NC} : http://localhost:5185"
echo -e "  📖 ${WHITE}Swagger API${NC} : http://localhost:5185/swagger"
echo -e "  🐘 ${WHITE}PostgreSQL${NC}  : localhost:5439 (postgre_recidencias)"
echo -e "${GREEN}======================================================${NC}"
echo -e "💡 Presiona ${YELLOW}Ctrl + C${NC} en esta terminal para detener ambos servicios.\n"

# Esperar a que ambos procesos finalicen
wait "$BACKEND_PID" "$FRONTEND_PID" 2>/dev/null || true
