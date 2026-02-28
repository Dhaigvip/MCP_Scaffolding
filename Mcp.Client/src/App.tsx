import './App.css'
import { useState, useEffect } from 'react'
import { AgentChat } from './components/AgentChat'
import type { Protocol } from './components/AgentChat/types'

function App() {
  const [protocol, setProtocol] = useState<Protocol>('ws')

  useEffect(() => {
    fetch('/config.json')
      .then((r) => r.json())
      .then((cfg: { protocol?: string }) => {
        if (cfg.protocol === 'ws' || cfg.protocol === 'sse') {
          setProtocol(cfg.protocol)
        }
      })
      .catch(() => { /* keep default 'ws' */ })
  }, [])

  return (
    <div className="app-container">
      <header className="app-header">
        <span className="app-title">🤖 Palma AI Agent</span>
      </header>

      <main className="app-main">
        {/*
          apiBase is empty — Vite proxy forwards /api/* → http://localhost:5104
          In production, set to your deployed API URL or leave empty (same origin).
          Protocol ('ws' | 'sse') is read from public/config.json at startup.
        */}
        <AgentChat apiBase="" defaultProtocol={protocol} />
      </main>
    </div>
  )
}

export default App
