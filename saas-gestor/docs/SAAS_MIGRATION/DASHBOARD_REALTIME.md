# 📊 DASHBOARD EM TEMPO REAL - ESPECIFICAÇÃO COMPLETA

## 1. TECNOLOGIA ESCOLHIDA: WebSocket (Socket.io)

### Por que WebSocket ao invés de SSE ou Polling?

| Critério | WebSocket | SSE | Polling |
|----------|-----------|-----|---------|
| **Latência** | ✅ < 50ms | ✅ < 100ms | ❌ 1-5s |
| **Bidirecional** | ✅ Sim | ❌ Não | ✅ Sim |
| **Overhead** | ✅ Baixo | ✅ Baixo | ❌ Alto |
| **Complexidade** | ⚠️ Média | ✅ Baixa | ✅ Baixa |
| **Reconexão** | ✅ Automática | ✅ Automática | ❌ Manual |
| **Browser Support** | ✅ 97%+ | ✅ 95%+ | ✅ 100% |

**Decisão:** WebSocket (Socket.io) por permitir comunicação bidirecional (não só receber, mas também enviar comandos do dashboard para o backend, como "forçar refresh" ou "cancelar build").

---

## 2. ESTRUTURA DO WEBSOCKET

### 2.1 Eventos (Namespaces)

```javascript
// Conexão principal do dashboard
const socket = io('/dashboard', {
  auth: {
    token: 'JWT_TOKEN_AQUI'
  }
});

// Eventos que o SERVIDOR envia para o CLIENTE
socket.on('dashboard:summary', (data) => { ... });
socket.on('dashboard:apps', (data) => { ... });
socket.on('dashboard:services', (data) => { ... });
socket.on('dashboard:builds', (data) => { ... });
socket.on('dashboard:events', (data) => { ... });
socket.on('dashboard:metrics', (data) => { ... });
socket.on('dashboard:alert', (data) => { ... });

// Eventos que o CLIENTE envia para o SERVIDOR
socket.emit('dashboard:subscribe', { tenantId: 123 });
socket.emit('dashboard:refresh', { type: 'apps' });
socket.emit('dashboard:acknowledge', { eventId: 456 });
```

### 2.2 Fluxo de Conexão

```
┌─────────────┐                    ┌─────────────┐
│   CLIENTE   │                    │   SERVIDOR  │
│   (React)   │                    │  (NestJS)   │
└──────┬──────┘                    └──────┬──────┘
       │                                   │
       │  1. CONNECT /dashboard            │
       │──────────────────────────────────>│
       │                                   │
       │  2. AUTH (JWT validation)         │
       │                                   │
       │  3. JOIN ROOM tenant:123          │
       │<──────────────────────────────────│
       │                                   │
       │  4. REQUEST INITIAL DATA          │
       │  GET /api/dashboard/initial       │
       │──────────────────────────────────>│
       │                                   │
       │  5. START REAL-TIME UPDATES       │
       │  (a cada 5s ou eventos)           │
       │<══════════════════════════════════│
       │  dashboard:summary                │
       │<══════════════════════════════════│
       │  dashboard:apps                   │
       │<══════════════════════════════════│
       │  dashboard:metrics                │
       │                                   │
```

---

## 3. PAYLOADS JSON (Exemplos Reais)

### 3.1 dashboard:summary

```json
{
  "timestamp": "2026-02-10T15:30:00.000Z",
  "tenantId": 123,
  "summary": {
    "applications": {
      "total": 12,
      "online": 10,
      "degraded": 1,
      "offline": 1,
      "error": 0
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
      "timestamp": "2026-02-10T14:45:00.000Z",
      "status": "success",
      "deployedBy": "joao.silva"
    }
  }
}
```

### 3.2 dashboard:apps

