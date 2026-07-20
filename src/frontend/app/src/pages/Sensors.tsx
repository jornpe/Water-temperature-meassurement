import { useEffect, useRef, useState } from 'react'
import {
  getDevices,
  type DeviceSummary,
} from '../api'
import SensorsContent from '../components/SensorsContent'
import { Box, CircularProgress, Alert } from '@mui/material'
import { useNavigate } from 'react-router-dom'

const POLL_INTERVAL_MS = 10000
const HIDDEN_POLL_INTERVAL_MS = 30000

export default function Sensors() {
  const navigate = useNavigate()
  const [devices, setDevices] = useState<DeviceSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const inFlightRef = useRef(false)

  useEffect(() => {
    let cancelled = false
    let timeoutId: number | undefined

    const getDelay = () => (document.visibilityState === 'visible' ? POLL_INTERVAL_MS : HIDDEN_POLL_INTERVAL_MS)

    const schedule = (delay: number) => {
      timeoutId = window.setTimeout(() => {
        void loadDevices(false)
      }, delay)
    }

    const loadDevices = async (isInitialLoad: boolean) => {
      if (inFlightRef.current || cancelled) {
        return
      }

      inFlightRef.current = true

      if (isInitialLoad) {
        setLoading(true)
      }

      try {
        const summaries = await getDevices()

        if (cancelled) {
          return
        }

        setDevices(summaries)
        setError(null)
      } catch (err: any) {
        if (!cancelled) {
          setError(err.message || 'Failed to load devices')
        }
      } finally {
        if (!cancelled) {
          if (isInitialLoad) {
            setLoading(false)
          }

          inFlightRef.current = false
          schedule(getDelay())
        }
      }
    }

    const handleVisibilityChange = () => {
      if (timeoutId !== undefined) {
        window.clearTimeout(timeoutId)
      }

      schedule(document.visibilityState === 'visible' ? 0 : HIDDEN_POLL_INTERVAL_MS)
    }

    void loadDevices(true)
    document.addEventListener('visibilitychange', handleVisibilityChange)

    return () => {
      cancelled = true
      document.removeEventListener('visibilitychange', handleVisibilityChange)

      if (timeoutId !== undefined) {
        window.clearTimeout(timeoutId)
      }
    }
  }, [])

  const handleSelectDevice = (device: DeviceSummary) => {
    navigate(`/devices/${device.id}`)
  }

  if (loading) {
    return (
      <Box 
        sx={{ 
          display: 'flex', 
          justifyContent: 'center', 
          alignItems: 'center', 
          minHeight: '400px' 
        }}
      >
        <CircularProgress />
      </Box>
    )
  }

  if (error) {
    return (
      <Box sx={{ p: 2 }}>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
        <SensorsContent devices={[]} onSelectDevice={() => undefined} />
      </Box>
    )
  }

  return (
    <SensorsContent devices={devices} onSelectDevice={handleSelectDevice} />
  )
}
