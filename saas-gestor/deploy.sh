#!/bin/bash
# Deploy script for SaaS Gestor on Ubuntu 24.04
# Run as root

set -e

echo "=========================================="
echo "SaaS Gestor - Deploy Script"
echo "Ubuntu 24.04 LTS"
echo "=========================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
APP_DIR="/opt/saas-gestor"
DOMAIN="${DOMAIN:-localhost}"
EMAIL="${EMAIL:-admin@localhost}"

# Function to print status
print_status() {
    echo -e "${GREEN}[✓]${NC} $1"
}

print_error() {
    echo -e "${RED}[✗]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[!]${NC} $1"
}

# Check if running as root
if [ "$EUID" -ne 0 ]; then 
    print_error "Please run as root (use sudo)"
    exit 1
fi

# Update system
echo ""
echo "Step 1: Updating system packages..."
apt-get update -y
apt-get upgrade -y
print_status "System updated"

# Install required packages
echo ""
echo "Step 2: Installing required packages..."
apt-get install -y \
    apt-transport-https \
    ca-certificates \
    curl \
    gnupg \
    lsb-release \
    git \
    nginx \
    certbot \
    python3-certbot-nginx \
    ufw \
    htop \
    net-tools \
    software-properties-common

print_status "Packages installed"

# Install Docker
echo ""
echo "Step 3: Installing Docker..."
if ! command -v docker &> /dev/null; then
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null
    apt-get update -y
    apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
    systemctl enable docker
    systemctl start docker
    print_status "Docker installed"
else
    print_status "Docker already installed"
fi

# Install Docker Compose
echo ""
echo "Step 4: Installing Docker Compose..."
if ! command -v docker-compose &> /dev/null; then
    curl -L "https://github.com/docker/compose/releases/download/v2.24.0/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
    chmod +x /usr/local/bin/docker-compose
    ln -sf /usr/local/bin/docker-compose /usr/bin/docker-compose
    print_status "Docker Compose installed"
else
    print_status "Docker Compose already installed"
fi

# Create application directory
echo ""
echo "Step 5: Creating application directory..."
mkdir -p $APP_DIR
mkdir -p $APP_DIR/data/mysql-master
mkdir -p $APP_DIR/data/mysql-slave
mkdir -p $APP_DIR/data/redis
mkdir -p $APP_DIR/data/nginx
mkdir -p $APP_DIR/backups
print_status "Directories created"

# Setup firewall
echo ""
echo "Step 6: Configuring firewall..."
ufw default deny incoming
ufw default allow outgoing
ufw allow ssh
ufw allow 80/tcp
ufw allow 443/tcp
ufw allow 3000/tcp  # Backend API
ufw allow 5173/tcp  # Frontend dev (optional)
ufw --force enable
print_status "Firewall configured"

# Create .env file
echo ""
echo "Step 7: Creating environment configuration..."
cat > $APP_DIR/.env << 'EOF'
# Database Configuration
DB_ROOT_PASSWORD=root_password_change_me
DB_DATABASE=saas_gestor
DB_USERNAME=saas_user
DB_PASSWORD=saas_password_change_me

# Master DB
DB_HOST_MASTER=mysql-master
DB_PORT_MASTER=3306

# Slave DB
DB_HOST_SLAVE=mysql-slave
DB_PORT_SLAVE=3306

# Redis Configuration
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_PASSWORD=redis_password_change_me

# JWT Configuration
JWT_SECRET=your_jwt_secret_key_change_me_in_production
JWT_EXPIRATION=24h

# Encryption Keys
DB_ENCRYPTION_KEY=db_encryption_key_32chars_long
ENV_ENCRYPTION_KEY=env_encryption_key_32chars_long

# Application Settings
NODE_ENV=production
APP_PORT=3000
FRONTEND_PORT=80
API_URL=http://localhost:3000

# Domain Configuration
DOMAIN=localhost
EMAIL=admin@localhost

# Docker Registry (optional)
# DOCKER_REGISTRY=your-registry.com
EOF

print_status "Environment file created"
print_warning "Please edit $APP_DIR/.env and change the default passwords!"

# Create docker-compose.yml
echo ""
echo "Step 8: Creating Docker Compose configuration..."
cat > $APP_DIR/docker-compose.yml << 'EOF'
version: '3.8'