```json
{
  "timestamp": "2026-02-10T15:30:05.000Z",
  "applications": [
    {
      "id": 1,
      "name": "E-commerce API",
      "slug": "ecommerce-api",
      "environment": "production",
      "status": "online",
      "statusDetails": {
        "healthCheck": "healthy",
        "lastCheck": "2026-02-10T15:29:55.000Z",
        "responseTimeMs": 45
      },
      "version": "v2.3.1",
      "lastDeploy": {
        "timestamp": "2026-02-10T14:45:00.000Z",
        "buildId": 156
      },
      "resources": {
        "cpu": {
          "usage": 23.5,
          "limit": 100,
          "unit": "percent"
        },
        "memory": {
          "usage": 512,
          "limit": 1024,
          "unit": "MB"
        }
      },
      "url": "https://api.ecommerce.com"
    },
    {
      "id": 2,
      "name": "Blog Frontend",
      "slug": "blog-frontend",
      "environment": "production",
      "status": "degraded",
      "statusDetails": {
        "healthCheck": "slow",
        "lastCheck": "2026-02-10T15:29:50.000Z",
        "responseTimeMs": 2500,
        "error": "High response time"
      },
      "version": "v1.8.0",
      "lastDeploy": {
        "timestamp": "2026-02-10T10:30:00.000Z",
        "buildId": 152
      },
      "resources": {
        "cpu": {
          "usage": 78.2,
          "limit": 100,
          "unit": "percent"
        },
        "memory": {
          "usage": 896,
          "limit": 1024,
          "unit": "MB"
        }
      },
      "url": "https://blog.com"
    },
    {
      "id": 3,
      "name": "Payment Service",
      "slug": "payment-service",
      "environment": "production",
      "status": "offline",
      "statusDetails": {
        "healthCheck": "failed",
        "lastCheck": "2026-02-10T15:29:30.000Z",
        "error": "Connection refused"
      },
      "version": "v1.2.5",
      "lastDeploy": {
        "timestamp": "2026-02-09T18:00:00.000Z",
        "buildId": 148
      },
      "resources": null,
      "url": "https://payments.com"
    }
  ]
}
```

### 3.3 dashboard:services

```json
{
  "timestamp": "2026-02-10T15:30:10.000Z",
  "services": [
    {
      "name": "MariaDB Principal",
      "type": "database",
      "status": "healthy",
      "latencyMs": 12,
      "lastCheck": "2026-02-10T15:30:05.000Z",
      "details": {
        "version": "10.6.12",
        "connections": 45,
        "maxConnections": 100,
        "uptime": "15d 7h 23m"
      }
    },
    {
      "name": "MariaDB XUI One",
      "type": "database",
      "status": "healthy",
      "latencyMs": 25,
      "lastCheck": "2026-02-10T15:30:05.000Z",
      "details": {
        "host": "xui-db.internal",
        "readOnly": true,
        "tables": 23
      }
    },
    {
      "name": "Redis Cache",
      "type": "cache",
      "status": "healthy",
      "latencyMs": 1,
      "lastCheck": "2026-02-10T15:30:05.000Z",
      "details": {
        "usedMemory": "156MB",
        "hitRate": 94.5
      }
    },
    {
      "name": "MinIO Storage",
      "type": "storage",
      "status": "healthy",
      "latencyMs": 8,
      "lastCheck": "2026-02-10T15:30:05.000Z",
      "details": {
        "buckets": 5,
        "totalSize": "2.3GB"
      }
    },
    {
      "name": "Worker Builds",
      "type": "worker",
      "status": "busy",
      "latencyMs": null,
      "lastCheck": "2026-02-10T15:30:05.000Z",
      "details": {
        "queueSize": 2,
        "processing": 2,
        "failed": 0
      }
    }
  ]
}
```

### 3.4 dashboard:builds

