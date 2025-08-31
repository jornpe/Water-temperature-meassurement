import { useEffect, useState } from 'react'
import { getTemperatures, type Temperature } from '../api'
import SensorsContent from '../components/SensorsContent'
import { Box, CircularProgress, Alert } from '@mui/material'

export default function Sensors() {
  const [data, setData] = useState<Temperature[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    // Load temperature data
    getTemperatures()
      .then(setData)
      .catch((err) => setError(err.message || 'Failed to load temperature data'))
      .finally(() => setLoading(false))
  }, [])

  // Convert Temperature data to match expected format
  const temperatures = data.map(temp => ({
    id: temp.id,
    value: temp.celsius,
    timestamp: temp.timestamp,
    sensor: temp.sensor || 'Default Sensor'
  }))

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
        <SensorsContent temperatures={[]} />
      </Box>
    )
  }

  return <SensorsContent temperatures={temperatures} />
}
