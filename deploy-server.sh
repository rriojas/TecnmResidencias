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

SKIP_PROMPT=false
for arg in "$@"; do
    if [ "$arg" = "-y" ] || [ "$arg" = "--yes" ]; then
        SKIP_PROMPT=true
    fi
done

# 2. Verificación de disponibilidad de puertos con confirmación previa
echo -e "\n${CYAN}2. Verificando disponibilidad de puertos (5085, 5185, 5439)...${NC}"

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
        echo -e "\n${YELLOW}⚠️  ALERTA: El puerto $port ($service_name) ya está en uso por un proceso host:${NC}"
        echo -e "   ${WHITE}$proc_details${NC}"

        if [ "$SKIP_PROMPT" = false ] && [ -t 0 ]; then
            read -r -p "👉 ¿Deseas detener el proceso ocupante (PID: $pids) para continuar? [S/n]: " response
            case "$response" in
                [nN][oO]|[nN])
                    echo -e "${RED}❌ Operación cancelada por el usuario para prevenir conflictos.${NC}"
                    echo -e "${YELLOW}💡 Libera manualmente el puerto $port o ajusta la configuración antes de volver a intentar.${NC}"
                    exit 1
                    ;;
                *)
                    echo -e "   ${CYAN}🔄 Liberando puerto $port (Deteniendo PID: $pids)...${NC}"
                    kill -9 $pids 2>/dev/null || true
                    sleep 1
                    echo -e "   ${GREEN}✅ Puerto $port liberado correctamente.${NC}"
                    ;;
            esac
        else
            echo -e "   ${YELLOW}🔄 Deteniendo PID $pids en puerto $port...${NC}"
            kill -9 $pids 2>/dev/null || true
            sleep 1
        fi
    else
        echo -e "   ${GREEN}✅ Puerto $port ($service_name) libre.${NC}"
    fi
}

check_and_confirm_port 5439 "PostgreSQL Base de Datos"
check_and_confirm_port 5185 "Backend API .NET"
check_and_confirm_port 5085 "Frontend Web Nginx"

# Confirmación general antes de montar contenedores
if [ "$SKIP_PROMPT" = false ] && [ -t 0 ]; then
    echo -e "\n${CYAN}📋 Resumen de Servicios a Desplegar:${NC}"
    echo -e "   🐘 Base de Datos : PostgreSQL 18 (${WHITE}BD: postgre_recidencias${NC}, Puerto: ${WHITE}5439${NC})"
    echo -e "   ⚙️  Backend API   : .NET 10 API (${WHITE}Puerto: 5185${NC})"
    echo -e "   🌐 Frontend Web  : Vue 3 + Nginx (${WHITE}Puerto: 5085${NC})"
    echo ""
    read -r -p "👉 ¿Confirmas el inicio del despliegue en el servidor? [S/n]: " main_response
    case "$main_response" in
        [nN][oO]|[nN])
            echo -e "${YELLOW}🛑 Despliegue cancelado por el usuario.${NC}"
            exit 0
            ;;
        *)
            echo -e "   ${GREEN}🚀 Procediendo con el despliegue...${NC}"
            ;;
    esac
fi

# 3. Asegurar estructura de directorios persistentes
echo -e "\n${CYAN}3. Preparando volúmenes y directorios de almacenamiento...${NC}"
mkdir -p RTecNM_V2_Backend/uploads/documents
mkdir -p RTecNM_V2_Backend/uploads/templates/excel
chmod -R 775 RTecNM_V2_Backend/uploads 2>/dev/null || true
echo -e "   ${GREEN}✅ Carpetas de archivos subidos configuradas.${NC}"

# 4. Construcción e inicio con Docker Compose
echo -e "\n${CYAN}4. Construyendo e iniciando contenedores en segundo plano...${NC}"
if ! docker compose up -d --build; then
    echo -e "\n${RED}❌ ERROR CRÍTICO: Falló la ejecución de 'docker compose up'.${NC}"
    echo -e "${YELLOW}👉 Revisa que ningún otro servicio esté usando los puertos 5085, 5185 o 5439 y verifica los permisos de Docker.${NC}"
    exit 1
fi

# 5. Esperar a que el backend e infraestructura estén 100% listos
echo -e "\n${CYAN}5. Verificando estado de los servicios...${NC}"
echo -e "   ${YELLOW}⏳ Esperando respuesta del Backend API...${NC}"

MAX_TRIES=30
TRIES=0
BACKEND_OK=false

while [ $TRIES -lt $MAX_TRIES ]; do
    if curl -s -f http://localhost:5185/api/v1/searches/autocomplete > /dev/null 2>&1 || \
       curl -s http://localhost:5185/swagger/index.html > /dev/null 2>&1; then
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
echo -e "  🌐 ${WHITE}Aplicación Web Frontend${NC} : http://${SERVER_IP}:5085  (o http://localhost:5085)"
echo -e "  ⚙️  ${WHITE}API Backend (.NET)${NC}      : http://${SERVER_IP}:5185"
echo -e "  📖 ${WHITE}Documentación Swagger${NC}  : http://${SERVER_IP}:5185/swagger"
echo -e "  🐘 ${WHITE}Base de Datos (Postgres)${NC}: Host Port 5439 (DB: postgre_recidencias)"
echo -e "${GREEN}======================================================================${NC}"
echo -e "📌 ${WHITE}Comandos útiles de gestión:${NC}"
echo -e "   - Ver estado de contenedores : ${YELLOW}docker compose ps${NC}"
echo -e "   - Ver logs en vivo           : ${YELLOW}docker compose logs -f${NC}"
echo -e "   - Detener el sistema         : ${YELLOW}docker compose down${NC}"
echo -e "   - Reiniciar todo             : ${YELLOW}docker compose restart${NC}"
echo -e "${GREEN}======================================================================${NC}\n"
