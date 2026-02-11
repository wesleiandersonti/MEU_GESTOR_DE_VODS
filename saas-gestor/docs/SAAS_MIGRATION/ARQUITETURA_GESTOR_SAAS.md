# 🏗️ ARQUITETURA COMPLETA - SISTEMA GESTOR SAAS

## 📋 VISÃO GERAL DO SISTEMA

Sistema de gestão centralizado para deploy, builds, arquivos e bancos de dados, rodando em Ubuntu 22.04, com integração segura ao MariaDB do XUI One.

---

## 🎯 ARQUITETURA TÉCNICA PROPOSTA

### 1. Stack Tecnológica Escolhida

| Componente | Tecnologia | Justificativa |
|------------|------------|---------------|
| **Backend** | Node.js + NestJS | TypeScript nativo, arquitetura modular, excelente para APIs REST e WebSocket, grande ecossistema |
| **Frontend** | React 18 + TypeScript | SPA moderna, componentização, vasta biblioteca de componentes UI |
| **Banco Principal** | MariaDB 10.6 Master-Slave | Alta disponibilidade, replicação, failover automático, separação leitura/escrita |
| **Cache** | Redis 7 | Sessões, cache de queries, pub/sub para tempo real |
| **Fila** | BullMQ (Redis) | Jobs assíncronos, builds em background, processamento confiável |
| **WebSocket** | Socket.io | Comunicação bidirecional real-time para dashboard |
| **Storage** | MinIO (S3-compatible) | Armazenamento de arquivos, compatível com AWS S3 API |
| **Reverse Proxy** | Nginx | SSL termination, rate limiting, load balancing |
| **Monitoramento** | Prometheus + Grafana | Métricas, alertas, dashboards |
| **Logs** | Loki + Grafana | Centralização de logs, busca eficiente |
| **AI/Assistente** | **OpenClaw** | Treinamento e gestão do projeto com IA, automação de código, revisão e documentação |

### 2. Por que NestJS?

✅ **TypeScript first**: Type safety em todo o backend  
✅ **Arquitetura modular**: Fácil manutenção e testes  
✅ **Injeção de dependências**: Código desacoplado  
✅ **Integração nativa**: WebSocket, filas, TypeORM  
✅ **Documentação automática**: Swagger/OpenAPI integrado  
✅ **Performance**: Baseado em Express/Fastify, async/await nativo  

---

## 🏛️ DIAGRAMA DA ARQUITETURA

```
┌─────────────────────────────────────────────────────────────────────┐
│                         CLIENTES                                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐              │
│  │   Web App    │  │  Mobile App  │  │   CLI Tool   │              │
│  │   (React)    │  │   (Futuro)   │  │              │              │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘              │
└─────────┼─────────────────┼─────────────────┼──────────────────────┘
          │                 │                 │
          └─────────────────┴─────────────────┘
                            │ HTTPS/WSS
          ┌─────────────────┴─────────────────┐
          │         NGINX (Reverse Proxy)      │
          │  • SSL Termination                 │
          │  • Rate Limiting                   │
          │  • Static Files                    │
          └─────────────────┬─────────────────┘
                            │
          ┌─────────────────┴─────────────────┐
          │      UBUNTU SERVER 22.04 LTS       │
          │                                    │
          │  ┌─────────────────────────────┐  │
          │  │    Docker Network (saas)    │  │
          │  │                             │  │
          │  │  ┌───────────────────────┐  │  │
          │  │  │   NestJS API (Node)   │  │  │
          │  │  │   • REST API          │  │  │
          │  │  │   • WebSocket         │  │  │
          │  │  │   • Jobs Queue        │  │  │
          │  │  │   Port: 3000          │  │  │
          │  │  └───────────┬───────────┘  │  │
          │  │              │              │  │
          │  │  ┌───────────┴───────────┐  │  │
          │  │  │      React SPA        │  │  │
          │  │  │   Port: 80 (nginx)    │  │  │
          │  │  └───────────────────────┘  │  │
          │  │                             │  │
           │  │  ┌──────────────────────────┐ │  │
           │  │  │   MARIADB CLUSTER        │ │  │
           │  │  │  ┌─────────────────────┐ │ │  │
           │  │  │  │  MASTER (Escrita)   │ │ │  │
           │  │  │  │  • Port: 3306       │ │ │  │
           │  │  │  │  • Replicação       │ │ │  │
           │  │  │  └──────────┬──────────┘ │ │  │
           │  │  │             │            │ │  │
           │  │  │  ┌──────────┴──────────┐ │ │  │
           │  │  │  │  SLAVE (Leitura)    │ │ │  │
           │  │  │  │  • Port: 3307       │ │ │  │
           │  │  │  │  • Replicação       │ │ │  │
           │  │  │  │  • Failover         │ │ │  │
           │  │  │  └─────────────────────┘ │ │  │
           │  │  └──────────────────────────┘ │  │
           │  │                                 │  │
           │  │  ┌───────────────────────┐     │  │
           │  │  │   Redis 7             │     │  │
           │  │  │   Port: 6379          │     │  │
           │  │  └───────────────────────┘     │  │
          │  │                             │  │
          │  │  ┌───────────────────────┐  │  │
          │  │  │   MinIO (S3)          │  │  │
          │  │  │   Port: 9000/9001     │  │  │
          │  │  └───────────────────────┘  │  │
          │  │                             │  │
          │  │  ┌───────────────────────┐  │  │
          │  │  │   Prometheus          │  │  │
          │  │  │   Port: 9090          │  │  │
          │  │  └───────────────────────┘  │  │
          │  │                             │  │
          │  └─────────────────────────────┘  │
          │                                    │
          │  ┌─────────────────────────────┐  │
          │  │   VOLUMES DOCKER           │  │
          │  │   • mariadb_data           │  │
          │  │   • redis_data             │  │
          │  │   • minio_data             │  │
          │  │   • app_uploads            │  │
          │  │   • app_logs               │  │
          │  │   • app_backups            │  │
          │  └─────────────────────────────┘  │
          │                                    │
          └────────────────────────────────────┘
                            │
          ┌─────────────────┴─────────────────┐
          │    CONEXÃO COM XUI ONE (READ)     │
          │         MariaDB Externo           │
          │       (Apenas SELECT, SHOW)       │
          └────────────────────────────────────┘
```

