import { useEffect, useState } from 'react'
import { Button, Card, CardContent, CardHeader, Stack, TextField, Typography, Alert } from '@mui/material'
import { registerAdmin, usersExist } from '../api'
import { useNavigate } from 'react-router-dom'

export default function Register() {
  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const navigate = useNavigate()

  useEffect(() => {
    usersExist().then((exists) => {
      if (exists) navigate('/login', { replace: true })
    })
  }, [navigate])

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    try {
      await registerAdmin(userName, password)
      navigate('/login', { replace: true })
    } catch (e: any) {
      setError(e.message || 'Failed to register')
    }
  }

  return (
    <Stack alignItems="center" sx={{ mt: 8 }}>
      <Card sx={{ width: 420 }}>
        <CardHeader title="Create admin user" subheader="First-time setup" />
        <CardContent>
          <Stack component="form" spacing={2} onSubmit={submit}>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField label="Username" value={userName} onChange={(e) => setUserName(e.target.value)} required fullWidth />
            <TextField label="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required fullWidth />
            <Button variant="contained" type="submit">Register</Button>
          </Stack>
        </CardContent>
      </Card>
      <Typography variant="body2" sx={{ mt: 2 }}>
        Already have an account? <a href="/login">Log in</a>
      </Typography>
    </Stack>
  )
}