services:
  # MariaDB Master
  mysql-master:
    image: mariadb:10.6
    container_name: saas-mysql-master
    restart: unless-stopped
    environment:
      MYSQL_ROOT_PASSWORD: ${DB_ROOT_PASSWORD}
      MYSQL_DATABASE: ${DB_DATABASE}
      MYSQL_USER: ${DB_USERNAME}
      MYSQL_PASSWORD: ${DB_PASSWORD}
    volumes:
      - ./data/mysql-master:/var/lib/mysql
      - ./init/master.cnf:/etc/mysql/conf.d/master.cnf
    ports:
      - "3306:3306"
    networks:
      - saas-network
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost"]
      interval: 10s
      timeout: 5s
      retries: 5

  # MariaDB Slave
  mysql-slave:
    image: mariadb:10.6
    container_name: saas-mysql-slave
    restart: unless-stopped
    environment:
      MYSQL_ROOT_PASSWORD: ${DB_ROOT_PASSWORD}
      MYSQL_DATABASE: ${DB_DATABASE}
      MYSQL_USER: ${DB_USERNAME}
      MYSQL_PASSWORD: ${DB_PASSWORD}
    volumes:
      - ./data/mysql-slave:/var/lib/mysql
      - ./init/slave.cnf:/etc/mysql/conf.d/slave.cnf
    ports:
      - "3307:3306"
    networks:
      - saas-network
    depends_on:
      - mysql-master
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Redis
  redis:
    image: redis:7-alpine
    container_name: saas-redis
    restart: unless-stopped
    command: redis-server --requirepass ${REDIS_PASSWORD}
    volumes:
      - ./data/redis:/data
    ports:
      - "6379:6379"
    networks:
      - saas-network
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5

  # Backend API
  backend:
    build:
      context: ./backend
      dockerfile: Dockerfile
    container_name: saas-backend
    restart: unless-stopped
    environment:
      NODE_ENV: ${NODE_ENV}
      PORT: ${APP_PORT}
      DB_HOST_MASTER: ${DB_HOST_MASTER}
      DB_PORT_MASTER: ${DB_PORT_MASTER}
      DB_HOST_SLAVE: ${DB_HOST_SLAVE}
      DB_PORT_SLAVE: ${DB_PORT_SLAVE}
      DB_USERNAME: ${DB_USERNAME}
      DB_PASSWORD: ${DB_PASSWORD}
      DB_DATABASE: ${DB_DATABASE}
      REDIS_HOST: ${REDIS_HOST}
      REDIS_PORT: ${REDIS_PORT}
      REDIS_PASSWORD: ${REDIS_PASSWORD}
      JWT_SECRET: ${JWT_SECRET}
      JWT_EXPIRATION: ${JWT_EXPIRATION}
      DB_ENCRYPTION_KEY: ${DB_ENCRYPTION_KEY}
      ENV_ENCRYPTION_KEY: ${ENV_ENCRYPTION_KEY}
    ports:
      - "3000:3000"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - ./data/builds:/tmp/builds
    networks:
      - saas-network
    depends_on:
      - mysql-master
      - mysql-slave
      - redis

  # Frontend
  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    container_name: saas-frontend
    restart: unless-stopped
    environment:
      VITE_API_URL: ${API_URL}
    ports:
      - "80:80"
    networks:
      - saas-network
    depends_on:
      - backend

  # Nginx Load Balancer
  nginx:
    image: nginx:alpine
    container_name: saas-nginx
    restart: unless-stopped
    ports:
      - "8080:80"
    volumes:
      - ./data/nginx:/etc/nginx/conf.d
      - ./init/nginx.conf:/etc/nginx/nginx.conf
    networks:
      - saas-network
    depends_on:
      - backend
      - frontend

networks:
  saas-network:
    driver: bridge

volumes:
  mysql-master-data:
  mysql-slave-data:
  redis-data:
EOF

print_status "Docker Compose configuration created"

# Create init directory and config files
echo ""
echo "Step 9: Creating database configuration files..."
mkdir -p $APP_DIR/init

# Master MySQL config
cat > $APP_DIR/init/master.cnf << 'EOF'
[mysqld]
server-id = 1
log_bin = mysql-bin
binlog_do_db = saas_gestor
binlog_format = ROW
character-set-server = utf8mb4
collation-server = utf8mb4_unicode_ci
max_connections = 500
innodb_buffer_pool_size = 1G
EOF

# Slave MySQL config
cat > $APP_DIR/init/slave.cnf << 'EOF'
[mysqld]
server-id = 2
relay_log = mysql-relay-bin
log_bin = mysql-bin
read_only = 1
character-set-server = utf8mb4
collation-server = utf8mb4_unicode_ci
max_connections = 500
innodb_buffer_pool_size = 1G
EOF

print_status "Database configuration files created"

# Create Nginx config
cat > $APP_DIR/init/nginx.conf << 'EOF'
events {
    worker_connections 1024;
}

