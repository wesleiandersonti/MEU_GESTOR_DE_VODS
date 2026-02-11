#!/bin/bash
#
# Script de Instalação - Sistema Gestor SaaS
# Ubuntu Server 22.04 LTS
# 
# Uso: sudo ./install-ubuntu.sh
#

set -e  # Parar em caso de erro

echo "=========================================="
echo "  INSTALAÇÃO SISTEMA GESTOR SAAS"
echo "  Ubuntu 22.04 LTS"
echo "=========================================="
echo ""

# Cores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Funções de log
log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Verificar se é root
if [[ $EUID -ne 0 ]]; then
   log_error "Este script deve ser executado como root (sudo)"
   exit 1
fi

# Verificar Ubuntu 22.04
if ! grep -q "22.04" /etc/os-release; then
    log_warn "Este script foi testado no Ubuntu 22.04. Sua versão pode ser diferente."
    read -p "Deseja continuar mesmo assim? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

log_info "Iniciando instalação..."

# ============================================
# ETAPA 1: ATUALIZAÇÃO DO SISTEMA
# ============================================
log_info "Etapa 1/10: Atualizando sistema..."

apt-get update
apt-get upgrade -y
apt-get install -y \
    apt-transport-https \
    ca-certificates \
    curl \
    gnupg \
    lsb-release \
    software-properties-common \
    git \
    htop \
    net-tools \
    ufw \
    fail2ban

# ============================================
# ETAPA 2: INSTALAÇÃO DOCKER
# ============================================
log_info "Etapa 2/10: Instalando Docker..."

# Remover versões antigas
apt-get remove -y docker docker-engine docker.io containerd runc 2>/dev/null || true

# Adicionar repositório Docker oficial
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/ubuntu \
  $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null

apt-get update
apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin

# Adicionar usuário ao grupo docker (se executado via sudo)
if [ -n "$SUDO_USER" ]; then
    usermod -aG docker "$SUDO_USER"
    log_info "Usuário $SUDO_USER adicionado ao grupo docker"
fi

# Iniciar Docker
systemctl enable docker
systemctl start docker

# ============================================
# ETAPA 3: INSTALAÇÃO DOCKER COMPOSE
# ============================================
log_info "Etapa 3/10: Instalando Docker Compose..."

# Docker Compose V2 já vem com o plugin, mas vamos garantir
apt-get install -y docker-compose

# ============================================
# ETAPA 4: CONFIGURAÇÃO DE REDE E FIREWALL
# ============================================
log_info "Etapa 4/10: Configurando firewall..."

# Configurar UFW
ufw --force reset
ufw default deny incoming
ufw default allow outgoing

# Permitir SSH (importante!)
ufw allow ssh

# Permitir HTTP e HTTPS
ufw allow 80/tcp
ufw allow 443/tcp

# Permitir portas dos serviços (apenas se necessário externamente)
# ufw allow 3306/tcp  # MariaDB - NÃO RECOMENDADO, usar apenas internamente
# ufw allow 6379/tcp  # Redis - NÃO RECOMENDADO, usar apenas internamente

# Habilitar firewall
ufw --force enable

log_info "Firewall configurado. Portas abertas: 22 (SSH), 80 (HTTP), 443 (HTTPS)"

# ============================================
# ETAPA 5: CRIAR ESTRUTURA DE DIRETÓRIOS
# ============================================
log_info "Etapa 5/10: Criando estrutura de diretórios..."

APP_DIR="/opt/saas-gestor"
mkdir -p "$APP_DIR"/{backend,frontend,nginx,scripts,data/{mariadb,redis,minio,logs,backups},backups/{daily,weekly,monthly}}

# Criar usuário dedicado (se não existir)
if ! id -u saas-gestor &>/dev/null; then
    useradd -r -s /bin/false -d "$APP_DIR" saas-gestor
    log_info "Usuário saas-gestor criado"
fi

# Permissões
chown -R saas-gestor:saas-gestor "$APP_DIR"
chmod 750 "$APP_DIR"

# ============================================
# ETAPA 6: GERAR SENHAS ALEATÓRIAS
# ============================================
log_info "Etapa 6/10: Gerando credenciais seguras..."

generate_password() {
    openssl rand -base64 32 | tr -d "=+/" | cut -c1-25
}