---

## 🗄️ MODELO DE DADOS (MariaDB)

### 1. Schema Principal: `saas_gestor`

```sql
-- =====================================================
-- 1. TENANTS E USUÁRIOS (Multi-tenant básico)
-- =====================================================

CREATE TABLE tenants (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    slug VARCHAR(100) UNIQUE NOT NULL,
    plan_type ENUM('free', 'basic', 'pro', 'enterprise') DEFAULT 'free',
    status ENUM('active', 'suspended', 'cancelled') DEFAULT 'active',
    max_apps INT DEFAULT 5,
    max_databases INT DEFAULT 3,
    storage_limit_gb INT DEFAULT 10,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_tenant_slug (slug),
    INDEX idx_tenant_status (status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    tenant_id INT NOT NULL,
    email VARCHAR(255) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    name VARCHAR(255),
    role ENUM('admin', 'devops', 'developer', 'viewer') DEFAULT 'developer',
    status ENUM('active', 'inactive', 'blocked') DEFAULT 'active',
    last_login TIMESTAMP NULL,
    email_verified BOOLEAN DEFAULT FALSE,
    two_factor_enabled BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
    UNIQUE KEY uk_tenant_email (tenant_id, email),
    INDEX idx_user_role (role),
    INDEX idx_user_status (status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- 2. APLICAÇÕES
-- =====================================================

CREATE TABLE applications (
    id INT AUTO_INCREMENT PRIMARY KEY,
    tenant_id INT NOT NULL,
    name VARCHAR(255) NOT NULL,
    slug VARCHAR(100) NOT NULL,
    description TEXT,
    repository_url VARCHAR(500),
    repository_branch VARCHAR(100) DEFAULT 'main',
    app_type ENUM('node', 'php', 'python', 'static', 'docker') NOT NULL,
    environment ENUM('development', 'staging', 'production') DEFAULT 'development',
    status ENUM('active', 'inactive', 'error', 'deploying') DEFAULT 'inactive',
    domain VARCHAR(255),
    port INT,
    docker_image VARCHAR(255),
    env_vars JSON,
    build_command TEXT,
    start_command TEXT,
    health_check_url VARCHAR(500),
    last_deploy_at TIMESTAMP NULL,
    last_build_at TIMESTAMP NULL,
    created_by INT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
    FOREIGN KEY (created_by) REFERENCES users(id),
    UNIQUE KEY uk_tenant_slug (tenant_id, slug),
    INDEX idx_app_status (status),
    INDEX idx_app_environment (environment),
    INDEX idx_app_type (app_type)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- 3. BUILDS
-- =====================================================

CREATE TABLE builds (
    id INT AUTO_INCREMENT PRIMARY KEY,
    tenant_id INT NOT NULL,
    application_id INT NOT NULL,
    build_number INT NOT NULL,
    commit_hash VARCHAR(100),
    commit_message TEXT,
    status ENUM('pending', 'running', 'success', 'failed', 'cancelled') DEFAULT 'pending',
    started_at TIMESTAMP NULL,
    completed_at TIMESTAMP NULL,
    duration_seconds INT,
    logs LONGTEXT,
    artifact_path VARCHAR(500),
    triggered_by INT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
    FOREIGN KEY (application_id) REFERENCES applications(id) ON DELETE CASCADE,
    FOREIGN KEY (triggered_by) REFERENCES users(id),
    UNIQUE KEY uk_app_build_number (application_id, build_number),
    INDEX idx_build_status (status),
    INDEX idx_build_dates (started_at, completed_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- 4. BANCOS DE DADOS
-- =====================================================

CREATE TABLE database_connections (
    id INT AUTO_INCREMENT PRIMARY KEY,
    tenant_id INT NOT NULL,
    name VARCHAR(255) NOT NULL,
    connection_type ENUM('internal', 'xui_one', 'external') NOT NULL,
    host VARCHAR(255) NOT NULL,
    port INT DEFAULT 3306,
    database_name VARCHAR(100),
    username VARCHAR(100),
    -- Senha criptografada (AES-256)
    password_encrypted TEXT,
    is_read_only BOOLEAN DEFAULT FALSE,
    is_xui_one BOOLEAN DEFAULT FALSE,
    status ENUM('active', 'inactive', 'error') DEFAULT 'active',
    last_check_at TIMESTAMP NULL,
    last_check_status VARCHAR(50),
    last_check_latency_ms INT,
    created_by INT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
    FOREIGN KEY (created_by) REFERENCES users(id),
    INDEX idx_db_status (status),
    INDEX idx_db_type (connection_type),
    INDEX idx_db_xui (is_xui_one)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE database_users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    tenant_id INT NOT NULL,
    connection_id INT NOT NULL,
    username VARCHAR(100) NOT NULL,
    password_encrypted TEXT,
    grants JSON, -- ['SELECT', 'INSERT', 'UPDATE', 'DELETE']
    status ENUM('active', 'inactive') DEFAULT 'active',
    created_by INT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
    FOREIGN KEY (connection_id) REFERENCES database_connections(id) ON DELETE CASCADE,
    FOREIGN KEY (created_by) REFERENCES users(id),
    INDEX idx_dbuser_status (status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- 5. MONITORAMENTO DA REPLICAÇÃO MARIADB
-- =====================================================

CREATE TABLE replication_status (
    id INT AUTO_INCREMENT PRIMARY KEY,
    master_host VARCHAR(255) NOT NULL,
    master_port INT DEFAULT 3306,
    slave_host VARCHAR(255) NOT NULL,
    slave_port INT DEFAULT 3306,
    replication_status ENUM('running', 'stopped', 'error') DEFAULT 'running',
    last_io_error TEXT,
    last_sql_error TEXT,
    seconds_behind_master INT DEFAULT 0,
    master_log_file VARCHAR(100),
    master_log_pos BIGINT,
    slave_io_running BOOLEAN,
    slave_sql_running BOOLEAN,
    checked_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_repl_status (replication_status),
    INDEX idx_repl_checked (checked_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- 6. ARQUIVOS E STORAGE
-- =====================================================

CREATE TABLE file_storage (
    id INT AUTO_INCREMENT PRIMARY KEY,
    tenant_id INT NOT NULL,
    application_id INT NULL,
    filename VARCHAR(255) NOT NULL,
    original_name VARCHAR(255),
    mime_type VARCHAR(100),
    size_bytes BIGINT,
    storage_path VARCHAR(500) NOT NULL,
    bucket VARCHAR(100) DEFAULT 'default',
    is_public BOOLEAN DEFAULT FALSE,
    public_url VARCHAR(500),
    uploaded_by INT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
    FOREIGN KEY (application_id) REFERENCES applications(id) ON DELETE SET NULL,
    FOREIGN KEY (uploaded_by) REFERENCES users(id),
    INDEX idx_file_tenant (tenant_id),
    INDEX idx_file_app (application_id),
    INDEX idx_file_created (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- 6. AUDITORIA E LOGS
-- =====================================================

CREATE TABLE audit_logs (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    tenant_id INT NOT NULL,
    user_id INT,
    action VARCHAR(100) NOT NULL, -- 'CREATE_APP', 'DELETE_DB', 'DEPLOY', etc
    entity_type VARCHAR(50), -- 'application', 'database', 'user', etc
    entity_id INT,
    old_values JSON,
    new_values JSON,
    ip_address VARCHAR(45),
    user_agent VARCHAR(500),
    severity ENUM('info', 'warning', 'error', 'critical') DEFAULT 'info',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(id),
    INDEX idx_audit_tenant (tenant_id),
    INDEX idx_audit_user (user_id),
    INDEX idx_audit_action (action),
    INDEX idx_audit_created (created_at),
    INDEX idx_audit_severity (severity)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci PARTITION BY RANGE (YEAR(created_at)) (
    PARTITION p2024 VALUES LESS THAN (2025),
    PARTITION p2025 VALUES LESS THAN (2026),
    PARTITION p2026 VALUES LESS THAN (2027),
    PARTITION pfuture VALUES LESS THAN MAXVALUE
);

-- =====================================================
-- 7. EVENTOS PARA DASHBOARD (Tempo Real)
-- =====================================================

CREATE TABLE system_events (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    tenant_id INT NOT NULL,
    event_type VARCHAR(50) NOT NULL, -- 'app_status_change', 'build_completed', 'db_error', etc
    severity ENUM('info', 'warning', 'error', 'critical') DEFAULT 'info',
    title VARCHAR(255) NOT NULL,
    message TEXT,
    metadata JSON,
    is_read BOOLEAN DEFAULT FALSE,
    read_by INT,
    read_at TIMESTAMP NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
    FOREIGN KEY (read_by) REFERENCES users(id),
    INDEX idx_events_tenant (tenant_id),
    INDEX idx_events_type (event_type),
    INDEX idx_events_severity (severity),
    INDEX idx_events_created (created_at),
    INDEX idx_events_read (is_read)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- 8. MÉTRICAS DO SISTEMA (Para Dashboard)
-- =====================================================

CREATE TABLE system_metrics (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    tenant_id INT NOT NULL,
    metric_type VARCHAR(50) NOT NULL, -- 'cpu', 'memory', 'disk', 'connections', etc
    metric_name VARCHAR(100) NOT NULL,
    metric_value DECIMAL(15,4) NOT NULL,
    unit VARCHAR(20), -- 'percent', 'bytes', 'count', 'ms'
    labels JSON, -- {"app_id": 123, "db_id": 456}
    recorded_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
    INDEX idx_metrics_tenant (tenant_id),
    INDEX idx_metrics_type (metric_type),
    INDEX idx_metrics_recorded (recorded_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci PARTITION BY RANGE (UNIX_TIMESTAMP(recorded_at)) (
    PARTITION p_recent VALUES LESS THAN (UNIX_TIMESTAMP('2026-02-10 00:00:00')),
    PARTITION p_future VALUES LESS THAN MAXVALUE
);
```

