# SaaS Gestor - Deploy Guide

## 📋 System Requirements

- **OS**: Ubuntu 24.04 LTS (or 22.04 LTS)
- **RAM**: 4GB minimum (8GB recommended)
- **CPU**: 2 cores minimum (4 cores recommended)
- **Disk**: 50GB minimum (100GB recommended)
- **Network**: Internet access for Docker images

## 🚀 Quick Deploy (5 minutes)

### Method 1: Using the deploy script

```bash
# 1. Copy the deploy script to your VM
scp deploy.sh root@192.168.170.124:/root/

# 2. SSH into the VM
ssh root@192.168.170.124

# 3. Run the deploy script
chmod +x deploy.sh
./deploy.sh

# 4. Edit environment variables
nano /opt/saas-gestor/.env

# 5. Copy your application code
# From your local machine:
scp -r saas-gestor/backend root@192.168.170.124:/opt/saas-gestor/
scp -r saas-gestor/frontend root@192.168.170.124:/opt/saas-gestor/

# 6. Start the application
cd /opt/saas-gestor
./start.sh
```

### Method 2: Manual step-by-step

```bash
# 1. Update system
apt-get update && apt-get upgrade -y

# 2. Install Docker
curl -fsSL https://get.docker.com | sh
systemctl enable docker && systemctl start docker

# 3. Install Docker Compose
curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
chmod +x /usr/local/bin/docker-compose

# 4. Create application directory
mkdir -p /opt/saas-gestor && cd /opt/saas-gestor

# 5. Copy docker-compose.yml and .env
cp /path/to/docker-compose.yml .
cp /path/to/.env.example .env

# 6. Edit environment variables
nano .env

# 7. Copy application code
mkdir -p backend frontend
cp -r /path/to/backend/* backend/
cp -r /path/to/frontend/* frontend/

# 8. Start services
docker-compose up -d
```

## ⚙️ Environment Variables

Edit `/opt/saas-gestor/.env`:

```bash
# Database Configuration
DB_ROOT_PASSWORD=your_secure_root_password
DB_APP_PASSWORD=your_secure_app_password
DB_NAME=saas_gestor
DB_USER=saas_app

# Redis Configuration
REDIS_PASSWORD=your_secure_redis_password

# JWT Configuration
JWT_SECRET=your_jwt_secret_min_32_chars_long
JWT_REFRESH_SECRET=your_refresh_secret_min_32_chars

# Encryption Keys (32 characters)
DB_ENCRYPTION_KEY=your_db_encryption_key_32chars
ENV_ENCRYPTION_KEY=your_env_encryption_key_32chars
```

## 🔧 Management Commands

```bash
# View status
/opt/saas-gestor/status.sh

# View logs
/opt/saas-gestor/logs.sh
/opt/saas-gestor/logs.sh backend
/opt/saas-gestor/logs.sh frontend

# Start/Stop/Restart
systemctl start saas-gestor
systemctl stop saas-gestor
systemctl restart saas-gestor

# Backup
cd /opt/saas-gestor && ./backup.sh

# Update application
cd /opt/saas-gestor && ./update.sh

# Docker commands
cd /opt/saas-gestor
docker-compose ps
docker-compose logs -f
docker-compose down
docker-compose up -d
docker-compose restart
```

## 🌐 Access URLs

After deployment:
- **Frontend**: http://192.168.170.124
- **Backend API**: http://192.168.170.124:3000
- **API Documentation**: http://192.168.170.124:3000/api/docs

## 🔒 Security

1. **Change default passwords** in `.env` file
2. **Enable HTTPS** (see SSL section below)
3. **Configure firewall** (already done by deploy script)
4. **Regular backups** using `./backup.sh`

## 📝 SSL/HTTPS Setup (Optional)

```bash
# Install certbot
apt-get install -y certbot python3-certbot-nginx

# Get SSL certificate
certbot --nginx -d your-domain.com -d www.your-domain.com

# Auto-renewal (already configured)
systemctl status certbot.timer
```

## 🐛 Troubleshooting

### Check service status
```bash
cd /opt/saas-gestor
./status.sh
```

### View logs
```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f backend
docker-compose logs -f mariadb-master
docker-compose logs -f redis
```

### Reset everything
```bash
cd /opt/saas-gestor
docker-compose down -v  # Remove volumes too
docker-compose up -d
```

### Database connection issues
```bash
# Check MariaDB
docker exec -it saas-mariadb-master mysql -u root -p

# Check replication status
docker exec -it saas-mariadb-slave mysql -u root -p -e "SHOW SLAVE STATUS\G"
```

### Port already in use
```bash
# Check what's using port 80/3000
netstat -tlnp | grep -E ':(80|3000|3306|6379)'

# Kill process or change ports in docker-compose.yml
```

## 📊 Monitoring

```bash
# System resources
docker stats

# Disk usage
df -h

# Memory usage
free -h
```

## 🔄 Updates

```bash
cd /opt/saas-gestor

# Pull latest code
git pull origin main

# Rebuild containers
docker-compose down
docker-compose build --no-cache
docker-compose up -d

# Or use the update script
./update.sh
```

## 📁 Directory Structure

```
/opt/saas-gestor/
├── backend/              # NestJS backend code
├── frontend/             # React frontend code
├── data/                 # Persistent data
│   ├── mysql-master/     # Master database
│   ├── mysql-slave/      # Slave database
│   ├── redis/            # Redis data
│   └── nginx/            # Nginx configs
├── backups/              # Backup files
├── init/                 # Initialization files
├── docker-compose.yml    # Docker Compose config
├── .env                  # Environment variables
├── start.sh              # Start script
├── stop.sh               # Stop script
├── logs.sh               # Logs script
├── status.sh             # Status script
├── backup.sh             # Backup script
└── update.sh             # Update script
```

## 🆘 Support

If you encounter issues:
1. Check logs: `./logs.sh`
2. Check status: `./status.sh`
3. Verify environment: `docker-compose config`
4. Restart services: `systemctl restart saas-gestor`

## 📄 License

Private - All rights reserved.
