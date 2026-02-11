# 📡 API RESTful - Especificação Completa

## 1. Visão Geral

**Base URL:** `https://api.seudominio.com/v1`  
**Autenticação:** JWT Bearer Token  
**Formato:** JSON  
**Paginação:** Limit/Offset ou Cursor-based

---

## 2. Autenticação

### POST /auth/login
Login de usuário.

**Request:**
```json
{
  "email": "admin@empresa.com",
  "password": "senhaSegura123"
}
```

**Response 200:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
  "expiresIn": 3600,
  "user": {
    "id": 1,
    "name": "Administrador",
    "email": "admin@empresa.com",
    "role": "admin",
    "tenantId": 1
  }
}
```

### POST /auth/refresh
Renovar token de acesso.

**Request:**
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4..."
}
```

**Response 200:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

### POST /auth/logout
Logout (invalida refresh token).

**Headers:**
```
Authorization: Bearer <token>
```

---

## 3. Usuários

### GET /users
Listar usuários do tenant.

**Headers:**
```
Authorization: Bearer <token>
```

**Query Params:**
```
?page=1&limit=10&role=developer&status=active
```

**Response 200:**
```json
{
  "data": [
    {
      "id": 1,
      "name": "João Silva",
      "email": "joao@empresa.com",
      "role": "admin",
      "status": "active",
      "lastLogin": "2026-02-10T15:30:00Z",
      "createdAt": "2026-01-15T10:00:00Z"
    }
  ],
  "meta": {
    "total": 25,
    "page": 1,
    "limit": 10,
    "totalPages": 3
  }
}
```

### POST /users
Criar novo usuário.

**Request:**
```json
{
  "name": "Maria Santos",
  "email": "maria@empresa.com",
  "role": "developer",
  "password": "senhaTemporaria123"
}
```

**Response 201:**
```json
{
  "id": 26,
  "name": "Maria Santos",
  "email": "maria@empresa.com",
  "role": "developer",
  "status": "active",
  "createdAt": "2026-02-10T15:35:00Z"
}
```

### GET /users/:id
Obter detalhes de um usuário.

### PUT /users/:id
Atualizar usuário.

### DELETE /users/:id
Remover usuário (soft delete).

---

## 4. Aplicações

### GET /applications
Listar aplicações.

**Query Params:**
```
?status=active&environment=production&page=1&limit=20
```

**Response 200:**
```json
{
  "data": [
    {
      "id": 1,
      "name": "E-commerce API",
      "slug": "ecommerce-api",
      "description": "API principal do e-commerce",
      "appType": "node",
      "environment": "production",
      "status": "active",
      "domain": "api.ecommerce.com",
      "port": 3000,
      "repositoryUrl": "https://github.com/empresa/ecommerce-api",
      "repositoryBranch": "main",
      "version": "v2.3.1",
      "lastDeployAt": "2026-02-10T14:45:00Z",
      "createdAt": "2026-01-01T00:00:00Z",
      "updatedAt": "2026-02-10T14:45:00Z"
    }
  ],
  "meta": {
    "total": 12,
    "page": 1,
    "limit": 20
  }
}
```

### POST /applications
Criar nova aplicação.

**Request:**
```json
{
  "name": "Blog Frontend",
  "slug": "blog-frontend",
  "description": "Frontend do blog corporativo",
  "appType": "static",
  "environment": "production",
  "repositoryUrl": "https://github.com/empresa/blog",
  "repositoryBranch": "main",
  "buildCommand": "npm run build",
  "startCommand": "npx serve dist",
  "envVars": {
    "NODE_ENV": "production",
    "API_URL": "https://api.empresa.com"
  }
}
```

**Response 201:**
```json
{
  "id": 13,
  "name": "Blog Frontend",
  "slug": "blog-frontend",
  "status": "inactive",
  "createdAt": "2026-02-10T15:40:00Z"
}
```

### GET /applications/:id
Detalhes da aplicação.

### PUT /applications/:id
Atualizar aplicação.

### DELETE /applications/:id
Remover aplicação.

### POST /applications/:id/deploy
Fazer deploy da aplicação.

