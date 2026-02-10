import { useEffect, useMemo, useState } from 'react'

type HealthResponse = {
  status: string
  timestamp: string
}

function App() {
  const [health, setHealth] = useState<HealthResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const apiBase = useMemo(() => `${window.location.origin}/api/v1`, [])

  useEffect(() => {
    let mounted = true

    const load = async () => {
      setLoading(true)
      setError(null)
      try {
        const response = await fetch('/api/v1/health', { cache: 'no-store' })
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`)
        }
        const data = (await response.json()) as HealthResponse
        if (mounted) {
          setHealth(data)
        }
      } catch (err) {
        if (mounted) {
          setError(err instanceof Error ? err.message : 'Unknown error')
        }
      } finally {
        if (mounted) {
          setLoading(false)
        }
      }
    }

    load()
    return () => {
      mounted = false
    }
  }, [])

  return (
    <main className="min-h-screen bg-slate-100 text-slate-900 p-6">
      <div className="mx-auto w-full max-w-5xl space-y-6">
        <header className="rounded-xl bg-slate-900 text-white p-6 shadow-lg">
          <h1 className="text-3xl font-bold">SaaS Gestor</h1>
          <p className="mt-1 text-slate-300">Painel inicial da plataforma rodando na sua rede local</p>
        </header>

        <section className="grid gap-4 md:grid-cols-3">
          <article className="rounded-xl bg-white p-5 shadow">
            <p className="text-xs uppercase tracking-wide text-slate-500">Frontend</p>
            <p className="mt-2 text-2xl font-semibold text-emerald-600">Online</p>
            <p className="mt-1 text-sm text-slate-500">Nginx + Vite build</p>
          </article>

          <article className="rounded-xl bg-white p-5 shadow">
            <p className="text-xs uppercase tracking-wide text-slate-500">API</p>
            <p className="mt-2 text-2xl font-semibold">
              {loading ? 'Verificando...' : error ? 'Falha' : health?.status ?? 'Indefinido'}
            </p>
            <p className="mt-1 text-sm text-slate-500">
              {error ? error : health?.timestamp ? `Atualizado em ${health.timestamp}` : 'Sem dados'}
            </p>
          </article>

          <article className="rounded-xl bg-white p-5 shadow">
            <p className="text-xs uppercase tracking-wide text-slate-500">Ambiente</p>
            <p className="mt-2 text-2xl font-semibold">Producao</p>
            <p className="mt-1 text-sm text-slate-500">Deploy via Docker Compose</p>
          </article>
        </section>

        <section className="rounded-xl bg-white p-6 shadow">
          <h2 className="text-xl font-semibold">Endpoints principais</h2>
          <div className="mt-4 grid gap-3 md:grid-cols-2">
            <a className="rounded-lg border border-slate-200 p-4 hover:bg-slate-50" href="/api/v1/health" target="_blank" rel="noreferrer">
              <p className="font-medium">Health Check</p>
              <p className="text-sm text-slate-500">{apiBase}/health</p>
            </a>
            <a className="rounded-lg border border-slate-200 p-4 hover:bg-slate-50" href="/api/docs" target="_blank" rel="noreferrer">
              <p className="font-medium">Swagger Docs</p>
              <p className="text-sm text-slate-500">{window.location.origin}/api/docs</p>
            </a>
          </div>

          <div className="mt-5 flex gap-3">
            <button
              type="button"
              onClick={() => window.location.reload()}
              className="rounded-md bg-blue-600 px-4 py-2 text-white hover:bg-blue-700"
            >
              Atualizar painel
            </button>
            <button
              type="button"
              onClick={() => window.open('/api/docs', '_blank', 'noopener,noreferrer')}
              className="rounded-md border border-slate-300 px-4 py-2 text-slate-700 hover:bg-slate-50"
            >
              Abrir documentacao
            </button>
          </div>
        </section>
      </div>
    </main>
  )
}

export default App