http {
    upstream backend {
        server backend:3000;
    }

    upstream frontend {
        server frontend:80;
    }

    server {
        listen 80;
        server_name localhost;

        location /api {
            proxy_pass http://backend;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }

        location / {
            proxy_pass http://frontend;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }
}
EOF

print_status "Nginx configuration created"

# Create management scripts
echo ""
echo "Step 10: Creating management scripts..."

# Start script
cat > $APP_DIR/start.sh << 'EOF'
#!/bin/bash
cd /opt/saas-gestor
docker-compose up -d
echo "SaaS Gestor started!"
echo "Frontend: http://localhost"
echo "Backend API: http://localhost:3000"
EOF
chmod +x $APP_DIR/start.sh

# Stop script
cat > $APP_DIR/stop.sh << 'EOF'
#!/bin/bash
cd /opt/saas-gestor
docker-compose down
echo "SaaS Gestor stopped!"
EOF
chmod +x $APP_DIR/stop.sh

# Logs script
cat > $APP_DIR/logs.sh << 'EOF'
#!/bin/bash
cd /opt/saas-gestor
if [ -z "$1" ]; then
    docker-compose logs -f
else
    docker-compose logs -f "$1"
fi
EOF
chmod +x $APP_DIR/logs.sh

# Backup script
cat > $APP_DIR/backup.sh << 'EOF'
#!/bin/bash
BACKUP_DIR="/opt/saas-gestor/backups"
DATE=$(date +%Y%m%d_%H%M%S)
mkdir -p $BACKUP_DIR

# Backup databases
docker exec saas-mysql-master mysqldump -u root -p${DB_ROOT_PASSWORD} saas_gestor > $BACKUP_DIR/db_backup_$DATE.sql

# Backup Redis
docker exec saas-redis redis-cli SAVE
docker cp saas-redis:/data/dump.rdb $BACKUP_DIR/redis_backup_$DATE.rdb

# Compress backup
tar -czf $BACKUP_DIR/backup_$DATE.tar.gz -C $BACKUP_DIR db_backup_$DATE.sql redis_backup_$DATE.rdb
rm $BACKUP_DIR/db_backup_$DATE.sql $BACKUP_DIR/redis_backup_$DATE.rdb

echo "Backup completed: $BACKUP_DIR/backup_$DATE.tar.gz"
EOF
chmod +x $APP_DIR/backup.sh

# Update script
cat > $APP_DIR/update.sh << 'EOF'
#!/bin/bash
cd /opt/saas-gestor
echo "Pulling latest code..."
git pull origin main
echo "Rebuilding containers..."
docker-compose down
docker-compose build --no-cache
docker-compose up -d
echo "Update completed!"
EOF
chmod +x $APP_DIR/update.sh

print_status "Management scripts created"

# Create systemd service
echo ""
echo "Step 11: Creating systemd service..."
cat > /etc/systemd/system/saas-gestor.service << EOF
[Unit]
Description=SaaS Gestor Application
Requires=docker.service
After=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
WorkingDirectory=$APP_DIR
ExecStart=$APP_DIR/start.sh
ExecStop=$APP_DIR/stop.sh
TimeoutStartSec=0

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable saas-gestor
print_status "Systemd service created and enabled"

# Create status check script
cat > $APP_DIR/status.sh << 'EOF'
#!/bin/bash
echo "=========================================="
echo "SaaS Gestor - System Status"
echo "=========================================="
echo ""
echo "Docker Containers:"
docker-compose ps
echo ""
echo "System Resources:"
docker stats --no-stream --format "table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}\t{{.NetIO}}\t{{.BlockIO}}"
echo ""
echo "Disk Usage:"
df -h /opt/saas-gestor
echo ""
echo "Service Status:"
systemctl is-active saas-gestor
EOF
chmod +x $APP_DIR/status.sh

# Final instructions
echo ""
echo "=========================================="
echo "${GREEN}Installation Complete!${NC}"
echo "=========================================="
echo ""
echo "Next Steps:"
echo ""
echo "1. ${YELLOW}Edit the environment file:${NC}"
echo "   nano $APP_DIR/.env"
echo ""
echo "2. ${YELLOW}Copy your application code:${NC}"
echo "   - Copy backend/ to $APP_DIR/backend/"
echo "   - Copy frontend/ to $APP_DIR/frontend/"
echo ""
echo "3. ${YELLOW}Start the application:${NC}"
echo "   cd $APP_DIR"
echo "   ./start.sh"
echo ""
echo "4. ${YELLOW}Check status:${NC}"
echo "   ./status.sh"
echo ""
echo "5. ${YELLOW}View logs:${NC}"
echo "   ./logs.sh"
echo "   ./logs.sh backend"
echo "   ./logs.sh frontend"
echo ""
echo "Access URLs:"
echo "   Frontend: http://192.168.170.124"
echo "   Backend API: http://192.168.170.124:3000"
echo ""
echo "Management Commands:"
echo "   systemctl start saas-gestor"
echo "   systemctl stop saas-gestor"
echo "   systemctl restart saas-gestor"
echo "   systemctl status saas-gestor"
echo ""
echo "=========================================="