**Request:**
```json
{
  "commitHash": "abc123",
  "branch": "main",
  "environment": "production"
}
```

**Response 202:**
```json
{
  "message": "Deploy iniciado",
  "buildId": 156,
  "buildNumber": 23,
  "status": "queued"
}
```

### POST /applications/:id/rollback
Rollback para versão anterior.

**Request:**
```json
{
  "buildId": 155
}
```

---

## 5. Builds

### GET /builds
Listar builds.

**Query Params:**
```
?applicationId=1&status=running&page=1&limit=20
```

**Response 200:**
```json
{
  "data": [
    {
      "id": 156,
      "buildNumber": 23,
      "applicationId": 1,
      "applicationName": "E-commerce API",
      "status": "running",
      "stage": "testing",
      "progress": 75,
      "commit": {
        "hash": "abc1234",
        "message": "Fix payment gateway timeout",
        "author": "maria.dev",
        "timestamp": "2026-02-10T15:25:00Z"
      },
      "startedAt": "2026-02-10T15:28:00Z",
      "estimatedEnd": "2026-02-10T15:32:00Z",
      "triggeredBy": "maria.dev"
    }
  ],
  "meta": {
    "total": 45
  }
}
```

### GET /builds/:id
Detalhes do build.

**Response 200:**
```json
{
  "id": 156,
  "buildNumber": 23,
  "applicationId": 1,
  "status": "success",
  "logs": "[2026-02-10T15:28:00Z] INFO: Starting build...\n[2026-02-10T15:28:05Z] INFO: Installing dependencies...",
  "duration": 245,
  "artifactPath": "builds/app-1/build-23.tar.gz"
}
```

### POST /builds/:id/cancel
Cancelar build em execução.

---

## 6. Bancos de Dados

### GET /databases
Listar conexões de banco de dados.

**Response 200:**
```json
{
  "data": [
    {
      "id": 1,
      "name": "MariaDB Principal",
      "connectionType": "internal",
      "host": "mariadb",
      "port": 3306,
      "databaseName": "saas_gestor",
      "status": "active",
      "lastCheckAt": "2026-02-10T15:30:00Z",
      "lastCheckStatus": "healthy",
      "lastCheckLatencyMs": 12
    },
    {
      "id": 2,
      "name": "XUI One DB",
      "connectionType": "xui_one",
      "host": "xui-db.internal",
      "port": 3306,
      "isReadOnly": true,
      "status": "active",
      "lastCheckAt": "2026-02-10T15:30:00Z",
      "lastCheckStatus": "healthy",
      "lastCheckLatencyMs": 25
    }
  ]
}
```

### POST /databases
Criar nova conexão de banco.

**Request:**
```json
{
  "name": "Analytics DB",
  "connectionType": "external",
  "host": "db.analytics.com",
  "port": 3306,
  "databaseName": "analytics_prod",
  "username": "analytics_user",
  "password": "senhaSegura"
}
```

**Response 201:**
```json
{
  "id": 7,
  "name": "Analytics DB",
  "status": "active",
  "message": "Conexão testada com sucesso"
}
```

### GET /databases/:id/tables
Listar tabelas (XUI One ou outro).

**Response 200:**
```json
{
  "databaseId": 2,
  "tables": [
    {
      "name": "users",
      "rows": 15420,
      "size": "2.5MB"
    },
    {
      "name": "streams",
      "rows": 892,
      "size": "1.2MB"
    }
  ]
}
```

### GET /databases/:id/query
Executar query (apenas SELECT para XUI One).

**Query Params:**
```
?sql=SELECT * FROM users LIMIT 10
```

**Response 200:**
```json
{
  "columns": ["id", "username", "email"],
  "rows": [
    [1, "joao", "joao@email.com"],
    [2, "maria", "maria@email.com"]
  ],
  "rowCount": 2,
  "executionTimeMs": 15
}
```

### POST /databases/:id/users
Criar usuário de banco.

**Request:**
```json
{
  "username": "app_readonly",
  "password": "senhaSegura123",
  "grants": ["SELECT", "SHOW VIEW"]
}
```