---

## 🗄️ MARIADB MASTER-SLAVE (REPLICAÇÃO)

### 1. Visão Geral da Replicação

O sistema utiliza **MariaDB 10.6 com replicação Master-Slave** para:
- ✅ **Alta Disponibilidade**: Failover automático se o Master falhar
- ✅ **Balanceamento de Carga**: Leituras no Slave, escritas no Master
- ✅ **Backups Online**: Backups feitos no Slave sem impactar o Master
- ✅ **Análises**: Queries pesadas no Slave (relatórios, BI)

### 2. Arquitetura Master-Slave

```
┌─────────────────────────────────────────────────────┐
│            MARIADB REPLICATION CLUSTER              │
├─────────────────────────────────────────────────────┤
│                                                      │
│  ┌──────────────────┐      ┌──────────────────┐   │
│  │    MASTER        │      │     SLAVE        │   │
│  │    (Primary)     │      │   (Secondary)    │   │
│  │                  │      │                  │   │
│  │  • Escritas      │      │  • Leituras      │   │
│  │  • Port: 3306    │      │  • Port: 3307    │   │
│  │  • Binlog        │──────▶│  • Relay Log     │   │
│  │  • GTID          │      │  • Replicação    │   │
│  │                  │      │  • Read-Only     │   │
│  └──────────────────┘      └──────────────────┘   │
│           │                        │                │
│           │    Async Replication   │                │
│           └────────────────────────┘                │
│                                                      │
│  ┌──────────────────────────────────────────────┐  │
│  │         MONITORAMENTO                         │  │
│  │  • Lag de replicação (seconds_behind_master) │  │
│  │  • Status de replicação (SHOW SLAVE STATUS)  │  │
│  │  • Failover automático (opcional)            │  │
│  └──────────────────────────────────────────────┘  │
│                                                      │
└─────────────────────────────────────────────────────┘
```

