# 📚 REDIS CACHE - Guia de Uso

## ✅ Redis já está configurado!

O Redis está configurado automaticamente no projeto para:
- **Cache de dados** (sessões, queries frequentes)
- **Fila de jobs** (BullMQ para builds)
- **Pub/Sub** (WebSocket em tempo real)

---

## 🚀 COMO USAR O CACHE

### 1. Injetar o CacheManager

```typescript
import { Injectable, Inject } from '@nestjs/common';
import { CACHE_MANAGER } from '@nestjs/cache-manager';
import { Cache } from 'cache-manager';

@Injectable()
export class MeuServico {
  constructor(
    @Inject(CACHE_MANAGER) private cacheManager: Cache,
  ) {}
}
```

### 2. Salvar no Cache

```typescript
// Salvar por 1 hora (3600 segundos)
await this.cacheManager.set('chave', valor, 3600);

// Salvar objeto
await this.cacheManager.set(`user:${userId}`, user, 3600);

// Salvar sem TTL (usa padrão de 24 horas)
await this.cacheManager.set('config', config);
```

### 3. Ler do Cache

```typescript
// Tentar obter do cache
const cached = await this.cacheManager.get('chave');

if (cached) {
  console.log('✅ Dado veio do cache (rápido!)');
  return cached;
}

// Se não tem no cache, buscar no banco
console.log('🔄 Buscando no banco...');
const data = await this.database.getData();

// Salvar no cache para próxima vez
await this.cacheManager.set('chave', data, 3600);

return data;
```

### 4. Deletar do Cache

```typescript
// Deletar uma chave específica
await this.cacheManager.del('chave');

// Deletar com padrão (ex: todas do usuário)
await this.cacheManager.del(`user:${userId}`);
```

### 5. Resetar tudo (cuidado!)

```typescript
// Limpa TODO o cache
await this.cacheManager.reset();
```

---

## 💡 EXEMPLOS PRÁTICOS

### Exemplo 1: Cache de usuário

```typescript
async getUser(userId: number) {
  const cacheKey = `user:${userId}`;
  
  // Tentar pegar do cache
  const cached = await this.cacheManager.get(cacheKey);
  if (cached) return cached;
  
  // Buscar no banco
  const user = await this.usersRepository.findOne(userId);
  
  // Salvar no cache por 1 hora
  await this.cacheManager.set(cacheKey, user, 3600);
  
  return user;
}
```

### Exemplo 2: Cache de dashboard stats

```typescript
async getDashboardStats(tenantId: number) {
  const cacheKey = `stats:${tenantId}`;
  
  // Cache por 5 minutos (300 segundos)
  const cached = await this.cacheManager.get(cacheKey);
  if (cached) return cached;
  
  // Calcular stats (lento)
  const stats = {
    totalApps: await this.appsRepository.count({ tenantId }),
    totalBuilds: await this.buildsRepository.count({ tenantId }),
    activeUsers: await this.usersRepository.count({ tenantId, status: 'active' }),
  };
  
  await this.cacheManager.set(cacheKey, stats, 300);
  
  return stats;
}
```

### Exemplo 3: Invalidar cache quando dados mudam

```typescript
async updateUser(userId: number, data: UpdateUserDto) {
  // Atualizar no banco
  await this.usersRepository.update(userId, data);
  
  // Invalidar cache
  await this.cacheManager.del(`user:${userId}`);
  
  return { message: 'User updated' };
}
```

---

## 🔧 DECORATOR @Cacheable (Avançado)

Para métodos simples, você pode usar o decorator:

```typescript
import { CacheInterceptor, CacheTTL } from '@nestjs/cache-manager';
import { UseInterceptors } from '@nestjs/common';

@Controller('users')
export class UsersController {
  
  @Get(':id')
  @UseInterceptors(CacheInterceptor)
  @CacheTTL(300) // 5 minutos
  async findOne(@Param('id') id: string) {
    return this.usersService.findOne(id);
  }
}
```

---

## 📊 MELHORES PRÁTICAS

### ✅ FAÇA:
- Use cache para dados que não mudam muito
- Defina TTL apropriado (quanto mais volátil, menor o TTL)
- Use chaves descritivas: `user:123`, `tenant:456:stats`
- Sempre invalide cache quando dados mudam
- Use cache para queries pesadas (COUNT, JOINs)

### ❌ NÃO FAÇA:
- Não coloque dados sensíveis no cache (senhas, tokens)
- Não use cache para dados em tempo real (use WebSocket)
- Não cacheie tudo (só o que vale a pena)

---

## 🔍 MONITORAR O REDIS

### Acessar Redis CLI

```bash
# Conectar ao Redis
docker exec -it saas-redis redis-cli

# Ver todas as chaves
KEYS *

# Ver uma chave específica
GET "user:1"

# Ver informações
INFO

# Ver tamanho do cache
DBSIZE
```

### Comandos úteis

```bash
# Ver estatísticas
docker exec saas-redis redis-cli INFO stats

# Ver memória usada
docker exec saas-redis redis-cli INFO memory

# Limpar todo o cache (cuidado!)
docker exec saas-redis redis-cli FLUSHALL
```

---

## 🎯 CACHE VS BANCO

| Operação | Sem Cache | Com Cache | Melhoria |
|----------|-----------|-----------|----------|
| Buscar usuário | 50-100ms | 1-5ms | **90% mais rápido** |
| Dashboard stats | 500ms | 5ms | **99% mais rápido** |
| Query complexa | 1000ms | 10ms | **99% mais rápido** |

---

## 🚀 EXEMPLOS NO PROJETO

Já implementamos no `AuthService`:

```typescript
// ✅ Cache de usuário
const user = await this.getUserFromCache(userId);

// ✅ Cache de stats
const stats = await this.getTenantStats(tenantId);

// ✅ Limpar cache após alteração
await this.clearTenantCache(tenantId);
```

---

**Pronto! Agora seu sistema está super rápido com cache Redis!** ⚡