### POST /databases/:id/backup
Criar backup do banco.

**Response 202:**
```json
{
  "message": "Backup iniciado",
  "jobId": "backup-12345"
}
```

---

## 7. Arquivos (Storage)

### GET /files
Listar arquivos.

**Query Params:**
```
?applicationId=1&page=1&limit=50
```

**Response 200:**
```json
{
  "data": [
    {
      "id": 1,
      "filename": "documento.pdf",
      "originalName": "Contrato_Serviço.pdf",
      "mimeType": "application/pdf",
      "sizeBytes": 1024576,
      "sizeFormatted": "1.0 MB",
      "isPublic": false,
      "uploadedBy": "joao.silva",
      "createdAt": "2026-02-10T10:00:00Z"
    }
  ]
}
```

### POST /files/upload
Upload de arquivo.

**Content-Type:** `multipart/form-data`

**Request:**
```
file: <binary>
applicationId: 1
isPublic: false
```

**Response 201:**
```json
{
  "id": 2,
  "filename": "imagem.png",
  "sizeFormatted": "2.5 MB",
  "downloadUrl": "/files/2/download"
}
```

### GET /files/:id/download
Download de arquivo.

**Response:** Binary file

### DELETE /files/:id
Remover arquivo.

---

## 8. Dashboard

### GET /dashboard/summary
Resumo para o dashboard.

**Response 200:**
```json
{
  "timestamp": "2026-02-10T15:30:00Z",
  "summary": {
    "applications": {
      "total": 12,
      "online": 10,
      "degraded": 1,
      "offline": 1
    },
    "builds": {
      "running": 2,
      "queued": 0,
      "failedLast24h": 1,
      "successLast24h": 15
    },
    "databases": {
      "total": 5,
      "healthy": 4,
      "warning": 1,
      "error": 0
    },
    "lastDeploy": {
      "appName": "E-commerce API",
      "version": "v2.3.1",
      "timestamp": "2026-02-10T14:45:00Z",
      "status": "success"
    }
  }
}
```

### GET /dashboard/applications-status
Status das aplicações (para WebSocket).

### GET /dashboard/services-status
Status dos serviços.

### GET /dashboard/builds-queue
Fila de builds.

### GET /dashboard/events
Eventos recentes.

---

## 9. Auditoria

### GET /audit-logs
Logs de auditoria.

**Query Params:**
```
?action=CREATE_APP&severity=error&startDate=2026-02-01&endDate=2026-02-10
```

**Response 200:**
```json
{
  "data": [
    {
      "id": 1024,
      "action": "CREATE_APP",
      "entityType": "application",
      "entityId": 13,
      "user": {
        "id": 1,
        "name": "Admin",
        "email": "admin@empresa.com"
      },
      "newValues": {
        "name": "Blog Frontend",
        "slug": "blog-frontend"
      },
      "ipAddress": "192.168.1.100",
      "severity": "info",
      "createdAt": "2026-02-10T15:40:00Z"
    }
  ]
}
```

---

## 10. Erros

### Estrutura de Erro

```json
{
  "statusCode": 400,
  "message": "Dados inválidos",
  "error": "Bad Request",
  "details": [
    {
      "field": "email",
      "message": "Email já cadastrado"
    }
  ],
  "timestamp": "2026-02-10T15:45:00Z"
}
```

### Códigos HTTP

| Código | Significado |
|--------|-------------|
| 200 | OK |
| 201 | Created |
| 202 | Accepted (processamento assíncrono) |
| 400 | Bad Request |
| 401 | Unauthorized |
| 403 | Forbidden |
| 404 | Not Found |
| 409 | Conflict |
| 422 | Unprocessable Entity |
| 429 | Too Many Requests |
| 500 | Internal Server Error |

---

## 11. Rate Limiting

- **Limite geral:** 1000 requests/hora por IP
- **Auth:** 10 requests/minuto (login)
- **API:** 100 requests/minuto por usuário

**Headers de resposta:**
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1644518400
```

---

**Versão:** 1.0.0  
**Atualizado:** Fevereiro 2026
