import { useEffect, useState } from 'react'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  FormControlLabel,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material'
import {
  getHomeAssistantSettings,
  updateHomeAssistantSettings,
  type UpdateHomeAssistantSettingsData,
} from '../api'

export default function Settings() {
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [pushDataToHomeAssistant, setPushDataToHomeAssistant] = useState(false)
  const [ipAddress, setIpAddress] = useState('')
  const [port, setPort] = useState('1883')
  const [user, setUser] = useState('')
  const [password, setPassword] = useState('')

  useEffect(() => {
    const loadSettings = async () => {
      try {
        const settings = await getHomeAssistantSettings()
        setPushDataToHomeAssistant(settings.pushDataToHomeAssistant)
        setIpAddress(settings.ipAddress || '')
        setPort(String(settings.port || 1883))
        setUser(settings.user || '')
        setPassword(settings.password || '')
        setError(null)
      } catch (err: any) {
        setError(err.message || 'Failed to load settings')
      } finally {
        setLoading(false)
      }
    }

    void loadSettings()
  }, [])

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault()
    setError(null)
    setSuccess(null)

    if (pushDataToHomeAssistant && !ipAddress.trim()) {
      setError('IP address is required when Home Assistant push is enabled')
      return
    }

    const parsedPort = Number(port)
    if (pushDataToHomeAssistant && (!Number.isInteger(parsedPort) || parsedPort <= 0 || parsedPort > 65535)) {
      setError('Port must be between 1 and 65535 when Home Assistant push is enabled')
      return
    }

    setSaving(true)

    try {
      const payload: UpdateHomeAssistantSettingsData = {
        pushDataToHomeAssistant,
        ipAddress: ipAddress.trim() || null,
        port: Number.isInteger(parsedPort) && parsedPort > 0 ? parsedPort : 1883,
        user: user.trim() || null,
        password,
      }

      const updatedSettings = await updateHomeAssistantSettings(payload)
      setPushDataToHomeAssistant(updatedSettings.pushDataToHomeAssistant)
      setIpAddress(updatedSettings.ipAddress || '')
      setPort(String(updatedSettings.port || 1883))
      setUser(updatedSettings.user || '')
      setPassword(updatedSettings.password || '')
      setSuccess('Settings updated successfully.')
    } catch (err: any) {
      setError(err.message || 'Failed to update settings')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Stack spacing={3}>
      <Stack spacing={1}>
        <Typography variant="h4">Settings</Typography>
        <Typography variant="body2" color="text.secondary">
          General application settings. More sections can be added here later.
        </Typography>
      </Stack>

      {error && <Alert severity="error">{error}</Alert>}
      {success && <Alert severity="success">{success}</Alert>}

      <Card>
        <CardContent>
          <Stack spacing={2}>
            <Typography variant="h6">Home Assistant integration</Typography>

            {loading ? (
              <Typography variant="body2" color="text.secondary">
                Loading settings...
              </Typography>
            ) : (
              <Box component="form" onSubmit={handleSubmit}>
                <Stack spacing={2}>
                  <FormControlLabel
                    control={<Switch checked={pushDataToHomeAssistant} onChange={(_, checked) => setPushDataToHomeAssistant(checked)} />}
                    label="Push data to Home Assistant"
                  />
                  <TextField
                    label="IP address"
                    value={ipAddress}
                    onChange={(event) => setIpAddress(event.target.value)}
                    required={pushDataToHomeAssistant}
                    fullWidth
                  />
                  <TextField
                    label="Port"
                    value={port}
                    onChange={(event) => setPort(event.target.value)}
                    type="number"
                    inputProps={{ min: 1, max: 65535 }}
                    required={pushDataToHomeAssistant}
                    fullWidth
                  />
                  <TextField label="User" value={user} onChange={(event) => setUser(event.target.value)} fullWidth />
                  <TextField
                    label="Password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    fullWidth
                  />
                  <Button type="submit" variant="contained" disabled={saving} sx={{ alignSelf: 'flex-start' }}>
                    {saving ? 'Saving...' : 'Save settings'}
                  </Button>
                </Stack>
              </Box>
            )}
          </Stack>
        </CardContent>
      </Card>
    </Stack>
  )
}