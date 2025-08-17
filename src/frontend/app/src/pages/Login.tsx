import { useEffect, useState } from 'react'
import { Button, Card, CardContent, CardHeader, Stack, TextField, Typography, Alert } from '@mui/material'
import { login, usersExist } from '../api'
import { useNavigate } from 'react-router-dom'

export default function Login() {
  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const navigate = useNavigate()

  useEffect(() => {
    usersExist().then((exists) => {
      if (!exists) navigate('/register', { replace: true })
    })
  }, [navigate])

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    try {
      await login(userName, password)
      navigate('/', { replace: true })
    } catch (e: any) {
      setError('Invalid credentials')
    }
  }

  return (
    <Stack alignItems="center" sx={{ mt: 8 }}>
      <Card sx={{ width: 420 }}>
        <CardHeader title="Welcome back" subheader="Sign in to continue" />
        <CardContent>
          <Stack component="form" spacing={2} onSubmit={submit}>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField label="Username" value={userName} onChange={(e) => setUserName(e.target.value)} required fullWidth />
            <TextField label="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required fullWidth />
            <Button variant="contained" type="submit">Login</Button>
          </Stack>
        </CardContent>
      </Card>
      <Typography variant="body2" sx={{ mt: 2 }}>
        First time here? <a href="/register">Create admin</a>
      </Typography>
    </Stack>
  )
}
