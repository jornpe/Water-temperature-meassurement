import { useEffect, useState } from 'react'
import { getTemperatures, type Temperature } from './api'

export default function App() {
  const [data, setData] = useState<Temperature[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getTemperatures()
      .then((d) => setData(d))
      .catch((e) => setError(String(e)))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <p>Loading…</p>
  if (error) return <p style={{ color: 'crimson' }}>Error: {error}</p>

  return (
    <main style={{ fontFamily: 'system-ui, sans-serif', padding: 24 }}>
      <h1>Water Temperatures</h1>
      <ul>
        {data.map((t) => (
          <li key={t.id}>
            <strong>{t.sensor}</strong>: {t.celsius.toFixed(2)} °C
            <small style={{ marginLeft: 8, color: '#555' }}>
              at {new Date(t.timestamp).toLocaleString()}
            </small>
          </li>
        ))}
      </ul>
    </main>
  )
}
