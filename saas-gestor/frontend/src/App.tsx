function App() {
  return (
    <main className="min-h-screen bg-gray-100 text-gray-900 flex items-center justify-center p-6">
      <div className="w-full max-w-2xl bg-white rounded-xl shadow p-8 space-y-4">
        <h1 className="text-3xl font-bold">SaaS Gestor</h1>
        <p className="text-gray-600">Frontend online com sucesso.</p>
        <ul className="list-disc pl-6 text-sm space-y-1">
          <li>API Health: <a className="text-blue-600" href="/api/v1/health">/api/v1/health</a></li>
          <li>API Docs: <a className="text-blue-600" href="/api/docs">/api/docs</a></li>
        </ul>
        <button
          className="inline-block px-4 py-2 rounded bg-blue-600 text-white"
          onClick={() => window.location.reload()}
          type="button"
        >
          Atualizar
        </button>
      </div>
    </main>
  )
}

export default App
