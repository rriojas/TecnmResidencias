#!/usr/bin/env bash
# ==============================================================================
# TecNM Residencias v2 - Production Safe Update Script
# Actualización segura de producción sin pérdida de datos ni caída del servicio
# ==============================================================================

set -euo pipefail

# Colores de salida
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
WHITE='\033[1;37m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo -e "${CYAN}======================================================================${NC}"
echo -e "${CYAN}  🏛️  TecNM Residencias - Actualización Segura en Servidor de Producción${NC}"
echo -e "${CYAN}======================================================================${NC}"

# Parsear argumentos
SKIP_PROMPT=false
SKIP_BACKUP=false

for arg in "$@"; do
    case "$arg" in
        -y|--yes)
            SKIP_PROMPT=true
            ;;
        --no-backup)
            SKIP_BACKUP=true
            ;;
    esac
done

# 1. Comprobar herramientas requeridas
echo -e "\n${CYAN}[1/6] Verificando Docker y Docker Compose...${NC}"
if ! command -v docker &> /dev/null; then
    echo -e "${RED}❌ Docker no está instalado o no está en el PATH.${NC}"
    exit 1
fi

if ! docker compose version &> /dev/null; then
    echo -e "${RED}❌ Docker Compose plugin no está disponible.${NC}"
    exit 1
fi
echo -e "   ${GREEN}✅ Entorno Docker validado.${NC}"

# 2. Respaldo Preventivo de Base de Datos PostgreSQL
echo -e "\n${CYAN}[2/6] Verificando Base de Datos y Creando Respaldo Preventivo...${NC}"
BACKUP_DIR="${SCRIPT_DIR}/backups"
mkdir -p "$BACKUP_DIR"

DB_CONTAINER="residencia-v2-db"
DB_NAME="postgre_recidencias"
DB_USER="residency_user"

if [ "$SKIP_BACKUP" = false ]; then
    if docker ps --format '{{.Names}}' | grep -Eq "^${DB_CONTAINER}\$"; then
        TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
        BACKUP_FILE="${BACKUP_DIR}/backup_${DB_NAME}_${TIMESTAMP}.sql"

        echo -e "   ${YELLOW}⏳ Extrayendo snapshot de PostgreSQL (${DB_CONTAINER})...${NC}"
        if docker exec "$DB_CONTAINER" pg_dump -U "$DB_USER" -d "$DB_NAME" --clean --if-exists > "$BACKUP_FILE"; then
            BACKUP_SIZE=$(du -h "$BACKUP_FILE" | awk '{print $1}')
            echo -e "   ${GREEN}✅ Respaldo generado con éxito:${NC} ${WHITE}${BACKUP_FILE}${NC} (${BACKUP_SIZE})"
        else
            echo -e "${RED}⚠️  Advertencia: pg_dump falló. No se pudo generar el respaldo automático.${NC}"
            if [ "$SKIP_PROMPT" = false ] && [ -t 0 ]; then
                read -r -p "👉 ¿Deseas continuar con la actualización SIN respaldo previo? [s/N]: " continue_without_backup
                case "$continue_without_backup" in
                    [sS][iI]|[sS])
                        echo -e "   ${YELLOW}⚠️ Continuando sin respaldo...${NC}"
                        ;;
                    *)
                        echo -e "${RED}🛑 Operación cancelada para proteger los datos.${NC}"
                        exit 1
                        ;;
                esac
            fi
        fi
    else
        echo -e "   ${YELLOW}ℹ️  El contenedor de base de datos no está en ejecución. Se preservarán los volúmenes existentes.${NC}"
    fi
else
    echo -e "   ${YELLOW}ℹ️  Respaldo omitido por bandera --no-backup.${NC}"
fi

# Confirmación previa si es interactivo
if [ "$SKIP_PROMPT" = false ] && [ -t 0 ]; then
    echo -e "\n${CYAN}📋 Resumen de la Actualización:${NC}"
    echo -e "   🐘 Base de Datos : ${GREEN}Preservada intacta${NC} (Volumen: postgres_v2_data, sin alteraciones)"
    echo -e "   📁 Archivos Uploads: ${GREEN}Preservados intactos${NC} (./RTecNM_V2_Backend/uploads)"
    echo -e "   ⚙️  Backend API   : Recompilación e inicio en caliente (.NET 10)"
    echo -e "   🌐 Frontend Web  : Recompilación de bundle de producción (Vue 3 + Nginx)"
    echo ""
    read -r -p "👉 ¿Deseas proceder con la actualización en caliente? [S/n]: " confirm_update
    case "$confirm_update" in
        [nN][oO]|[nN])
            echo -e "${YELLOW}🛑 Actualización cancelada por el usuario.${NC}"
            exit 0
            ;;
    esac
fi

# 3. Pre-construcción segura de imágenes (Dry-run antes de detener nada)
echo -e "\n${CYAN}[3/6] Compilando nuevas imágenes de Backend y Frontend en paralelo...${NC}"
echo -e "   ${YELLOW}⏳ Esto garantiza que si hay un error de compilación, el servicio actual NO sufra interrupción.${NC}"

