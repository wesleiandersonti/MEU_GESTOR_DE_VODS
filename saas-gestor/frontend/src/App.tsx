import { Link } from 'react-router-dom'

function App() {
  return (
    <main className="min-h-screen bg-gray-100 text-gray-900 flex items-center justify-center p-6">
      <div className="w-full max-w-2xl bg-white rounded-xl shadow p-8 space-y-4">
        <h1 className="text-3xl font-bold">SaaS Gestor</h1>
        <p className="text-gray-600">Frontend online com sucesso.</p>
        <ul className="list-disc pl-6 text-sm space-y-1">
          <li>API Health: <a className="text-blue-600" href="http://localhost:3000/api/v1/health">http://localhost:3000/api/v1/health</a></li>
          <li>API Docs: <a className="text-blue-600" href="http://localhost:3000/api/docs">http://localhost:3000/api/docs</a></li>
        </ul>
        <Link to="/" className="inline-block px-4 py-2 rounded bg-blue-600 text-white">
          Atualizar
        </Link>
      </div>
    </main>
  )
}

export default App