### 3. Configuração do Master

**Arquivo: `mariadb-master.cnf`**

```ini
[mysqld]
# Configuração do servidor
server-id = 1
bind-address = 0.0.0.0
port = 3306

# Replicação
log_bin = mysql-bin
binlog_format = ROW
binlog_row_image = FULL
expire_logs_days = 7
max_binlog_size = 100M

# GTID (Global Transaction ID) - Recomendado
# gtid_domain_id = 0
# gtid_strict_mode = 1
# log_slave_updates = 1

# Bancos a serem replicados
binlog_do_db = saas_gestor
# binlog_do_db = outro_banco

# Bancos a serem ignorados
# binlog_ignore_db = mysql
# binlog_ignore_db = information_schema
# binlog_ignore_db = performance_schema

# Performance
innodb_flush_log_at_trx_commit = 1
sync_binlog = 1

# Conexões
max_connections = 200
max_allowed_packet = 64M
```

**Comandos SQL no Master:**

```sql
-- Criar usuário de replicação
CREATE USER 'replica'@'%' IDENTIFIED BY 'senha_replica_segura';
GRANT REPLICATION SLAVE ON *.* TO 'replica'@'%';
FLUSH PRIVILEGES;

-- Verificar status
SHOW MASTER STATUS;
-- +------------------+----------+--------------+------------------+
-- | File             | Position | Binlog_Do_DB | Binlog_Ignore_DB |
-- +------------------+----------+--------------+------------------+
-- | mysql-bin.000001 |      154 | saas_gestor  |                  |
-- +------------------+----------+--------------+------------------+
```