```json
{
  "timestamp": "2026-02-10T15:30:15.000Z",
  "builds": [
    {
      "id": 158,
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
        "timestamp": "2026-02-10T15:25:00.000Z"
      },
      "startedAt": "2026-02-10T15:28:00.000Z",
      "estimatedEnd": "2026-02-10T15:32:00.000Z",
      "triggeredBy": "maria.dev"
    },
    {
      "id": 159,
      "buildNumber": 8,
      "applicationId": 4,
      "applicationName": "Mobile API",
      "status": "queued",
      "stage": null,
      "progress": 0,
      "commit": {
        "hash": "def5678",
        "message": "Add push notifications",
        "author": "joao.silva",
        "timestamp": "2026-02-10T15:29:00.000Z"
      },
      "startedAt": null,
      "estimatedEnd": null,
      "triggeredBy": "joao.silva"
    },
    {
      "id": 157,
      "buildNumber": 15,
      "applicationId": 2,
      "applicationName": "Blog Frontend",
      "status": "completed",
      "stage": "deploy",
      "progress": 100,
      "commit": {
        "hash": "ghi9012",
        "message": "Update hero banner",
        "author": "ana.design",
        "timestamp": "2026-02-10T15:20:00.000Z"
      },
      "startedAt": "2026-02-10T15:22:00.000Z",
      "completedAt": "2026-02-10T15:24:30.000Z",
      "duration": 150,
      "triggeredBy": "ana.design"
    }
  ]
}
```

### 3.5 dashboard:events

```json
{
  "timestamp": "2026-02-10T15:30:20.000Z",
  "events": [
    {
      "id": 1024,
      "type": "db_created",
      "severity": "info",
      "title": "Banco de dados criado",
      "message": "Database 'analytics_prod' criado com sucesso para aplicação Analytics",
      "metadata": {
        "databaseId": 7,
        "databaseName": "analytics_prod",
        "applicationId": 5
      },
      "user": {
        "id": 3,
        "name": "joao.silva",
        "role": "devops"
      },
      "createdAt": "2026-02-10T15:28:00.000Z",
      "isRead": false
    },
    {
      "id": 1023,
      "type": "build_failed",
      "severity": "error",
      "title": "Build falhou",
      "message": "Falha no teste de integração do Payment Service",
      "metadata": {
        "buildId": 156,
        "applicationId": 3,
        "errorLog": "Test timeout after 30000ms"
      },
      "user": {
        "id": 4,
        "name": "maria.dev",
        "role": "developer"
      },
      "createdAt": "2026-02-10T15:25:00.000Z",
      "isRead": true
    },
    {
      "id": 1022,
      "type": "app_deployed",
      "severity": "info",
      "title": "Deploy realizado",
      "message": "E-commerce API v2.3.1 deployed com sucesso",
      "metadata": {
        "buildId": 155,
        "applicationId": 1,
        "version": "v2.3.1"
      },
      "user": {
        "id": 3,
        "name": "joao.silva",
        "role": "devops"
      },
      "createdAt": "2026-02-10T14:45:00.000Z",
      "isRead": true
    }
  ]
}
```

### 3.6 dashboard:metrics

```json
{
  "timestamp": "2026-02-10T15:30:25.000Z",
  "metrics": {
    "server": {
      "cpu": {
        "usage": 45.2,
        "cores": 4,
        "unit": "percent"
      },
      "memory": {
        "used": 6144,
        "total": 8192,
        "unit": "MB",
        "percent": 75.0
      },
      "disk": {
        "used": 45056,
        "total": 102400,
        "unit": "MB",
        "percent": 44.0
      }
    },
    "mariadb": {
      "connections": {
        "active": 45,
        "max": 100
      },
      "queriesPerSecond": 1250,
      "slowQueries": 2
    },
    "network": {
      "connectionsActive": 234,
      "bandwidthIn": 1024,
      "bandwidthOut": 2048,
      "unit": "KB/s"
    }
  },
  "history": [
    {"timestamp": "15:25:00", "cpu": 42.1, "memory": 73.5},
    {"timestamp": "15:26:00", "cpu": 44.3, "memory": 74.2},
    {"timestamp": "15:27:00", "cpu": 43.8, "memory": 74.8},
    {"timestamp": "15:28:00", "cpu": 45.0, "memory": 75.0},
    {"timestamp": "15:29:00", "cpu": 44.5, "memory": 74.9}
  ]
}
```

---

## 4. LAYOUT DA TELA (Wireframe)

### 4.1 Estrutura Visual

