# 📋 VERSIONAMENTO SEMÂNTICO (SemVer)

Guia de versionamento para o projeto MEU GESTOR DE VODS.

---

## 🎯 O que é Semantic Versioning?

Formato: **MAJOR.MINOR.PATCH** (ex: 1.0.10)

```
1.0.10
│ │ │
│ │ └─ PATCH: Correções de bugs
│ └── MINOR: Novas funcionalidades
└── MAJOR: Mudanças incompatíveis
```

---

## 📊 Regras de Incremento

### 🔴 PATCH (Último número)
**Quando incrementar:** Correções de bugs, ajustes, melhorias internas

**Exemplos:**
- 1.0.0 → 1.0.1 (correção de bug)
- 1.0.1 → 1.0.2 (ajuste de build)
- 1.0.9 → 1.0.10 ✅ (não 1.1.0)
- 1.0.99 → 1.0.100 ✅ (pode passar de 99!)

**O que inclui:**
- Correções de erros
- Ajustes de performance
- Melhorias na documentação
- Correções de build
- Refatorações internas

---

### 🟡 MINOR (Número do meio)
**Quando incrementar:** Novas funcionalidades compatíveis com versões anteriores

**Exemplos:**
- 1.0.10 → 1.1.0 (nova funcionalidade)
- 1.1.5 → 1.2.0 (novo recurso)
- 1.9.0 → 1.10.0 ✅ (pode passar de 9!)

**O que inclui:**
- Novas funcionalidades
- Novos endpoints/botões
- Novas telas
- Melhorias significativas na UI
- Novos comandos

**Reset:** PATCH volta para 0
- 1.0.15 → 1.1.0 (não 1.1.15)

---

### 🟢 MAJOR (Primeiro número)
**Quando incrementar:** Mudanças incompatíveis (breaking changes)

**Exemplos:**
- 1.15.30 → 2.0.0 (mudança drástica)
- 2.5.1 → 3.0.0 (API diferente)

**O que inclui:**
- Mudanças que quebram compatibilidade
- Remoção de funcionalidades antigas
- Reescrita completa
- Migração obrigatória de dados
- Mudança na arquitetura

**Reset:** MINOR e PATCH voltam para 0
- 1.9.99 → 2.0.0

---

## 📝 Convenções do Projeto

### Sobre números maiores que 99

**Mito:** "Tem que ir de 1.0.99 para 1.1.0"

**Realidade:** Não existe limite! Pode ter:
- 1.0.150 ✅
- 1.0.999 ✅
- 1.0.10000 ✅

**Quando mudar MINOR:**
- Só quando adicionar **nova funcionalidade**, não por causa do número!

---

## 🔄 Exemplo Prático do Nosso Projeto

```
Versão Inicial: 1.0.0

1.0.0 → 1.0.1 (correção de bug no download)
1.0.1 → 1.0.2 (ajuste na UI)
1.0.2 → 1.0.5 (várias correções pequenas)
1.0.5 → 1.0.10 (correção do build)
1.0.10 → 1.0.15 (mais correções)
...
1.0.150 → 1.1.0 (⭐ NOVA FUNCIONALIDADE: Sincronização nuvem)
1.1.0 → 1.1.1 (correção na nuvem)
1.1.1 → 1.2.0 (⭐ NOVO: Sistema de favoritos melhorado)
1.2.0 → 1.2.1 (correção)
...
1.9.50 → 1.10.0 (⭐ NOVO: Interface renovada)
...
1.99.99 → 2.0.0 (⭐ MUDANÇA GRANDE: Migração para SaaS)
```

---

## 🚀 Fluxo de Trabalho

### ⚠️ IMPORTANTE: Sempre mude a versão ao fazer build!

**Regra de Ouro:** Sempre que fizer alterações e quiser que o app detecte atualização:

```bash
# 1. Faça suas alterações
# 2. Commite
# 3. CRIE NOVA TAG (não reutilize tag antiga!)
git tag -a v1.0.X -m "Descrição"
git push origin v1.0.X
```

**❌ NUNCA reutilize a mesma tag:**
```bash
# ERRADO - App não detectará atualização:
git tag -d v1.0.10
git tag -a v1.0.10 -m "Mesma versão"
```

**✅ SEMPRE crie versão nova:**
```bash
# CERTO - App detectará atualização:
git tag -a v1.0.11 -m "Nova versão"
```

### 1. Desenvolvimento
```bash
# Durante desenvolvimento, sem tag
# Commits normais
```

### 2. Correção de Bug
```bash
# Corrigir o código
# Commit: "fix: corrige erro XYZ"
git add .
git commit -m "fix: corrige erro no download"
git push origin main

# Criar tag PATCH
git tag -a v1.0.11 -m "Versão 1.0.11 - Correção no download"
git push origin v1.0.11
```

### 3. Nova Funcionalidade
```bash
# Desenvolver feature
# Commit: "feat: adiciona sistema X"
git add .
git commit -m "feat: adiciona sincronização nuvem"
git push origin main

# Criar tag MINOR (zera PATCH)
git tag -a v1.1.0 -m "Versão 1.1.0 - Sincronização nuvem"
git push origin v1.1.0
```

### 4. Mudança Grande
```bash
# Desenvolver migração
# Commit: "feat!: migra para nova arquitetura"
git add .
git commit -m "feat!: migração completa para SaaS"
git push origin main

# Criar tag MAJOR (zera MINOR e PATCH)
git tag -a v2.0.0 -m "Versão 2.0.0 - Plataforma SaaS"
git push origin v2.0.0
```

---

## 📋 Checklist antes de criar tag

- [ ] Código compilando localmente
- [ ] Testes realizados (se houver)
- [ ] CHANGELOG.md atualizado
- [ ] README.md atualizado (se necessário)
- [ ] Documentação atualizada
- [ ] Build no GitHub Actions passando

---

## 🎯 Resumo Rápido

| Situação | Versão Anterior | Versão Nova | Exemplo |
|----------|----------------|-------------|---------|
| Correção de bug | 1.0.5 | 1.0.6 | `git tag v1.0.6` |
| Nova funcionalidade | 1.0.99 | 1.1.0 | `git tag v1.1.0` |
| Mudança drástica | 1.15.30 | 2.0.0 | `git tag v2.0.0` |
| Correção após minor | 1.1.0 | 1.1.1 | `git tag v1.1.1` |

---

## ❌ Erros Comuns

### ❌ NÃO FAÇA:
```
1.0.9 → 1.1.0 (só porque chegou em 9)
1.0.99 → 2.0.0 (pulou muito!)
1.0.5 → 1.5.0 (pulou MINOR)
1.2.3 → 1.2.4.5 (muitos números!)
```

### ✅ FAÇA:
```
1.0.9 → 1.0.10 (correção)
1.0.99 → 1.0.100 (correção)
1.0.150 → 1.1.0 (nova feature)
1.9.99 → 1.10.0 (nova feature)
```

---

## 📚 Referências

- **SemVer Oficial:** https://semver.org/lang/pt-BR/
- **Conventional Commits:** https://www.conventionalcommits.org/pt-br/v1.0.0/

---

**Nota:** Este projeto segue Semantic Versioning 2.0.0 desde a versão 1.0.0