### 4. Configuração do Slave

**Arquivo: `mariadb-slave.cnf`**

```ini
[mysqld]
# Configuração do servidor
server-id = 2
bind-address = 0.0.0.0
port = 3307

# Replicação
relay_log = mysql-relay-bin
relay_log_recovery = 1
read_only = 1

# GTID
# gtid_domain_id = 0
# gtid_strict_mode = 1

# Bancos a serem replicados
replicate_do_db = saas_gestor

# Performance
innodb_flush_log_at_trx_commit = 2
sync_binlog = 0

# Conexões
max_connections = 200
max_allowed_packet = 64M
```

**Comandos SQL no Slave:**

```sql
-- Configurar replicação
CHANGE MASTER TO
  MASTER_HOST = 'mariadb-master',
  MASTER_PORT = 3306,
  MASTER_USER = 'replica',
  MASTER_PASSWORD = 'senha_replica_segura',
  MASTER_LOG_FILE = 'mysql-bin.000001',
  MASTER_LOG_POS = 154;

-- Iniciar replicação
START SLAVE;

-- Verificar status
SHOW SLAVE STATUS\G
-- Deve mostrar:
-- Slave_IO_Running: Yes
-- Slave_SQL_Running: Yes
-- Seconds_Behind_Master: 0
```

### 5. Docker Compose - Configuração Master-Slave

```yaml
version: '3.8'

services:
  # ==========================================
  # MARIADB MASTER
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
    volumes:
      - ./data/mariadb-master:/var/lib/mysql
      - ./config/mariadb-master.cnf:/etc/mysql/mariadb.conf.d/99-master.cnf:ro
      - ./init-scripts:/docker-entrypoint-initdb.d:ro
      - ./backups:/backups
    ports:
      - "127.0.0.1:3306:3306"
    networks:
      - saas-network
    command: >
      --server-id=1
      --log-bin=mysql-bin
      --binlog-format=ROW
      --binlog-row-image=FULL
      --expire-logs-days=7
      --max-binlog-size=100M
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost", "-u", "root", "-p${DB_ROOT_PASSWORD}"]
      interval: 30s
      timeout: 10s
      retries: 3

  # ==========================================
  # MARIADB SLAVE
  # ==========================================
  mariadb-slave:
    image: mariadb:10.6
    container_name: saas-mariadb-slave
    restart: unless-stopped
    environment:
      MYSQL_ROOT_PASSWORD: ${DB_ROOT_PASSWORD}
      MYSQL_DATABASE: saas_gestor
    volumes:
      - ./data/mariadb-slave:/var/lib/mysql
      - ./config/mariadb-slave.cnf:/etc/mysql/mariadb.conf.d/99-slave.cnf:ro
    ports:
      - "127.0.0.1:3307:3306"
    networks:
      - saas-network
    command: >
      --server-id=2
      --relay-log=mysql-relay-bin
      --relay-log-recovery=1
      --read-only=1
      --log-bin=mysql-bin
    depends_on:
      mariadb-master:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost", "-u", "root", "-p${DB_ROOT_PASSWORD}"]
      interval: 30s
      timeout: 10s
      retries: 3

  # ==========================================
  # SCRIPT DE INICIALIZAÇÃO DO SLAVE
  # ==========================================
  mariadb-slave-init:
    image: mariadb:10.6
    container_name: saas-mariadb-slave-init
    volumes:
      - ./scripts/init-slave.sh:/init-slave.sh:ro
    command: >
      bash -c "/init-slave.sh"
    depends_on:
      - mariadb-master
      - mariadb-slave
    networks:
      - saas-network
    profiles:
      - init

networks:
  saas-network:
    driver: bridge
```

### 6. Script de Inicialização do Slave

**`scripts/init-slave.sh`:**

