#!/bin/bash
# SaaS Gestor - One Command Deploy
# Execute: sudo bash -c "$(curl -sSL https://your-url/deploy.sh)"
# OU: chmod +x deploy.sh && sudo ./deploy.sh

set -e

# ==========================================
# CONFIGURATION
# ==========================================
APP_NAME="saas-gestor"
APP_DIR="/opt/${APP_NAME}"
REPO_URL="${REPO_URL:-}"  # Opcional: URL do repositório Git
DOMAIN="${DOMAIN:-$(hostname -I | awk '{print $1}')}"  # IP da máquina

# Cores
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

# ==========================================
# FUNCTIONS
# ==========================================
print_banner() {
    clear
    echo -e "${CYAN}"
    echo "  ███████╗ █████╗  █████╗ ███████╗     ██████╗ ███████╗███████╗████████╗ ██████╗ ██████╗ "
    echo "  ██╔════╝██╔══██╗██╔══██╗██╔════╝    ██╔════╝ ██╔════╝██╔════╝╚══██╔══╝██╔═══██╗██╔══██╗"
    echo "  ███████╗███████║███████║███████╗    ██║  ███╗█████╗  ███████╗   ██║   ██║   ██║██████╔╝"
    echo "  ╚════██║██╔══██║██╔══██║╚════██║    ██║   ██║██╔══╝  ╚════██║   ██║   ██║   ██║██╔══██╗"
    echo "  ███████║██║  ██║██║  ██║███████║    ╚██████╔╝███████╗███████║   ██║   ╚██████╔╝██║  ██║"
    echo "  ╚══════╝╚═╝  ╚═╝╚═╝  ╚═╝╚══════╝     ╚═════╝ ╚══════╝╚══════╝   ╚═╝    ╚═════╝ ╚═╝  ╚═╝"
    echo -e "${NC}"
    echo -e "${BLUE}═══════════════════════════════════════════════════════════════════════════════${NC}"
    echo -e "${YELLOW}                    Deploy Automático - Ubuntu 24.04 LTS${NC}"
    echo -e "${BLUE}═══════════════════════════════════════════════════════════════════════════════${NC}"
    echo ""
}

print_step() {
    echo -e "${YELLOW}[PASSO $1/15]${NC} $2"
}

print_success() {
    echo -e "${GREEN}[✓]${NC} $1"
}

print_error() {
    echo -e "${RED}[✗]${NC} $1"
}

print_info() {
    echo -e "${BLUE}[i]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[!]${NC} $1"
}

# ==========================================
# STEP 1: CHECK ROOT
# ==========================================
check_root() {
    if [ "$EUID" -ne 0 ]; then 
        print_error "Este script precisa ser executado como ROOT"
        echo "Execute: sudo $0"
        exit 1
    fi
}

# ==========================================
# STEP 2: UPDATE SYSTEM
# ==========================================
update_system() {
    print_step "2" "Atualizando sistema..."
    apt-get update -qq > /dev/null 2>&1
    apt-get upgrade -y -qq > /dev/null 2>&1
    print_success "Sistema atualizado"
}

# ==========================================
# STEP 3: INSTALL DEPENDENCIES
# ==========================================
install_dependencies() {
    print_step "3" "Instalando dependências..."
    apt-get install -y -qq \
        apt-transport-https \
        ca-certificates \
        curl \
        gnupg \
        lsb-release \
        git \
        nginx \
        ufw \
        net-tools \
        software-properties-common \
        > /dev/null 2>&1
    print_success "Dependências instaladas"
}

# ==========================================
# STEP 4: INSTALL DOCKER
# ==========================================
install_docker() {
    print_step "4" "Instalando Docker..."
    
    if command -v docker &> /dev/null; then
        print_success "Docker já instalado ($(docker --version))"
        return
    fi
    
    # Remove old versions
    apt-get remove -y docker docker-engine docker.io containerd runc > /dev/null 2>&1 || true
    
    # Install Docker
    curl -fsSL https://get.docker.com | sh > /dev/null 2>&1
    
    # Enable and start
    systemctl enable docker > /dev/null 2>&1
    systemctl start docker
    
    # Add current user to docker group
    usermod -aG docker $SUDO_USER 2>/dev/null || true
    usermod -aG docker ubuntu 2>/dev/null || true
    
    print_success "Docker instalado ($(docker --version))"
}

