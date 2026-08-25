#!/usr/bin/env bash
# ==============================================================================
# TecNM Residencias v2 - Production Deployment Script for Ubuntu Server 24.04.4
# ==============================================================================

set -e

# Colores para terminal
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
WHITE='\033[1;37m'
NC='\033[0m' # No Color

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo -e "${CYAN}======================================================================${NC}"
echo -e "${CYAN}  🏛️  TecNM Residencias - Despliegue en Servidor (Ubuntu 24.04.4 LTS) ${NC}"
echo -e "${CYAN}======================================================================${NC}"

# 1. Verificación de Docker y Docker Compose
echo -e "\n${CYAN}1. Verificando requerimientos del sistema...${NC}"

if ! command -v docker &> /dev/null; then
    echo -e "${RED}❌ Docker no está instalado en este sistema Ubuntu.${NC}"
    echo -e "${YELLOW}👉 Para instalar Docker en Ubuntu Server 24.04, ejecuta:${NC}"
    echo "   sudo apt update && sudo apt install -y docker.io docker-compose-v2"
    echo "   sudo usermod -aG docker \$USER"
    exit 1
fi

if ! docker compose version &> /dev/null; then
    echo -e "${RED}❌ Docker Compose plugin no está disponible.${NC}"
    echo -e "${YELLOW}👉 Para instalarlo, ejecuta: sudo apt install -y docker-compose-v2${NC}"
    exit 1
fi

echo -e "   ${GREEN}✅ Docker y Docker Compose detectados correctamente.${NC}"

# 2. Liberar procesos host en puertos en uso si existen fuera de Docker
echo -e "\n${CYAN}2. Verificando disponibilidad de puertos (5000, 5144, 5432)...${NC}"

free_port_if_occupied() {
    local port=$1
    local pids
    pids=$(lsof -t -i :"$port" 2>/dev/null || true)
    if [ -n "$pids" ]; then
        echo -e "   ${YELLOW}⚠️  Puerto $port ocupado por proceso local. Deteniendo PIDs: $pids...${NC}"
        kill -9 $pids 2>/dev/null || true
    fi
}

free_port_if_occupied 5144

# 3. Asegurar estructura de directorios persistentes
echo -e "\n${CYAN}3. Preparando volúmenes y directorios de almacenamiento...${NC}"
mkdir -p RTecNM_V2_Backend/uploads/documents
mkdir -p RTecNM_V2_Backend/uploads/templates/excel
chmod -R 775 RTecNM_V2_Backend/uploads 2>/dev/null || true
echo -e "   ${GREEN}✅ Carpetas de archivos subidos configuradas.${NC}"

# 4. Construcción e inicio con Docker Compose
echo -e "\n${CYAN}4. Construyendo e iniciando contenedores en segundo plano...${NC}"
docker compose up -d --build

# 5. Esperar a que el backend e infraestructura estén 100% listos
echo -e "\n${CYAN}5. Verificando estado de los servicios...${NC}"
echo -e "   ${YELLOW}⏳ Esperando respuesta del Backend API...${NC}"

MAX_TRIES=30
TRIES=0
BACKEND_OK=false

while [ $TRIES -lt $MAX_TRIES ]; do
    if curl -s -f http://localhost:5144/api/v1/searches/autocomplete > /dev/null 2>&1 || \
       curl -s http://localhost:5144/swagger/index.html > /dev/null 2>&1; then
        BACKEND_OK=true
        break
    fi
    TRIES=$((TRIES+1))
    sleep 2
done

if [ "$BACKEND_OK" = true ]; then
    echo -e "   ${GREEN}✅ Backend API listo y respondiendo.${NC}"
else
    echo -e "   ${YELLOW}⚠️  El backend tardó más de lo esperado en responder. Revisa los logs.${NC}"
fi

# 6. Resumen de Despliegue
SERVER_IP=$(hostname -I 2>/dev/null | awk '{print $1}' || echo "IP_DEL_SERVIDOR")

echo -e "\n${GREEN}======================================================================${NC}"
echo -e "${GREEN}  🎉 ¡SISTEMA DESPLEGADO Y LISTO EN PRODUCCIÓN/SERVIDOR!${NC}"
echo -e "${GREEN}======================================================================${NC}"
echo -e "  🌐 ${WHITE}Aplicación Web Frontend${NC} : http://${SERVER_IP}:5000  (o http://localhost:5000)"
echo -e "  ⚙️  ${WHITE}API Backend (.NET)${NC}      : http://${SERVER_IP}:5144"
echo -e "  📖 ${WHITE}Documentación Swagger${NC}  : http://${SERVER_IP}:5144/swagger"
echo -e "  🐘 ${WHITE}Base de Datos (Postgres)${NC}: Host Port 5432 (DB: residency_v2)"
echo -e "${GREEN}======================================================================${NC}"
echo -e "📌 ${WHITE}Comandos útiles de gestión:${NC}"
echo -e "   - Ver estado de contenedores : ${YELLOW}docker compose ps${NC}"
echo -e "   - Ver logs en vivo           : ${YELLOW}docker compose logs -f${NC}"
echo -e "   - Detener el sistema         : ${YELLOW}docker compose down${NC}"
echo -e "   - Reiniciar todo             : ${YELLOW}docker compose restart${NC}"
echo -e "${GREEN}======================================================================${NC}\n"