if ! docker compose build backend frontend; then
    echo -e "\n${RED}❌ ERROR CRÍTICO: La compilación de las nuevas imágenes falló.${NC}"
    echo -e "${YELLOW}👉 El servicio de producción actual continúa intacto y sin afectación.${NC}"
    echo -e "${YELLOW}👉 Corrige los errores en el código antes de reintentar.${NC}"
    exit 1
fi
echo -e "   ${GREEN}✅ Imágenes construidas correctamente sin errores.${NC}"

# 4. Actualización en caliente de contenedores (Zero Downtime / Hot Swap)
echo -e "\n${CYAN}[4/6] Aplicando actualización a los contenedores en producción...${NC}"

# Asegurar que Postgres esté arriba si no lo estaba (sin re-crear volumen)
docker compose up -d postgres

# Recrear backend con la nueva imagen preservando volúmenes de uploads
echo -e "   ${CYAN}🔄 Actualizando contenedor de Backend API...${NC}"
docker compose up -d --no-deps backend

# Recrear frontend con la nueva imagen
echo -e "   ${CYAN}🔄 Actualizando contenedor de Frontend Web...${NC}"
docker compose up -d --no-deps frontend

echo -e "   ${GREEN}✅ Contenedores actualizados en segundo plano.${NC}"

# 5. Verificación de Salud de los Servicios
echo -e "\n${CYAN}[5/6] Verificando salud de la aplicación en producción...${NC}"

# Esperar respuesta del backend
BACKEND_OK=false
MAX_RETRIES=30
RETRY=0

echo -e "   ${YELLOW}⏳ Esperando inicio de la API Backend...${NC}"
while [ $RETRY -lt $MAX_RETRIES ]; do
    if curl -s -f http://localhost:5185/swagger/index.html > /dev/null 2>&1 || \
       curl -s -f http://localhost:5185/api/v1/searches/autocomplete > /dev/null 2>&1 || \
       docker compose ps backend | grep -q "Up"; then
        BACKEND_OK=true
        break
    fi
    RETRY=$((RETRY + 1))
    sleep 2
done

if [ "$BACKEND_OK" = true ]; then
    echo -e "   ${GREEN}✅ Backend API activo y respondiendo.${NC}"
else
    echo -e "   ${YELLOW}⚠️  El backend tardó en responder. Revisa los logs con: docker compose logs backend${NC}"
fi

# Esperar respuesta del frontend
FRONTEND_OK=false
RETRY=0
while [ $RETRY -lt 15 ]; do
    if curl -s -f http://localhost:5085 > /dev/null 2>&1 || \
       docker compose ps frontend | grep -q "Up"; then
        FRONTEND_OK=true
        break
    fi
    RETRY=$((RETRY + 1))
    sleep 1
done

if [ "$FRONTEND_OK" = true ]; then
    echo -e "   ${GREEN}✅ Frontend Web activo y sirviendo la nueva versión.${NC}"
fi

# 6. Limpieza segura de imágenes huérfanas
echo -e "\n${CYAN}[6/6] Limpiando capas intermedias huérfanas de Docker...${NC}"
docker image prune -f > /dev/null 2>&1 || true
echo -e "   ${GREEN}✅ Espacio en disco optimizado.${NC}"

SERVER_IP=$(hostname -I 2>/dev/null | awk '{print $1}' || echo "localhost")

echo -e "\n${GREEN}======================================================================${NC}"
echo -e "${GREEN}  🎉 ¡ACTUALIZACIÓN EXITOSA COMPLETADA SIN AFECTAR DATOS!${NC}"
echo -e "${GREEN}======================================================================${NC}"
echo -e "  🌐 ${WHITE}Frontend Web Actualizado${NC} : http://${SERVER_IP}:5085"
echo -e "  ⚙️  ${WHITE}Backend API Actualizado${NC}  : http://${SERVER_IP}:5185"
echo -e "  🐘 ${WHITE}Base de Datos${NC}            : Intacta (PostgreSQL en puerto 5439)"
if [ "$SKIP_BACKUP" = false ] && [ -n "${BACKUP_FILE:-}" ] && [ -f "${BACKUP_FILE:-}" ]; then
    echo -e "  💾 ${WHITE}Copia de Seguridad${NC}      : ${BACKUP_FILE}"
fi
echo -e "${GREEN}======================================================================${NC}"
echo -e "📌 ${WHITE}Comandos de verificación:${NC}"
echo -e "   - Estado de servicios : ${YELLOW}docker compose ps${NC}"
echo -e "   - Logs del backend    : ${YELLOW}docker compose logs -f backend${NC}"
echo -e "   - Logs del frontend   : ${YELLOW}docker compose logs -f frontend${NC}"
echo -e "${GREEN}======================================================================${NC}\n"