# Gerar senhas
DB_ROOT_PASSWORD=$(generate_password)
DB_APP_PASSWORD=$(generate_password)
DB_USER_PASSWORD=$(generate_password)
DB_REPLICA_PASSWORD=$(generate_password)
REDIS_PASSWORD=$(generate_password)
MINIO_ROOT_USER="saas-admin"
MINIO_ROOT_PASSWORD=$(generate_password)
JWT_SECRET=$(generate_password)
ENCRYPTION_KEY=$(openssl rand -base64 32)

# Salvar credenciais
CREDENTIALS_FILE="$APP_DIR/.credentials.env"
cat > "$CREDENTIALS_FILE" << EOF
# CREDENCIAIS DO SISTEMA - GUARDE EM LOCAL SEGURO!
# Gerado em: $(date)

# MariaDB Master
DB_ROOT_PASSWORD=$DB_ROOT_PASSWORD
DB_APP_PASSWORD=$DB_APP_PASSWORD
DB_USER_PASSWORD=$DB_USER_PASSWORD

# MariaDB Replicação
DB_REPLICA_PASSWORD=$DB_REPLICA_PASSWORD

# Redis
REDIS_PASSWORD=$REDIS_PASSWORD

# MinIO
MINIO_ROOT_USER=$MINIO_ROOT_USER
MINIO_ROOT_PASSWORD=$MINIO_ROOT_PASSWORD

# Segurança
JWT_SECRET=$JWT_SECRET
ENCRYPTION_KEY=$ENCRYPTION_KEY
EOF

chmod 600 "$CREDENTIALS_FILE"
chown saas-gestor:saas-gestor "$CREDENTIALS_FILE"

log_info "Credenciais geradas e salvas em $CREDENTIALS_FILE"
log_warn "⚠️  GUARDE ESTE ARQUIVO EM LOCAL SEGURO!"

# ============================================
# ETAPA 7: CRIAR DOCKER-COMPOSE
# ============================================
log_info "Etapa 7/10: Criando docker-compose.yml..."

cat > "$APP_DIR/docker-compose.yml" << 'EOF'
version: '3.8'