```
┌─────────────────────────────────────────────────────────────────┐
│  🏠 DASHBOARD                    [🔔 3]  [👤 Admin ▼]  [⚙️]    │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐ ┌─────────┐               │
│  │   12    │ │   10    │ │    1    │ │   15    │               │
│  │  Apps   │ │ Online  │ │ Falhas  │ │ Builds  │               │
│  │  Total  │ │         │ │ 24h     │ │ Hoje    │               │
│  └─────────┘ └─────────┘ └─────────┘ └─────────┘               │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ 📱 APLICAÇÕES                              [🔍] [Filtros]│   │
│  │ ┌─────────────────────────────────────────────────────┐ │   │
│  │ │ Nome          │ Ambiente│ Status  │ Versão │ Ações │ │   │
│  │ ├─────────────────────────────────────────────────────┤ │   │
│  │ │ E-commerce API│ Prod    │ 🟢 Online│ v2.3.1 │ ▼    │ │   │
│  │ │ Blog Frontend │ Prod    │ 🟡 Degrad│ v1.8.0 │ ▼    │ │   │
│  │ │ Payment Sv    │ Prod    │ 🔴 Offline│ v1.2.5 │ ▼    │ │   │
│  │ └─────────────────────────────────────────────────────┘ │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌────────────────────────┐ ┌──────────────────────────────┐   │
│  │ 🔧 SERVIÇOS            │ │ 🔄 BUILDS & DEPLOYS          │   │
│  │                        │ │                              │   │
│  │ 🟢 MariaDB Principal   │ │ [████░░░░░░] E-commerce API  │   │
│  │ 🟢 MariaDB XUI One     │ │ [░░░░░░░░░░] Mobile API      │   │
│  │ 🟢 Redis Cache         │ │ [✓] Blog Frontend            │   │
│  │ 🟢 MinIO Storage       │ │                              │   │
│  │ 🟡 Worker Builds       │ │                              │   │
│  │                        │ │                              │   │
│  └────────────────────────┘ └──────────────────────────────┘   │
│                                                                  │
│  ┌────────────────────────┐ ┌──────────────────────────────┐   │
│  │ 📊 MÉTRICAS DO SERVIDOR│ │ 📝 EVENTOS RECENTES          │   │
│  │                        │ │                              │   │
│  │   CPU    ████░░░░ 45%  │ │ 🟢 DB criado                 │   │
│  │   Memória██████░░ 75%  │ │ 🔴 Build falhou              │   │
│  │   Disco  ███░░░░░ 44%  │ │ 🟢 Deploy ok                 │   │
│  │                        │ │ ⚠️  Alto uso CPU             │   │
│  │   [Gráfico linha]      │ │                              │   │
│  │                        │ │ [Ver todos →]                │   │
│  └────────────────────────┘ └──────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 Paleta de Cores

```css
/* Status Colors */
--status-online: #10B981;      /* Verde */
--status-degraded: #F59E0B;    /* Amarelo/Laranja */
--status-offline: #EF4444;     /* Vermelho */
--status-error: #DC2626;       /* Vermelho escuro */
--status-pending: #6B7280;     /* Cinza */
--status-running: #3B82F6;     /* Azul */
--status-success: #10B981;     /* Verde */
--status-warning: #F59E0B;     /* Amarelo */

/* Severity Colors */
--severity-info: #3B82F6;      /* Azul */
--severity-warning: #F59E0B;   /* Amarelo */
--severity-error: #EF4444;     /* Vermelho */
--severity-critical: #7F1D1D;  /* Vermelho escuro */

/* UI Colors */
--bg-primary: #FFFFFF;
--bg-secondary: #F3F4F6;
--bg-tertiary: #E5E7EB;
--text-primary: #111827;
--text-secondary: #6B7280;
--border-color: #E5E7EB;
```

---

## 5. IMPLEMENTAÇÃO REACT

### 5.1 Hook Customizado: useDashboardSocket

```typescript
// hooks/useDashboardSocket.ts
import { useEffect, useState, useCallback } from 'react';
import { io, Socket } from 'socket.io-client';

interface DashboardData {
  summary: any;
  applications: any[];
  services: any[];
  builds: any[];
  events: any[];
  metrics: any;
}