# ==========================================
# STEP 5: INSTALL DOCKER COMPOSE
# ==========================================
install_compose() {
    print_step "5" "Instalando Docker Compose..."
    
    if command -v docker-compose &> /dev/null; then
        print_success "Docker Compose já instalado ($(docker-compose --version))"
        return
    fi
    
    curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" \
        -o /usr/local/bin/docker-compose \
        > /dev/null 2>&1
    chmod +x /usr/local/bin/docker-compose
    ln -sf /usr/local/bin/docker-compose /usr/bin/docker-compose
    
    print_success "Docker Compose instalado"
}

# ==========================================
# STEP 6: CREATE DIRECTORIES
# ==========================================
create_directories() {
    print_step "6" "Criando estrutura de diretórios..."
    
    mkdir -p ${APP_DIR}/{backend,frontend,data/{mysql-master,mysql-slave,redis,nginx,builds},backups,init-scripts}
    chmod -R 755 ${APP_DIR}
    
    print_success "Diretórios criados em ${APP_DIR}"
}

# ==========================================
# STEP 7: GENERATE PASSWORDS
# ==========================================
generate_passwords() {
    print_step "7" "Gerando senhas seguras..."
    
    DB_ROOT_PASS=$(openssl rand -base64 32 | tr -dc 'a-zA-Z0-9' | head -c 32)
    DB_APP_PASS=$(openssl rand -base64 32 | tr -dc 'a-zA-Z0-9' | head -c 32)
    REDIS_PASS=$(openssl rand -base64 32 | tr -dc 'a-zA-Z0-9' | head -c 32)
    JWT_SECRET=$(openssl rand -base64 48 | tr -dc 'a-zA-Z0-9' | head -c 48)
    JWT_REFRESH=$(openssl rand -base64 48 | tr -dc 'a-zA-Z0-9' | head -c 48)
    DB_ENC_KEY=$(openssl rand -base64 24 | tr -dc 'a-zA-Z0-9' | head -c 32)
    ENV_ENC_KEY=$(openssl rand -base64 24 | tr -dc 'a-zA-Z0-9' | head -c 32)
    
    print_success "Senhas geradas automaticamente"
}

# ==========================================
# STEP 8: CREATE ENV FILE
# ==========================================
create_env_file() {
    print_step "8" "Criando arquivo de configuração (.env)..."
    
    cat > ${APP_DIR}/.env << EOF
# SaaS Gestor - Environment Variables
# Gerado automaticamente em $(date)

# ==========================================
# Database Configuration
# ==========================================
DB_ROOT_PASSWORD=${DB_ROOT_PASS}
DB_APP_PASSWORD=${DB_APP_PASS}
DB_REPLICA_PASSWORD=${DB_APP_PASS}
DB_NAME=saas_gestor
DB_USER=saas_app

DB_MASTER_HOST=mariadb-master
DB_MASTER_PORT=3306
DB_SLAVE_HOST=mariadb-slave
DB_SLAVE_PORT=3306

# ==========================================
# Redis Configuration
# ==========================================
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_PASSWORD=${REDIS_PASS}

# ==========================================
# JWT Configuration
# ==========================================
JWT_SECRET=${JWT_SECRET}
JWT_REFRESH_SECRET=${JWT_REFRESH}
JWT_EXPIRATION=1h
JWT_REFRESH_EXPIRATION=7d

# ==========================================
# Encryption Keys (32 chars)
# ==========================================
DB_ENCRYPTION_KEY=${DB_ENC_KEY}
ENV_ENCRYPTION_KEY=${ENV_ENC_KEY}

# ==========================================
# Application
# ==========================================
NODE_ENV=production
PORT=3000
CORS_ORIGIN=http://${DOMAIN},http://${DOMAIN}:80,http://${DOMAIN}:3000
API_URL=http://${DOMAIN}:3000/api/v1

# ==========================================
# Frontend
# ==========================================
VITE_API_URL=http://${DOMAIN}:3000/api/v1

# ==========================================
# Domain
# ==========================================
DOMAIN=${DOMAIN}
EMAIL=admin@${DOMAIN}
EOF
    
    chmod 600 ${APP_DIR}/.env
    print_success "Arquivo .env criado"
}