```bash
#!/bin/bash
set -e

echo "Aguardando Master ficar pronto..."
sleep 10

# Aguardar Master
until mysql -h mariadb-master -u root -p"${DB_ROOT_PASSWORD}" -e "SELECT 1"; do
  echo "Aguardando Master..."
  sleep 5
done

# Criar usuário de replicação no Master se não existir
mysql -h mariadb-master -u root -p"${DB_ROOT_PASSWORD}" -e "
CREATE USER IF NOT EXISTS 'replica'@'%' IDENTIFIED BY '${DB_REPLICA_PASSWORD}';
GRANT REPLICATION SLAVE ON *.* TO 'replica'@'%';
FLUSH PRIVILEGES;
"

# Obter posição do binlog no Master
MASTER_STATUS=$(mysql -h mariadb-master -u root -p"${DB_ROOT_PASSWORD}" -e "SHOW MASTER STATUS\G")
MASTER_LOG_FILE=$(echo "$MASTER_STATUS" | grep "File:" | awk '{print $2}')
MASTER_LOG_POS=$(echo "$MASTER_STATUS" | grep "Position:" | awk '{print $2}')

echo "Master: $MASTER_LOG_FILE:$MASTER_LOG_POS"

# Configurar Slave
mysql -h mariadb-slave -u root -p"${DB_ROOT_PASSWORD}" -e "
STOP SLAVE;
RESET SLAVE ALL;
CHANGE MASTER TO
  MASTER_HOST = 'mariadb-master',
  MASTER_PORT = 3306,
  MASTER_USER = 'replica',
  MASTER_PASSWORD = '${DB_REPLICA_PASSWORD}',
  MASTER_LOG_FILE = '${MASTER_LOG_FILE}',
  MASTER_LOG_POS = ${MASTER_LOG_POS};
START SLAVE;
"

# Verificar status
mysql -h mariadb-slave -u root -p"${DB_ROOT_PASSWORD}" -e "SHOW SLAVE STATUS\G" | grep -E "(Slave_IO_Running|Slave_SQL_Running|Seconds_Behind_Master)"

echo "Replicação configurada com sucesso!"
```

### 7. Uso no Backend (NestJS)

**Configuração do TypeORM:**

```typescript
// database.module.ts
import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

@Module({
  imports: [
    TypeOrmModule.forRoot({
      name: 'master',
      type: 'mariadb',
      host: process.env.DB_MASTER_HOST || 'mariadb-master',
      port: 3306,
      username: process.env.DB_USER,
      password: process.env.DB_PASSWORD,
      database: process.env.DB_NAME,
      entities: [__dirname + '/../**/*.entity{.ts,.js}'],
      synchronize: false,
    }),
    TypeOrmModule.forRoot({
      name: 'slave',
      type: 'mariadb',
      host: process.env.DB_SLAVE_HOST || 'mariadb-slave',
      port: 3307,
      username: process.env.DB_USER,
      password: process.env.DB_PASSWORD,
      database: process.env.DB_NAME,
      entities: [__dirname + '/../**/*.entity{.ts,.js}'],
      synchronize: false,
    }),
  ],
})
export class DatabaseModule {}
```

**Service com Read/Write Splitting:**

```typescript
// database.service.ts
import { Injectable } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';

@Injectable()
export class DatabaseService {
  constructor(
    @InjectRepository(Application, 'master')
    private masterRepo: Repository<Application>,
    
    @InjectRepository(Application, 'slave')
    private slaveRepo: Repository<Application>,
  ) {}

  // Escritas: sempre no Master
  async create(data: CreateApplicationDto) {
    return this.masterRepo.save(data);
  }

  async update(id: number, data: UpdateApplicationDto) {
    return this.masterRepo.update(id, data);
  }

  async delete(id: number) {
    return this.masterRepo.delete(id);
  }

  // Leituras: pode usar o Slave
  async findAll() {
    return this.slaveRepo.find();
  }

  async findOne(id: number) {
    return this.slaveRepo.findOne({ where: { id } });
  }

  // Leitura crítica: use Master (consistência forte)
  async findOneCritical(id: number) {
    return this.masterRepo.findOne({ where: { id } });
  }
}
```

### 8. Monitoramento da Replicação

**Query para monitorar:**

```sql
-- Status da replicação
SHOW SLAVE STATUS\G

-- Verificar lag
SELECT 
  NOW() as current_time,
  TIMESTAMPDIFF(SECOND, 
    (SELECT MAX(ts) FROM mysql.gtid_slave_pos),
    NOW()
  ) as seconds_behind_master;

-- Verificar erros
SHOW SLAVE STATUS\G | grep -E "(Last_IO_Error|Last_SQL_Error)"
```

**Métricas para Dashboard:**