export const useDashboardSocket = (tenantId: number) => {
  const [socket, setSocket] = useState<Socket | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [data, setData] = useState<DashboardData>({
    summary: null,
    applications: [],
    services: [],
    builds: [],
    events: [],
    metrics: null
  });
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const token = localStorage.getItem('auth_token');
    
    const newSocket = io('/dashboard', {
      auth: { token },
      transports: ['websocket'],
      reconnection: true,
      reconnectionAttempts: 5,
      reconnectionDelay: 1000
    });

    newSocket.on('connect', () => {
      console.log('✅ Dashboard conectado');
      setIsConnected(true);
      setError(null);
      
      // Assinar tenant
      newSocket.emit('dashboard:subscribe', { tenantId });
    });

    newSocket.on('disconnect', () => {
      console.log('❌ Dashboard desconectado');
      setIsConnected(false);
    });

    newSocket.on('connect_error', (err) => {
      console.error('Erro de conexão:', err);
      setError('Falha na conexão com servidor');
    });

    // Handlers de dados
    newSocket.on('dashboard:summary', (payload) => {
      setData(prev => ({ ...prev, summary: payload.summary }));
    });

    newSocket.on('dashboard:apps', (payload) => {
      setData(prev => ({ ...prev, applications: payload.applications }));
    });

    newSocket.on('dashboard:services', (payload) => {
      setData(prev => ({ ...prev, services: payload.services }));
    });

    newSocket.on('dashboard:builds', (payload) => {
      setData(prev => ({ ...prev, builds: payload.builds }));
    });

    newSocket.on('dashboard:events', (payload) => {
      setData(prev => ({ ...prev, events: payload.events }));
    });

    newSocket.on('dashboard:metrics', (payload) => {
      setData(prev => ({ ...prev, metrics: payload }));
    });

    newSocket.on('dashboard:alert', (payload) => {
      // Mostrar toast/notification
      showNotification(payload.title, payload.message, payload.severity);
    });

    setSocket(newSocket);

    return () => {
      newSocket.close();
    };
  }, [tenantId]);

  const refreshData = useCallback((type?: string) => {
    socket?.emit('dashboard:refresh', { type });
  }, [socket]);

  const acknowledgeEvent = useCallback((eventId: number) => {
    socket?.emit('dashboard:acknowledge', { eventId });
  }, [socket]);

  return {
    isConnected,
    data,
    error,
    refreshData,
    acknowledgeEvent
  };
};
```

### 5.2 Componente Dashboard

```typescript
// components/Dashboard/index.tsx
import React from 'react';
import { useDashboardSocket } from '../../hooks/useDashboardSocket';
import { SummaryCards } from './SummaryCards';
import { ApplicationsTable } from './ApplicationsTable';
import { ServicesStatus } from './ServicesStatus';
import { BuildsTimeline } from './BuildsTimeline';
import { EventsFeed } from './EventsFeed';
import { ServerMetrics } from './ServerMetrics';
import { ConnectionStatus } from './ConnectionStatus';

export const Dashboard: React.FC = () => {
  const { isConnected, data, error, refreshData } = useDashboardSocket(123);

  if (error) {
    return (
      <div className="dashboard-error">
        <h2>Erro de Conexão</h2>
        <p>{error}</p>
        <button onClick={() => refreshData()}>Tentar Reconectar</button>
      </div>
    );
  }

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <h1>Dashboard</h1>
        <ConnectionStatus isConnected={isConnected} />
        <button onClick={() => refreshData()} className="refresh-btn">
          🔄 Atualizar
        </button>
      </header>

      {/* Cards de Resumo */}
      {data.summary && <SummaryCards data={data.summary} />}

      {/* Grid Principal */}
      <div className="dashboard-grid">
        {/* Coluna Esquerda */}
        <div className="dashboard-col-left">
          <ApplicationsTable 
            applications={data.applications} 
            onAppClick={(app) => console.log('App clicked:', app)}
          />
        </div>

        {/* Coluna Direita */}
        <div className="dashboard-col-right">
          <ServicesStatus services={data.services} />
          <BuildsTimeline builds={data.builds} />
        </div>
      </div>

      {/* Linha Inferior */}
      <div className="dashboard-grid-bottom">
        <ServerMetrics metrics={data.metrics} />
        <EventsFeed 
          events={data.events} 
          onAcknowledge={(id) => console.log('Ack:', id)}
        />
      </div>
    </div>
  );
};
```

### 5.3 Componente de Status (Exemplo)

```typescript
// components/Dashboard/StatusBadge.tsx
import React from 'react';