# ==========================================
# STEP 9: CREATE DOCKER COMPOSE
# ==========================================
create_docker_compose() {
    print_step "9" "Criando Docker Compose..."
    
    cat > ${APP_DIR}/docker-compose.yml << 'EOF'
version: '3.8'

services:
  # MariaDB Master
  mariadb-master:
    image: mariadb:10.6
    container_name: saas-mariadb-master
    restart: unless-stopped
    env_file:
      - .env
    environment:
      MYSQL_ROOT_PASSWORD: ${DB_ROOT_PASSWORD}
      MYSQL_DATABASE: ${DB_NAME}
      MYSQL_USER: ${DB_USER}
      MYSQL_PASSWORD: ${DB_APP_PASSWORD}
    volumes:
      - ./data/mysql-master:/var/lib/mysql
      - ./init-scripts:/docker-entrypoint-initdb.d:ro
    ports:
      - "3306:3306"
    networks:
      - saas-network
    command: >
      --server-id=1
      --log-bin=mysql-bin
      --binlog-format=ROW
      --character-set-server=utf8mb4
      --collation-server=utf8mb4_unicode_ci
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost", "-p${DB_ROOT_PASSWORD}"]
      interval: 10s
      timeout: 5s
      retries: 5

  # MariaDB Slave
  mariadb-slave:
    image: mariadb:10.6
    container_name: saas-mariadb-slave
    restart: unless-stopped
    env_file:
      - .env
    environment:
      MYSQL_ROOT_PASSWORD: ${DB_ROOT_PASSWORD}
      MYSQL_DATABASE: ${DB_NAME}
    volumes:
      - ./data/mysql-slave:/var/lib/mysql
    ports:
      - "3307:3306"
    networks:
      - saas-network
    depends_on:
      - mariadb-master
    command: >
      --server-id=2
      --relay-log=mysql-relay-bin
      --read-only=1
      --character-set-server=utf8mb4
      --collation-server=utf8mb4_unicode_ci

  # Redis
  redis:
    image: redis:7-alpine
    container_name: saas-redis
    restart: unless-stopped
    env_file:
      - .env
    command: >
      redis-server
      --requirepass ${REDIS_PASSWORD}
      --appendonly yes
    volumes:
      - ./data/redis:/data
    ports:
      - "6379:6379"
    networks:
      - saas-network

  # Backend API
  backend:
    build:
      context: ./backend
      dockerfile: Dockerfile
    container_name: saas-backend
    restart: unless-stopped
    env_file:
      - .env
    environment:
      NODE_ENV: production
      PORT: 3000
    ports:
      - "3000:3000"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - ./data/builds:/tmp/builds
    networks:
      - saas-network
    depends_on:
      - mariadb-master
      - redis

  # Frontend
  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    container_name: saas-frontend
    restart: unless-stopped
    env_file:
      - .env
    ports:
      - "80:80"
    networks:
      - saas-network
    depends_on:
      - backend

networks:
  saas-network:
    driver: bridge
EOF
    
    print_success "Docker Compose criado"
}

# ==========================================
# STEP 10: CREATE INIT SCRIPTS
# ==========================================
create_init_scripts() {
    print_step "10" "Criando scripts de inicialização..."
    
    # Database init script
    cat > ${APP_DIR}/init-scripts/01-init.sql << 'EOF'
-- Create database and user
CREATE DATABASE IF NOT EXISTS saas_gestor CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Grant privileges
GRANT ALL PRIVILEGES ON saas_gestor.* TO 'saas_app'@'%';
FLUSH PRIVILEGES;

-- Create tables (will be auto-created by TypeORM)
EOF

    print_success "Scripts de inicialização criados"
}

# ==========================================
# STEP 11: CONFIGURE FIREWALL
# ==========================================
configure_firewall() {
    print_step "11" "Configurando firewall (UFW)..."
    
    ufw default deny incoming > /dev/null 2>&1
    ufw default allow outgoing > /dev/null 2>&1
    ufw allow 22/tcp > /dev/null 2>&1    # SSH
    ufw allow 80/tcp > /dev/null 2>&1    # HTTP
    ufw allow 443/tcp > /dev/null 2>&1   # HTTPS
    ufw allow 3000/tcp > /dev/null 2>&1  # API
    ufw allow 3306/tcp > /dev/null 2>&1  # MySQL Master
    ufw allow 3307/tcp > /dev/null 2>&1  # MySQL Slave
    ufw allow 6379/tcp > /dev/null 2>&1  # Redis
    
    ufw --force enable > /dev/null 2>&1
    
    print_success "Firewall configurado"
}