```typescript
// metrics.service.ts
async getReplicationMetrics() {
  const slaveStatus = await this.slaveRepository.query('SHOW SLAVE STATUS');
  
  return {
    ioRunning: slaveStatus[0]?.Slave_IO_Running === 'Yes',
    sqlRunning: slaveStatus[0]?.Slave_SQL_Running === 'Yes',
    secondsBehindMaster: slaveStatus[0]?.Seconds_Behind_Master || 0,
    lastError: slaveStatus[0]?.Last_IO_Error || slaveStatus[0]?.Last_SQL_Error,
    masterLogFile: slaveStatus[0]?.Master_Log_File,
    relayLogFile: slaveStatus[0]?.Relay_Log_File,
  };
}
```

### 9. Failover (Promoção do Slave para Master)

**Procedimento Manual:**

```bash
#!/bin/bash
# failover.sh

echo "Parando aplicações..."
docker-compose stop backend worker

echo "Parando replicação no Slave..."
docker exec saas-mariadb-slave mysql -u root -p"${DB_ROOT_PASSWORD}" -e "STOP SLAVE;"

echo "Promovendo Slave para Master..."
docker exec saas-mariadb-slave mysql -u root -p"${DB_ROOT_PASSWORD}" -e "SET GLOBAL read_only = 0;"

echo "Atualizando variáveis de ambiente..."
# Atualizar .env para apontar para o novo Master
sed -i 's/DB_MASTER_HOST=.*/DB_MASTER_HOST=mariadb-slave/' .env
sed -i 's/DB_SLAVE_HOST=.*/DB_SLAVE_HOST=mariadb-master/' .env

echo "Reiniciando aplicações..."
docker-compose up -d backend worker

echo "Failover concluído!"
```

### 10. Backups no Slave

```bash
#!/bin/bash
# backup-slave.sh

# Backup sempre no Slave para não impactar o Master
docker exec saas-mariadb-slave mysqldump \
  -u root -p"${DB_ROOT_PASSWORD}" \
  --single-transaction \
  --routines \
  --triggers \
  saas_gestor > "/backups/saas_gestor_$(date +%Y%m%d_%H%M%S).sql"
```

---

## 🤖 OPENCLAW - ASSISTENTE IA PARA GESTÃO DO PROJETO

### Visão Geral

**OpenClaw** será integrado ao sistema como assistente inteligente para gestão e desenvolvimento do projeto MEU GESTOR DE VODS.

### Funcionalidades do OpenClaw

| Área | Funcionalidade | Descrição |
|------|----------------|-----------|
| **Desenvolvimento** | Geração de código | Criação de componentes, APIs, queries SQL |
| **Code Review** | Revisão automática | Análise de PRs, sugestões de melhorias |
| **Documentação** | Auto-documentação | Geração de docs a partir do código |
| **Debugging** | Análise de erros | Identificação de bugs e sugestões de correções |
| **Gestão** | Planejamento de sprints | Quebra de tasks, estimativas, priorização |
| **DevOps** | Automação de deploys | Scripts de CI/CD, configuração de infra |
| **Suporte** | Respostas inteligentes | Chatbot para dúvidas técnicas |

### Treinamento do OpenClaw

**Base de conhecimento para treinamento:**

1. **Código-fonte completo** do MEU GESTOR DE VODS
2. **Documentação técnica** (arquitetura, APIs, schemas)
3. **Histórico de commits** e padrões do projeto
4. **Issues e bugs** resolvidos anteriormente
5. **Padrões de código** e convenções do time

**Processo de treinamento:**

```
Fase 1: Treinamento Inicial (2 semanas)
├── Análise do código existente
├── Compreensão da arquitetura
└── Aprendizado dos padrões de código

Fase 2: Fine-tuning (1 semana)
├── Ajustes baseados em feedback
├── Especialização por módulo
└── Validação com time de dev

Fase 3: Integração (1 semana)
├── API para comunicação
├── Interface no dashboard
└── Testes de usabilidade
```

### Interface no Dashboard

```
┌─────────────────────────────────────────────┐
│  🤖 OpenClaw Assistant                      │
├─────────────────────────────────────────────┤
│                                             │
│  [Chat com OpenClaw]                        │
│  ┌───────────────────────────────────────┐ │
│  │ Você: Como faço deploy do backend?   │ │
│  │                                       │ │
│  │ OpenClaw: Execute o comando:         │ │
│  │ ./scripts/deploy.sh                  │ │
│  │                                       │ │
│  │ Ou use o painel: Apps → Deploy       │ │
│  └───────────────────────────────────────┘ │
│                                             │
│  [Ações Rápidas]                            │
│  [Gerar código] [Revisar PR] [Criar teste]  │
│  [Documentar] [Debugar erro] [Otimizar]     │
│                                             │
└─────────────────────────────────────────────┘
```

### Comandos do OpenClaw

