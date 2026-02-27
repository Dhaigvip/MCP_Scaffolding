import './App.css'
import { AgentChat } from './components/AgentChat'

function App() {
  return (
    <div className="app-container">
      <header className="app-header">
        <span className="app-title">🤖 Palma AI Agent</span>
        <span className="app-subtitle">Powered by Claude · Swagger API tools</span>
      </header>

      <main className="app-main">
        {/*
          apiBase is empty — Vite proxy forwards /api/* → http://localhost:5104
          In production, set to your deployed API URL or leave empty (same origin).
        */}
        <AgentChat apiBase="" />
      </main>
    </div>
  )
}

export default App
