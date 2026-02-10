#!/bin/bash
# Quick setup script for SaaS Gestor
# This script prepares the VM and deploys the application

set -e

APP_DIR="/opt/saas-gestor"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}"
echo "=========================================="
echo "  SaaS Gestor - Quick Deploy"
echo "=========================================="
echo -e "${NC}"

# Check root
if [ "$EUID" -ne 0 ]; then 
    echo -e "${RED}Please run as root${NC}"
    exit 1
fi

# 1. Update system
echo -e "${YELLOW}[1/8] Updating system...${NC}"
apt-get update -qq
apt-get upgrade -y -qq

# 2. Install Docker
echo -e "${YELLOW}[2/8] Installing Docker...${NC}"
if ! command -v docker &> /dev/null; then
    curl -fsSL https://get.docker.com | sh
    usermod -aG docker $SUDO_USER 2>/dev/null || true
    systemctl enable docker
    systemctl start docker
fi

# 3. Install Docker Compose
echo -e "${YELLOW}[3/8] Installing Docker Compose...${NC}"
if ! command -v docker-compose &> /dev/null; then
    curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
    chmod +x /usr/local/bin/docker-compose
fi

# 4. Create directories
echo -e "${YELLOW}[4/8] Creating directories...${NC}"
mkdir -p $APP_DIR/{backend,frontend,data/{mysql-master,mysql-slave,redis,nginx},backups,init}

# 5. Copy files (if they exist in current directory)
echo -e "${YELLOW}[5/8] Setting up application files...${NC}"
if [ -d "backend" ]; then
    cp -r backend/* $APP_DIR/backend/ 2>/dev/null || true
fi
if [ -d "frontend" ]; then
    cp -r frontend/* $APP_DIR/frontend/ 2>/dev/null || true
fi
if [ -f "docker-compose.yml" ]; then
    cp docker-compose.yml $APP_DIR/
fi
if [ -f ".env.example" ]; then
    cp .env.example $APP_DIR/.env
fi

# 6. Set permissions
echo -e "${YELLOW}[6/8] Setting permissions...${NC}"
chmod -R 755 $APP_DIR

# 7. Configure firewall
echo -e "${YELLOW}[7/8] Configuring firewall...${NC}"
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw allow 3000/tcp
ufw allow 3306/tcp
ufw allow 3307/tcp
ufw allow 6379/tcp
ufw --force enable

# 8. Start services
echo -e "${YELLOW}[8/8] Starting services...${NC}"
cd $APP_DIR
if [ -f "docker-compose.yml" ]; then
    docker-compose up -d
fi

echo ""
echo -e "${GREEN}=========================================="
echo "  Setup Complete!"
echo "==========================================${NC}"
echo ""
echo -e "Application Directory: ${BLUE}$APP_DIR${NC}"
echo -e "Frontend: ${BLUE}http://$(hostname -I | awk '{print $1}')${NC}"
echo -e "Backend API: ${BLUE}http://$(hostname -I | awk '{print $1}'):3000${NC}"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "1. Edit .env file: nano $APP_DIR/.env"
echo "2. Copy your backend/frontend code to $APP_DIR/"
echo "3. Run: cd $APP_DIR && docker-compose up -d"
echo ""