```typescript
// Exemplos de interação com OpenClaw

// Gerar componente React
openclaw.generate({
  type: 'react-component',
  name: 'UserProfileCard',
  props: ['user: User', 'onEdit: () => void'],
  style: 'tailwind'
});

// Revisar código
openclaw.review({
  code: pullRequest.diff,
  language: 'typescript',
  focus: ['security', 'performance']
});

// Criar migration
openclaw.database({
  action: 'create-migration',
  table: 'users',
  changes: ['add column: phone', 'add index: email']
});

// Diagnosticar erro
openclaw.debug({
  error: errorLog,
  context: 'production',
  service: 'backend-api'
});
```

### Segurança e Privacidade

- **Nenhum dado sensível** é enviado para treinamento
- **Código proprietário** permanece local
- **API criptografada** para comunicação
- **Logs de auditoria** de todas as interações
- **Permissões granulares** por usuário/função

---

## 🔐 SEGURANÇA E PERMISSÕES

### 1. Matriz de Permissões (RBAC)

| Ação | Admin | DevOps | Developer | Viewer |
|------|-------|--------|-----------|--------|
| Gerenciar usuários | ✅ | ❌ | ❌ | ❌ |
| Configurar tenants | ✅ | ❌ | ❌ | ❌ |
| Criar/Editar apps | ✅ | ✅ | ✅ | ❌ |
| Deletar apps | ✅ | ✅ | ❌ | ❌ |
| Fazer deploy | ✅ | ✅ | ✅ | ❌ |
| Ver builds/logs | ✅ | ✅ | ✅ | ✅ |
| Gerenciar DBs | ✅ | ✅ | ❌ | ❌ |
| Criar usuários DB | ✅ | ✅ | ❌ | ❌ |
| Upload arquivos | ✅ | ✅ | ✅ | ❌ |
| Ver auditoria | ✅ | ✅ | ❌ | ❌ |
| Configurar XUI One | ✅ | ❌ | ❌ | ❌ |

### 2. Segurança do Banco XUI One

```sql
-- Usuário dedicado para leitura apenas
CREATE USER 'saas_reader'@'%' IDENTIFIED BY 'senha_forte_aqui';

-- Permissões MÍNIMAS necessárias
GRANT SELECT, SHOW VIEW ON xui_one.* TO 'saas_reader'@'%';
GRANT SHOW DATABASES ON *.* TO 'saas_reader'@'%';

-- NEGAR explicitamente operações de escrita
-- (Não conceder: INSERT, UPDATE, DELETE, CREATE, DROP, ALTER)

-- Restringir acesso por IP (se possível)
-- CREATE USER 'saas_reader'@'10.0.0.%' IDENTIFIED BY 'senha_forte_aqui';

FLUSH PRIVILEGES;
```

---

## 📂 ESTRUTURA DE DIRETÓRIOS NO UBUNTU

```
/opt/saas-gestor/                    # Diretório principal
├── docker-compose.yml               # Orquestração
├── .env                             # Variáveis de ambiente
├── .env.example                     # Template
├── nginx/                           # Configuração nginx
│   ├── nginx.conf
│   └── ssl/                         # Certificados
├── backend/                         # Código NestJS (montado)
├── frontend/                        # Build React (montado)
├── scripts/                         # Scripts utilitários
│   ├── backup.sh
│   ├── deploy.sh
│   └── setup.sh
├── data/                            # Dados persistentes
│   ├── mariadb/                     # Banco de dados
│   ├── redis/                       # Cache
│   ├── minio/                       # Arquivos
│   └── logs/                        # Logs
└── backups/                         # Backups automáticos
    ├── daily/
    ├── weekly/
    └── monthly/

/var/log/saas-gestor/                # Logs do sistema
├── app.log
├── access.log
├── error.log
└── audit/

/home/deploy/                        # Deploys de aplicações
├── app-1/
│   ├── releases/                    # Versões (rollback)
│   │   ├── 20240210-120000/
│   │   ├── 20240210-130000/
│   │   └── current -> 20240210-130000/
│   └── shared/                      # Dados compartilhados
│       ├── uploads/
│       ├── logs/
│       └── .env
└── app-2/
    └── ...
```

---

## 🚀 PRÓXIMOS PASSOS

1. **Instalação no Ubuntu 22.04** → Ver `02_INSTALLACAO_UBUNTU.md`
2. **Configuração Docker** → Ver `03_DOCKER_SETUP.md`
3. **API Specification** → Ver `04_API_SPECIFICATION.md`
4. **Dashboard em Tempo Real** → Ver `05_DASHBOARD_REALTIME.md`

---

**Documento criado em:** Fevereiro 2026  
**Versão:** 1.0.0  
**Status:** Arquitetura aprovada para implementação