interface StatusBadgeProps {
  status: 'online' | 'degraded' | 'offline' | 'error' | 'running' | 'success' | 'warning';
  showIcon?: boolean;
  size?: 'sm' | 'md' | 'lg';
}

const statusConfig = {
  online: { color: '#10B981', icon: '🟢', label: 'Online' },
  degraded: { color: '#F59E0B', icon: '🟡', label: 'Degradado' },
  offline: { color: '#EF4444', icon: '🔴', label: 'Offline' },
  error: { color: '#DC2626', icon: '❌', label: 'Erro' },
  running: { color: '#3B82F6', icon: '🔄', label: 'Executando' },
  success: { color: '#10B981', icon: '✅', label: 'Sucesso' },
  warning: { color: '#F59E0B', icon: '⚠️', label: 'Aviso' }
};

export const StatusBadge: React.FC<StatusBadgeProps> = ({ 
  status, 
  showIcon = true,
  size = 'md'
}) => {
  const config = statusConfig[status];
  
  const sizeClasses = {
    sm: 'px-2 py-0.5 text-xs',
    md: 'px-3 py-1 text-sm',
    lg: 'px-4 py-2 text-base'
  };

  return (
    <span 
      className={`status-badge ${sizeClasses[size]}`}
      style={{ 
        backgroundColor: `${config.color}20`,
        color: config.color,
        borderRadius: '9999px',
        fontWeight: 600,
        display: 'inline-flex',
        alignItems: 'center',
        gap: '0.5rem'
      }}
    >
      {showIcon && <span>{config.icon}</span>}
      <span>{config.label}</span>
    </span>
  );
};
```

---

## 6. BACKEND (NestJS) - WebSocket Gateway

```typescript
// dashboard/dashboard.gateway.ts
import {
  WebSocketGateway,
  WebSocketServer,
  SubscribeMessage,
  OnGatewayConnection,
  OnGatewayDisconnect,
} from '@nestjs/websockets';
import { Server, Socket } from 'socket.io';
import { UseGuards } from '@nestjs/common';
import { WsJwtGuard } from '../auth/ws-jwt.guard';
import { DashboardService } from './dashboard.service';

@WebSocketGateway({
  namespace: 'dashboard',
  cors: { origin: '*' }
})
@UseGuards(WsJwtGuard)
export class DashboardGateway implements OnGatewayConnection, OnGatewayDisconnect {
  @WebSocketServer()
  server: Server;

  constructor(private readonly dashboardService: DashboardService) {}

  handleConnection(client: Socket) {
    console.log(`Client connected: ${client.id}`);
  }

  handleDisconnect(client: Socket) {
    console.log(`Client disconnected: ${client.id}`);
  }

  @SubscribeMessage('dashboard:subscribe')
  async handleSubscribe(client: Socket, payload: { tenantId: number }) {
    const { tenantId } = payload;
    
    // Validar se usuário tem acesso ao tenant
    const user = (client as any).user;
    if (user.tenantId !== tenantId && user.role !== 'admin') {
      client.emit('error', { message: 'Acesso negado' });
      return;
    }

    // Entrar na sala do tenant
    client.join(`tenant:${tenantId}`);
    
    // Enviar dados iniciais
    const initialData = await this.dashboardService.getInitialData(tenantId);
    client.emit('dashboard:initial', initialData);

    // Iniciar intervalo de atualizações
    this.startRealtimeUpdates(client, tenantId);
  }

  @SubscribeMessage('dashboard:refresh')
  handleRefresh(client: Socket, payload: { type?: string }) {
    const tenantId = this.getTenantIdFromClient(client);
    
    if (payload.type) {
      this.sendSpecificUpdate(client, tenantId, payload.type);
    } else {
      this.sendAllUpdates(client, tenantId);
    }
  }

