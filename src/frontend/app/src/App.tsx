import { useEffect, useState } from 'react'
import { AppBar, Toolbar, Typography, Button, Stack, Paper } from '@mui/material'
import { getTemperatures, type Temperature, usersExist, logout, authHeader } from './api'
import { useNavigate } from 'react-router-dom'

export default function App() {
  const [data, setData] = useState<Temperature[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const navigate = useNavigate()

  useEffect(() => {
    // Redirect to register if no users and no token
    usersExist().then((exists) => {
      const hasToken = !!authHeader().Authorization
      if (!exists && !hasToken) {
        navigate('/register', { replace: true })
      }
    })

    getTemperatures().then(setData).catch((e) => setError(String(e))).finally(() => setLoading(false))
  }, [])

  if (loading) return <p>Loading…</p>
  if (error) return <p style={{ color: 'crimson' }}>Error: {error}</p>

  return (
    <Stack spacing={3}>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>Water Temperatures</Typography>
          <Button color="inherit" href="/login">Login</Button>
          <Button color="inherit" href="/register">Register</Button>
          <Button color="inherit" onClick={() => { logout(); navigate('/login') }}>Logout</Button>
        </Toolbar>
      </AppBar>
      <Paper sx={{ p: 3 }}>
        <Typography variant="h5" gutterBottom>Latest readings</Typography>
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
      </Paper>
    </Stack>
  )
}
