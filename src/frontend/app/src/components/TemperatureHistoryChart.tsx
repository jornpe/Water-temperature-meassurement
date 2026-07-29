import { useEffect, useMemo, useState } from 'react'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Stack,
  Typography,
} from '@mui/material'
import { Refresh } from '@mui/icons-material'
import { LineChart } from '@mui/x-charts/LineChart'
import {
  getDeviceTemperatureHistory,
  type DeviceTemperatureHistoryResponse,
} from '../api'
import TelemetryDateRangeSelector, {
  resolveTelemetryDateRange,
  type TelemetryDateRange,
} from './TelemetryDateRangeSelector'

interface TemperatureHistoryChartProps {
  deviceId: number
}

const temperatureColor = '#00838f'

export default function TemperatureHistoryChart({ deviceId }: TemperatureHistoryChartProps) {
  const [range, setRange] = useState<TelemetryDateRange>({ preset: '24h' })
  const [history, setHistory] = useState<DeviceTemperatureHistoryResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [refreshVersion, setRefreshVersion] = useState(0)

  useEffect(() => {
    let cancelled = false
    const loadHistory = async () => {
      setLoading(true)
      try {
        const selectedRange = resolveTelemetryDateRange(range)
        const response = await getDeviceTemperatureHistory(deviceId, {
          ...selectedRange,
          maxPoints: 1_500,
        })
        if (!cancelled) {
          setHistory(response)
          setError(null)
        }
      } catch (loadError: any) {
        if (!cancelled) {
          setError(loadError.message || 'Failed to load temperature history')
        }
      } finally {
        if (!cancelled) {
          setLoading(false)
        }
      }
    }

    void loadHistory()
    return () => {
      cancelled = true
    }
  }, [deviceId, range, refreshVersion])

  const chartData = useMemo(() => {
    const items = history?.items ?? []
    return {
      timestamps: items.map((item) => new Date(item.recordedAtUtc)),
      temperatures: items.map((item) => item.temperatureCelsius),
    }
  }, [history?.items])

  return (
    <Card variant="outlined" sx={{ height: '100%' }}>
      <CardContent sx={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
        <Stack
          direction={{ xs: 'column', md: 'row' }}
          spacing={2}
          justifyContent="space-between"
          alignItems={{ xs: 'stretch', md: 'flex-start' }}
          sx={{ mb: 2 }}
        >
          <Stack spacing={0.5}>
            <Typography variant="h6">Temperature history</Typography>
            <Typography variant="body2" color="text.secondary">
              Water-temperature readings across the selected period.
            </Typography>
          </Stack>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} alignItems="flex-start">
            <TelemetryDateRangeSelector
              value={range}
              onChange={setRange}
              disabled={loading}
              compact
            />
            <Button
              variant="outlined"
              size="small"
              startIcon={<Refresh />}
              onClick={() => setRefreshVersion((value) => value + 1)}
              disabled={loading}
              sx={{ width: { xs: '100%', sm: 'auto' } }}
            >
              Refresh
            </Button>
          </Stack>
        </Stack>

        {error && <Alert severity="warning" sx={{ mb: 2 }}>{error}</Alert>}
        {history?.isSampled && (
          <Alert severity="info" sx={{ mb: 2 }}>
            The chart is simplified from {history.totalCount.toLocaleString()} stored temperature readings.
          </Alert>
        )}

        {loading && !history ? (
          <Box sx={{ minHeight: 340, display: 'grid', placeItems: 'center' }}>
            <CircularProgress />
          </Box>
        ) : chartData.timestamps.length > 0 ? (
          <>
            <Box sx={{ width: '100%', minHeight: 340, flexGrow: 1 }}>
              <LineChart
                height={340}
                hideLegend
                xAxis={[
                  {
                    id: 'time',
                    data: chartData.timestamps,
                    scaleType: 'time',
                    valueFormatter: (value: Date) => new Date(value).toLocaleString(),
                    label: 'Time',
                  },
                ]}
                yAxis={[
                  {
                    id: 'temperature',
                    position: 'left',
                    width: 62,
                    label: 'Temperature (°C)',
                    valueFormatter: (value: number) => `${value.toFixed(1)}°C`,
                  },
                ]}
                series={[
                  {
                    id: 'temperature',
                    label: 'Water temperature',
                    data: chartData.temperatures,
                    yAxisId: 'temperature',
                    color: temperatureColor,
                    showMark: false,
                    valueFormatter: (value) => value == null ? 'Not available' : `${value.toFixed(2)}°C`,
                  },
                ]}
                grid={{ horizontal: true }}
                axisHighlight={{ x: 'line' }}
                margin={{ left: 8, right: 8, top: 16, bottom: 8 }}
              />
            </Box>
            <Stack direction="row" spacing={0.75} justifyContent="center" alignItems="center" sx={{ pt: 1 }}>
              <Box
                aria-hidden
                sx={{ width: 18, height: 4, borderRadius: 2, bgcolor: temperatureColor }}
              />
              <Typography variant="caption">Water temperature (°C)</Typography>
            </Stack>
          </>
        ) : (
          <Box
            sx={{
              minHeight: 280,
              display: 'grid',
              placeItems: 'center',
              textAlign: 'center',
              border: 1,
              borderStyle: 'dashed',
              borderColor: 'divider',
              borderRadius: 2,
              bgcolor: 'action.hover',
              px: 3,
            }}
          >
            <Stack spacing={0.5}>
              <Typography variant="h6">No temperature history in this range</Typography>
              <Typography variant="body2" color="text.secondary">
                Choose another date range or wait for the device to report a reading.
              </Typography>
            </Stack>
          </Box>
        )}
      </CardContent>
    </Card>
  )
}