  private startRealtimeUpdates(client: Socket, tenantId: number) {
    // Atualização a cada 5 segundos
    const interval = setInterval(async () => {
      if (!client.connected) {
        clearInterval(interval);
        return;
      }

      await this.sendAllUpdates(client, tenantId);
    }, 5000);

    client.on('disconnect', () => {
      clearInterval(interval);
    });
  }

  private async sendAllUpdates(client: Socket, tenantId: number) {
    const [
      summary,
      applications,
      services,
      builds,
      events,
      metrics
    ] = await Promise.all([
      this.dashboardService.getSummary(tenantId),
      this.dashboardService.getApplications(tenantId),
      this.dashboardService.getServices(tenantId),
      this.dashboardService.getBuilds(tenantId),
      this.dashboardService.getEvents(tenantId),
      this.dashboardService.getMetrics(tenantId)
    ]);

    client.emit('dashboard:summary', { summary });
    client.emit('dashboard:apps', { applications });
    client.emit('dashboard:services', { services });
    client.emit('dashboard:builds', { builds });
    client.emit('dashboard:events', { events });
    client.emit('dashboard:metrics', metrics);
  }

  // Método para broadcast quando eventos ocorrem
  async broadcastToTenant(tenantId: number, event: string, data: any) {
    this.server.to(`tenant:${tenantId}`).emit(event, data);
  }
}
```

---

## 7. SEGURANÇA E PERFORMANCE

### 7.1 Medidas de Segurança

```typescript
// Middleware de Rate Limiting para WebSocket
@Injectable()
export class WsRateLimitGuard implements CanActivate {
  private readonly rateLimiter = new Map<string, number[]>();
  
  canActivate(context: ExecutionContext): boolean {
    const client = context.switchToWs().getClient();
    const userId = client.user?.id;
    
    if (!userId) return false;
    
    const now = Date.now();
    const windowStart = now - 60000; // 1 minuto
    
    const requests = this.rateLimiter.get(userId) || [];
    const recentRequests = requests.filter(time => time > windowStart);
    
    if (recentRequests.length >= 100) { // Max 100 requests/minuto
      client.emit('error', { message: 'Rate limit exceeded' });
      return false;
    }
    
    recentRequests.push(now);
    this.rateLimiter.set(userId, recentRequests);
    
    return true;
  }
}
```

### 7.2 Otimizações de Performance

```typescript
// Estratégias de otimização:

// 1. Cache de dados frequentes
@Injectable()
export class DashboardCacheService {
  constructor(@Inject(CACHE_MANAGER) private cacheManager: Cache) {}

  async getCachedSummary(tenantId: number) {
    const cacheKey = `dashboard:summary:${tenantId}`;
    let data = await this.cacheManager.get(cacheKey);
    
    if (!data) {
      data = await this.calculateSummary(tenantId);
      await this.cacheManager.set(cacheKey, data, 3000); // 3 segundos
    }
    
    return data;
  }
}

// 2. Agregação de métricas (não enviar dados brutos)
async getMetrics(tenantId: number) {
  // Agregar dados a cada 10 segundos
  const rawMetrics = await this.metricsRepo.getLastMinute(tenantId);
  
  return {
    server: this.aggregateServerMetrics(rawMetrics),
    mariadb: this.aggregateDatabaseMetrics(rawMetrics),
    network: this.aggregateNetworkMetrics(rawMetrics),
    history: this.getHistoryPoints(rawMetrics, 10) // Últimos 10 pontos
  };
}

// 3. Seleção de campos (não enviar tudo)
async getApplications(tenantId: number) {
  return this.appRepo.find({
    where: { tenantId },
    select: ['id', 'name', 'status', 'version', 'lastDeploy'], // Apenas necessários
    relations: ['lastBuild'],
    take: 50 // Limite de 50 apps
  });
}
```

---

## 8. PRÓXIMOS PASSOS

1. **Implementar Backend**: Criar WebSocket Gateway e Services
2. **Criar Componentes React**: Cards, tabelas, gráficos
3. **Testes de Carga**: Verificar performance com 100+ conexões simultâneas
4. **Alertas**: Configurar notificações push/email para eventos críticos

---

**Documento criado em:** Fevereiro 2026  
**Versão:** 1.0.0  
**Tecnologia:** WebSocket (Socket.io) + React + NestJS