# ==========================================
# STEP 12: CREATE MANAGEMENT SCRIPTS
# ==========================================
create_scripts() {
    print_step "12" "Criando scripts de gerenciamento..."
    
    # Start script
    cat > ${APP_DIR}/start.sh << EOF
#!/bin/bash
cd ${APP_DIR}
echo -e "${BLUE}Iniciando SaaS Gestor...${NC}"
docker-compose up -d
echo -e "${GREEN}✓ Sistema iniciado!${NC}"
echo ""
echo "URLs de acesso:"
echo "  Frontend: http://${DOMAIN}"
echo "  API: http://${DOMAIN}:3000"
echo "  Docs: http://${DOMAIN}:3000/api/docs"
EOF

    # Stop script
    cat > ${APP_DIR}/stop.sh << 'EOF'
#!/bin/bash
cd /opt/saas-gestor
echo "Parando SaaS Gestor..."
docker-compose down
echo "✓ Sistema parado"
EOF

    # Logs script
    cat > ${APP_DIR}/logs.sh << 'EOF'
#!/bin/bash
cd /opt/saas-gestor
if [ -z "$1" ]; then
    docker-compose logs -f --tail=100
else
    docker-compose logs -f --tail=100 "$1"
fi
EOF

    # Status script
    cat > ${APP_DIR}/status.sh << 'EOF'
#!/bin/bash
echo "=========================================="
echo "SaaS Gestor - Status do Sistema"
echo "=========================================="
echo ""
cd /opt/saas-gestor
docker-compose ps
echo ""
echo "Uso de Recursos:"
docker stats --no-stream --format "table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}"
echo ""
echo "Uso de Disco:"
df -h /opt/saas-gestor
EOF

    # Backup script
    cat > ${APP_DIR}/backup.sh << EOF
#!/bin/bash
BACKUP_DIR="${APP_DIR}/backups"
DATE=\$(date +%Y%m%d_%H%M%S)
mkdir -p \$BACKUP_DIR

echo "Criando backup..."
docker exec saas-mariadb-master mysqldump -u root -p${DB_ROOT_PASS} saas_gestor > \$BACKUP_DIR/db_\$DATE.sql
docker exec saas-redis redis-cli SAVE
docker cp saas-redis:/data/dump.rdb \$BACKUP_DIR/redis_\$DATE.rdb

tar -czf \$BACKUP_DIR/backup_\$DATE.tar.gz -C \$BACKUP_DIR db_\$DATE.sql redis_\$DATE.rdb
rm \$BACKUP_DIR/db_\$DATE.sql \$BACKUP_DIR/redis_\$DATE.rdb

echo "✓ Backup criado: \$BACKUP_DIR/backup_\$DATE.tar.gz"
EOF

    # Make executable
    chmod +x ${APP_DIR}/*.sh
    
    print_success "Scripts criados"
}

# ==========================================
# STEP 13: CREATE SYSTEMD SERVICE
# ==========================================
create_systemd_service() {
    print_step "13" "Criando serviço systemd..."
    
    cat > /etc/systemd/system/${APP_NAME}.service << EOF
[Unit]
Description=SaaS Gestor Application
Requires=docker.service
After=docker.service network.target

[Service]
Type=oneshot
RemainAfterExit=yes
WorkingDirectory=${APP_DIR}
ExecStart=${APP_DIR}/start.sh
ExecStop=${APP_DIR}/stop.sh
TimeoutStartSec=0

[Install]
WantedBy=multi-user.target
EOF

    systemctl daemon-reload > /dev/null 2>&1
    systemctl enable ${APP_NAME} > /dev/null 2>&1
    
    print_success "Serviço systemd criado"
}

# ==========================================
# STEP 14: CLONE OR CREATE PROJECT
# ==========================================
setup_project() {
    print_step "14" "Configurando projeto..."
    
    if [ -n "$REPO_URL" ]; then
        print_info "Clonando repositório: $REPO_URL"
        cd ${APP_DIR}
        git clone $REPO_URL temp_repo > /dev/null 2>&1 || true
        if [ -d "temp_repo/backend" ]; then
            cp -r temp_repo/backend/* backend/ 2>/dev/null || true
        fi
        if [ -d "temp_repo/frontend" ]; then
            cp -r temp_repo/frontend/* frontend/ 2>/dev/null || true
        fi
        rm -rf temp_repo
        print_success "Projeto clonado"
    else
        print_info "Repositório não especificado. Criando estrutura básica..."
        
        # Create basic backend structure
        mkdir -p ${APP_DIR}/backend/src
        cat > ${APP_DIR}/backend/Dockerfile << 'EOF'
FROM node:18-alpine
WORKDIR /app
COPY package*.json ./
RUN npm ci --only=production
COPY . .
EXPOSE 3000
CMD ["npm", "run", "start:prod"]
EOF

        # Create basic frontend structure
        mkdir -p ${APP_DIR}/frontend/src
        cat > ${APP_DIR}/frontend/Dockerfile << 'EOF'
FROM node:18-alpine AS builder
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=builder /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
EOF

        cat > ${APP_DIR}/frontend/nginx.conf << 'EOF'
server {
    listen 80;
    server_name localhost;
    root /usr/share/nginx/html;
    index index.html;
    
    location / {
        try_files $uri $uri/ /index.html;
    }
}
EOF
        
        print_warning "Estrutura básica criada. Você precisa copiar seu código!"
    fi
}

# ==========================================
# STEP 15: START SERVICES
# ==========================================
start_services() {
    print_step "15" "Iniciando serviços..."
    
    cd ${APP_DIR}
    
    # Pull images
    print_info "Baixando imagens Docker..."
    docker-compose pull > /dev/null 2>&1
    
    # Start infrastructure first
    print_info "Iniciando infraestrutura (MariaDB, Redis)..."
    docker-compose up -d mariadb-master mariadb-slave redis > /dev/null 2>&1
    
    # Wait for databases
    print_info "Aguardando banco de dados..."
    sleep 10
    
    # Check if backend has code
    if [ -f "${APP_DIR}/backend/package.json" ]; then
        print_info "Iniciando backend e frontend..."
        docker-compose up -d backend frontend > /dev/null 2>&1 || true
    else
        print_warning "Backend não configurado. Inicie manualmente após copiar o código."
    fi
    
    print_success "Serviços iniciados"
}

# ==========================================
# SHOW FINAL INFO
# ==========================================
show_final_info() {
    echo ""
    echo -e "${GREEN}═══════════════════════════════════════════════════════════════════════════════${NC}"
    echo -e "${GREEN}                         DEPLOY CONCLUÍDO COM SUCESSO!${NC}"
    echo -e "${GREEN}═══════════════════════════════════════════════════════════════════════════════${NC}"
    echo ""
    echo -e "${CYAN}📁 Diretório da Aplicação:${NC} ${APP_DIR}"
    echo -e "${CYAN}🌐 Acesse o sistema:${NC}"
    echo -e "   • Frontend: ${GREEN}http://${DOMAIN}${NC}"
    echo -e "   • API: ${GREEN}http://${DOMAIN}:3000${NC}"
    echo -e "   • Documentação: ${GREEN}http://${DOMAIN}:3000/api/docs${NC}"
    echo ""
    echo -e "${YELLOW}🔐 Credenciais geradas automaticamente:${NC}"
    echo -e "   Arquivo: ${APP_DIR}/.env"
    echo -e "   Senha Root DB: ${GREEN}${DB_ROOT_PASS}${NC}"
    echo -e "   Senha App DB: ${GREEN}${DB_APP_PASS}${NC}"
    echo ""
    echo -e "${BLUE}⚙️  Comandos úteis:${NC}"
    echo -e "   ${APP_DIR}/start.sh      # Iniciar sistema"
    echo -e "   ${APP_DIR}/stop.sh       # Parar sistema"
    echo -e "   ${APP_DIR}/logs.sh       # Ver logs"
    echo -e "   ${APP_DIR}/status.sh     # Status do sistema"
    echo -e "   ${APP_DIR}/backup.sh     # Backup"
    echo ""
    echo -e "${BLUE}🐳 Comandos Docker:${NC}"
    echo -e "   cd ${APP_DIR}"
    echo -e "   docker-compose ps        # Listar containers"
    echo -e "   docker-compose logs -f   # Ver logs"
    echo -e "   docker-compose down      # Parar tudo"
    echo -e "   docker-compose up -d     # Iniciar tudo"
    echo ""
    echo -e "${YELLOW}⚠️  IMPORTANTE:${NC}"
    if [ ! -f "${APP_DIR}/backend/package.json" ]; then
        echo -e "   ${RED}Você precisa copiar o código do backend e frontend!${NC}"
        echo -e "   Exemplo: scp -r backend/* root@${DOMAIN}:${APP_DIR}/backend/"
    fi
    echo -e "   Salve as senhas em um local seguro!"
    echo ""
    echo -e "${GREEN}═══════════════════════════════════════════════════════════════════════════════${NC}"
    echo ""
}

# ==========================================
# MAIN EXECUTION
# ==========================================
main() {
    print_banner
    check_root
    
    echo -e "${BLUE}Iniciando deploy automático...${NC}"
    echo ""
    
    update_system
    install_dependencies
    install_docker
    install_compose
    create_directories
    generate_passwords
    create_env_file
    create_docker_compose
    create_init_scripts
    configure_firewall
    create_scripts
    create_systemd_service
    setup_project
    start_services
    
    show_final_info
}

# Run main function
main