services:
  # ==========================================
  # MARIADB MASTER - Escritas
  # ==========================================
  mariadb-master:
    image: mariadb:10.6
    container_name: saas-mariadb-master
    restart: unless-stopped
    environment:
      MYSQL_ROOT_PASSWORD: ${DB_ROOT_PASSWORD}
      MYSQL_DATABASE: saas_gestor
      MYSQL_USER: saas_app
      MYSQL_PASSWORD: ${DB_APP_PASSWORD}
      TZ: America/Sao_Paulo
    volumes:
      - ./data/mariadb-master:/var/lib/mysql
      - ./init-scripts:/docker-entrypoint-initdb.d:ro
      - ./backups:/backups
    ports:
      - "127.0.0.1:3306:3306"  # Apenas localhost
    networks:
      - saas-network
    command: >
      --server-id=1
      --log-bin=mysql-bin
      --binlog-format=ROW
      --binlog-row-image=FULL
      --expire-logs-days=7
      --max-binlog-size=100M
      --character-set-server=utf8mb4
      --collation-server=utf8mb4_unicode_ci
      --innodb-buffer-pool-size=1G
      --max-connections=200
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost", "-u", "root", "-p${DB_ROOT_PASSWORD}"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s

  # ==========================================
  # MARIADB SLAVE - Leituras e Replicação
  # ==========================================
  mariadb-slave:
    image: mariadb:10.6
    container_name: saas-mariadb-slave
    restart: unless-stopped
    environment:
      MYSQL_ROOT_PASSWORD: ${DB_ROOT_PASSWORD}
      MYSQL_DATABASE: saas_gestor
      TZ: America/Sao_Paulo
    volumes:
      - ./data/mariadb-slave:/var/lib/mysql
      - ./scripts/init-slave.sh:/init-slave.sh:ro
    ports:
      - "127.0.0.1:3307:3306"  # Apenas localhost - Slave na porta 3307
    networks:
      - saas-network
    depends_on:
      mariadb-master:
        condition: service_healthy
    command: >
      --server-id=2
      --relay-log=mysql-relay-bin
      --relay-log-recovery=1
      --read-only=1
      --log-bin=mysql-bin
      --character-set-server=utf8mb4
      --collation-server=utf8mb4_unicode_ci
      --innodb-buffer-pool-size=1G
      --max-connections=200
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost", "-u", "root", "-p${DB_ROOT_PASSWORD}"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s

  # ==========================================
  # Redis - Cache e sessões
  # ==========================================
  redis:
    image: redis:7-alpine
    container_name: saas-redis
    restart: unless-stopped
    command: >
      redis-server
      --requirepass ${REDIS_PASSWORD}
      --maxmemory 512mb
      --maxmemory-policy allkeys-lru
      --appendonly yes
    volumes:
      - ./data/redis:/data
    ports:
      - "127.0.0.1:6379:6379"  # Apenas localhost
    networks:
      - saas-network
    healthcheck:
      test: ["CMD", "redis-cli", "-a", "${REDIS_PASSWORD}", "ping"]
      interval: 30s
      timeout: 10s
      retries: 3

  # ==========================================
  # MinIO - Storage de arquivos (S3-compatible)
  # ==========================================
  minio:
    image: minio/minio:latest
    container_name: saas-minio
    restart: unless-stopped
    environment:
      MINIO_ROOT_USER: ${MINIO_ROOT_USER}
      MINIO_ROOT_PASSWORD: ${MINIO_ROOT_PASSWORD}
    volumes:
      - ./data/minio:/data
    ports:
      - "9000:9000"   # API S3
      - "9001:9001"   # Console Web
    networks:
      - saas-network
    command: server /data --console-address ":9001"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:9000/minio/health/live"]
      interval: 30s
      timeout: 20s
      retries: 3

  # ==========================================
  # Backend API (NestJS)
  # ==========================================
  backend:
    build:
      context: ./backend
      dockerfile: Dockerfile
    container_name: saas-backend
    restart: unless-stopped
    environment:
      NODE_ENV: production
      PORT: 3000
      # Master (Escritas)
      DB_MASTER_HOST: mariadb-master
      DB_MASTER_PORT: 3306
      # Slave (Leituras)
      DB_SLAVE_HOST: mariadb-slave
      DB_SLAVE_PORT: 3306
      # Conexão padrão (Master para compatibilidade)
      DB_HOST: mariadb-master
      DB_PORT: 3306
      DB_NAME: saas_gestor
      DB_USER: saas_app
      DB_PASSWORD: ${DB_APP_PASSWORD}
      REDIS_HOST: redis
      REDIS_PORT: 6379
      REDIS_PASSWORD: ${REDIS_PASSWORD}
      MINIO_ENDPOINT: minio
      MINIO_PORT: 9000
      MINIO_USE_SSL: "false"
      MINIO_ACCESS_KEY: ${MINIO_ROOT_USER}
      MINIO_SECRET_KEY: ${MINIO_ROOT_PASSWORD}
      JWT_SECRET: ${JWT_SECRET}
      ENCRYPTION_KEY: ${ENCRYPTION_KEY}
      TZ: America/Sao_Paulo
    volumes:
      - ./data/app_uploads:/app/uploads
      - ./data/app_logs:/app/logs
    ports:
      - "127.0.0.1:3000:3000"  # Apenas localhost (nginx faz proxy)
    networks:
      - saas-network
    depends_on:
      mariadb-master:
        condition: service_healthy
      mariadb-slave:
        condition: service_healthy
      redis:
        condition: service_healthy
      minio:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:3000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s

  # ==========================================
  # Frontend (React)
  # ==========================================
  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    container_name: saas-frontend
    restart: unless-stopped
    environment:
      REACT_APP_API_URL: /api
      REACT_APP_WS_URL: /dashboard
    networks:
      - saas-network
    depends_on:
      - backend

  # ==========================================
  # Nginx - Reverse Proxy
  # ==========================================
  nginx:
    image: nginx:alpine
    container_name: saas-nginx
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/ssl:/etc/nginx/ssl:ro
      - ./data/app_logs/nginx:/var/log/nginx
    networks:
      - saas-network
    depends_on:
      - backend
      - frontend

  # ==========================================
  # Worker - Processamento em background
  # ==========================================
  worker:
    build:
      context: ./backend
      dockerfile: Dockerfile
    container_name: saas-worker
    restart: unless-stopped
    command: npm run start:worker
    environment:
      NODE_ENV: production
      # Master (Escritas)
      DB_MASTER_HOST: mariadb-master
      DB_MASTER_PORT: 3306
      # Slave (Leituras)
      DB_SLAVE_HOST: mariadb-slave
      DB_SLAVE_PORT: 3306
      # Conexão padrão
      DB_HOST: mariadb-master
      DB_PORT: 3306
      DB_NAME: saas_gestor
      DB_USER: saas_app
      DB_PASSWORD: ${DB_APP_PASSWORD}
      REDIS_HOST: redis
      REDIS_PORT: 6379
      REDIS_PASSWORD: ${REDIS_PASSWORD}
      MINIO_ENDPOINT: minio
      MINIO_PORT: 9000
      MINIO_USE_SSL: "false"
      MINIO_ACCESS_KEY: ${MINIO_ROOT_USER}
      MINIO_SECRET_KEY: ${MINIO_ROOT_PASSWORD}
      JWT_SECRET: ${JWT_SECRET}
      ENCRYPTION_KEY: ${ENCRYPTION_KEY}
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro  # Para builds Docker
      - ./data/app_uploads:/app/uploads
      - ./data/app_logs:/app/logs
    networks:
      - saas-network
    depends_on:
      - mariadb-master
      - mariadb-slave
      - redis
      - minio

  # ==========================================
  # Prometheus - Métricas
  # ==========================================
  prometheus:
    image: prom/prometheus:latest
    container_name: saas-prometheus
    restart: unless-stopped
    volumes:
      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - ./data/prometheus:/prometheus
    ports:
      - "127.0.0.1:9090:9090"
    networks:
      - saas-network
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'

networks:
  saas-network:
    driver: bridge
EOF

chown saas-gestor:saas-gestor "$APP_DIR/docker-compose.yml"

# ============================================
# ETAPA 8: CRIAR ARQUIVO .ENV
# ============================================
log_info "Etapa 8/10: Criando arquivo .env..."

cat > "$APP_DIR/.env" << EOF
# Sistema Gestor SaaS - Environment

# MariaDB Master
DB_ROOT_PASSWORD=$DB_ROOT_PASSWORD
DB_APP_PASSWORD=$DB_APP_PASSWORD
DB_USER_PASSWORD=$DB_USER_PASSWORD

# MariaDB Replicação
DB_REPLICA_PASSWORD=$DB_REPLICA_PASSWORD

# Redis
REDIS_PASSWORD=$REDIS_PASSWORD

# MinIO
MINIO_ROOT_USER=$MINIO_ROOT_USER
MINIO_ROOT_PASSWORD=$MINIO_ROOT_PASSWORD

# Segurança
JWT_SECRET=$JWT_SECRET
ENCRYPTION_KEY=$ENCRYPTION_KEY

# Configurações gerais
TZ=America/Sao_Paulo
NODE_ENV=production
EOF

chown saas-gestor:saas-gestor "$APP_DIR/.env"
chmod 600 "$APP_DIR/.env"

# ============================================
# ETAPA 9: CRIAR SCRIPT DE BACKUP
# ============================================
log_info "Etapa 9/10: Criando scripts utilitários..."

cat > "$APP_DIR/scripts/backup.sh" << 'EOF'
#!/bin/bash
# Script de backup diário
# Usa o SLAVE para não impactar o MASTER

set -e

BACKUP_DIR="/opt/saas-gestor/backups/daily"
DATE=$(date +%Y%m%d_%H%M%S)
RETENTION_DAYS=7

# Criar diretório se não existir
mkdir -p "$BACKUP_DIR"

# Backup do banco de dados (sempre no SLAVE)
echo "Fazendo backup do MariaDB (Slave)..."
docker exec saas-mariadb-slave mysqldump -u root -p"${DB_ROOT_PASSWORD}" \
  --all-databases \
  --single-transaction \
  --routines \
  --triggers \
  > "$BACKUP_DIR/mariadb_$DATE.sql"
gzip "$BACKUP_DIR/mariadb_$DATE.sql"

# Backup dos arquivos
echo "Fazendo backup dos arquivos..."
tar -czf "$BACKUP_DIR/files_$DATE.tar.gz" -C /opt/saas-gestor/data/app_uploads .

# Remover backups antigos
echo "Removendo backups com mais de $RETENTION_DAYS dias..."
find "$BACKUP_DIR" -name "*.gz" -type f -mtime +$RETENTION_DAYS -delete

echo "Backup concluído: $DATE"
echo "Arquivos:"
ls -lh "$BACKUP_DIR"/*$DATE*
EOF

chmod +x "$APP_DIR/scripts/backup.sh"

# Script de deploy
cat > "$APP_DIR/scripts/deploy.sh" << 'EOF'
#!/bin/bash
# Script de deploy

set -e

APP_DIR="/opt/saas-gestor"
cd "$APP_DIR"

echo "Atualizando código..."
git pull origin main

echo "Reconstruindo containers..."
docker-compose -f "$APP_DIR/docker-compose.yml" build

echo "Iniciando novo deploy..."
docker-compose -f "$APP_DIR/docker-compose.yml" up -d

echo "Limpeza..."
docker system prune -f

echo "Deploy concluído!"
EOF

chmod +x "$APP_DIR/scripts/deploy.sh"

# Script de inicialização do Slave
cat > "$APP_DIR/scripts/init-slave.sh" << 'EOF'
#!/bin/bash
# Script de inicialização da replicação Master-Slave

set -e

echo "Aguardando Master ficar pronto..."
sleep 10

# Aguardar Master ficar disponível
until docker exec saas-mariadb-master mysqladmin ping -h localhost -u root -p"${DB_ROOT_PASSWORD}" --silent; do
  echo "Aguardando Master..."
  sleep 5
done

echo "Criando usuário de replicação no Master..."
docker exec saas-mariadb-master mysql -u root -p"${DB_ROOT_PASSWORD}" -e "
CREATE USER IF NOT EXISTS 'replica'@'%' IDENTIFIED BY '${DB_REPLICA_PASSWORD:-replica_password}';
GRANT REPLICATION SLAVE ON *.* TO 'replica'@'%';
FLUSH PRIVILEGES;
"

echo "Obtendo posição do binlog..."
MASTER_STATUS=$(docker exec saas-mariadb-master mysql -u root -p"${DB_ROOT_PASSWORD}" -e "SHOW MASTER STATUS\G")
MASTER_LOG_FILE=$(echo "$MASTER_STATUS" | grep "File:" | awk '{print $2}')
MASTER_LOG_POS=$(echo "$MASTER_STATUS" | grep "Position:" | awk '{print $2}')

echo "Master: $MASTER_LOG_FILE:$MASTER_LOG_POS"

echo "Configurando Slave..."
docker exec saas-mariadb-slave mysql -u root -p"${DB_ROOT_PASSWORD}" -e "
STOP SLAVE;
RESET SLAVE ALL;
CHANGE MASTER TO
  MASTER_HOST = 'saas-mariadb-master',
  MASTER_PORT = 3306,
  MASTER_USER = 'replica',
  MASTER_PASSWORD = '${DB_REPLICA_PASSWORD:-replica_password}',
  MASTER_LOG_FILE = '${MASTER_LOG_FILE}',
  MASTER_LOG_POS = ${MASTER_LOG_POS};
START SLAVE;
"

echo "Verificando status da replicação..."
docker exec saas-mariadb-slave mysql -u root -p"${DB_ROOT_PASSWORD}" -e "SHOW SLAVE STATUS\G" | grep -E "(Slave_IO_Running|Slave_SQL_Running|Seconds_Behind_Master)"

echo "✅ Replicação configurada com sucesso!"
EOF

chmod +x "$APP_DIR/scripts/init-slave.sh"

# Script de monitoramento da replicação
cat > "$APP_DIR/scripts/check-replication.sh" << 'EOF'
#!/bin/bash
# Verificar status da replicação

echo "Status da Replicação Master-Slave"
echo "=================================="
echo ""

echo "Master Status:"
docker exec saas-mariadb-master mysql -u root -p"${DB_ROOT_PASSWORD}" -e "SHOW MASTER STATUS\G" 2>/dev/null | grep -E "(File|Position)"

echo ""
echo "Slave Status:"
docker exec saas-mariadb-slave mysql -u root -p"${DB_ROOT_PASSWORD}" -e "SHOW SLAVE STATUS\G" 2>/dev/null | grep -E "(Slave_IO_Running|Slave_SQL_Running|Seconds_Behind_Master|Last_IO_Error|Last_SQL_Error)"

echo ""
echo "Lag de Replicação:"
docker exec saas-mariadb-slave mysql -u root -p"${DB_ROOT_PASSWORD}" -e "
SELECT 
  NOW() as current_time,
  TIMESTAMPDIFF(SECOND, 
    (SELECT MAX(ts) FROM mysql.gtid_slave_pos),
    NOW()
  ) as seconds_behind_master;
" 2>/dev/null || echo "Não foi possível obter lag"
EOF

chmod +x "$APP_DIR/scripts/check-replication.sh"

chown -R saas-gestor:saas-gestor "$APP_DIR/scripts"

# ============================================
# ETAPA 10: CONFIGURAÇÃO FINAL
# ============================================
log_info "Etapa 10/10: Configurações finais..."

# Criar logrotate para logs
cat > /etc/logrotate.d/saas-gestor << 'EOF'
/opt/saas-gestor/data/app_logs/*.log {
    daily
    rotate 14
    compress
    delaycompress
    missingok
    notifempty
    create 644 saas-gestor saas-gestor
    sharedscripts
    postrotate
        /usr/bin/docker kill --signal="SIGUSR1" saas-backend 2>/dev/null || true
    endscript
}
EOF

# Configurar fail2ban
cat > /etc/fail2ban/jail.local << EOF
[DEFAULT]
bantime = 3600
findtime = 600
maxretry = 5

[sshd]
enabled = true
port = ssh
filter = sshd
logpath = /var/log/auth.log
maxretry = 3

[nginx-limit-req]
enabled = true
filter = nginx-limit-req
action = iptables-multiport[name=ReqLimit, port="http,https", protocol=tcp]
logpath = /opt/saas-gestor/data/app_logs/nginx/error.log
EOF

systemctl restart fail2ban

# Configurar cron para backup diário
(crontab -l 2>/dev/null; echo "0 2 * * * /opt/saas-gestor/scripts/backup.sh >> /var/log/saas-gestor-backup.log 2>&1") | crontab -

# ============================================
# RESUMO
# ============================================
echo ""
echo "=========================================="
echo "  ✅ INSTALAÇÃO CONCLUÍDA!"
echo "=========================================="
echo ""
echo "📂 Diretório da aplicação: /opt/saas-gestor"
echo "👤 Usuário do sistema: saas-gestor"
echo ""
echo "🔐 Credenciais salvas em: /opt/saas-gestor/.credentials.env"
echo "⚠️  IMPORTANTE: Copie este arquivo para um local seguro!"
echo ""
echo "📋 Próximos passos:"
echo "1. Copie os códigos backend e frontend para /opt/saas-gestor/"
echo "2. Configure o nginx em /opt/saas-gestor/nginx/nginx.conf"
echo "3. Execute: cd /opt/saas-gestor && docker-compose up -d"
echo "4. Configure a replicação: ./scripts/init-slave.sh"
echo "5. Acesse: http://$(hostname -I | awk '{print $1}')"
echo ""
echo "🗄️  MariaDB Master-Slave:"
echo "   • Master (Escritas): localhost:3306"
echo "   • Slave (Leituras):  localhost:3307"
echo "   • Verificar status:  ./scripts/check-replication.sh"
echo ""
echo "🌐 MinIO Console: http://$(hostname -I | awk '{print $1}'):9001"
echo "   Usuário: $MINIO_ROOT_USER"
echo "   Senha: $MINIO_ROOT_PASSWORD"
echo ""
echo "📊 Monitoramento:"
echo "   - Prometheus: http://$(hostname -I | awk '{print $1}'):9090"
echo ""
echo "=========================================="
echo ""

# Mostrar credenciais
log_warn "CREDENCIAIS GERADAS (SALVE ESTAS INFORMAÇÕES):"
echo ""
echo "MariaDB Root Password: $DB_ROOT_PASSWORD"
echo "MariaDB App Password: $DB_APP_PASSWORD"
echo "Redis Password: $REDIS_PASSWORD"
echo "MinIO User: $MINIO_ROOT_USER"
echo "MinIO Password: $MINIO_ROOT_PASSWORD"
echo ""
