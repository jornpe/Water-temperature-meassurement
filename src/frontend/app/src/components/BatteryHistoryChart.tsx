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
  getDeviceBatteryHistory,
  type DeviceBatteryHistoryResponse,
} from '../api'
import TelemetryDateRangeSelector, {
  resolveTelemetryDateRange,
  type TelemetryDateRange,
} from './TelemetryDateRangeSelector'

interface BatteryHistoryChartProps {
  deviceId: number
}

const seriesColors = {
  percentage: '#2e7d32',
  modemVoltage: '#ed6c02',
  adcVoltage: '#1976d2',
}

const legendItems = [
  { label: 'Battery percentage', unit: '%', color: seriesColors.percentage },
  { label: 'Modem voltage', unit: 'V', color: seriesColors.modemVoltage },
  { label: 'ADC voltage', unit: 'V', color: seriesColors.adcVoltage },
]

export default function BatteryHistoryChart({ deviceId }: BatteryHistoryChartProps) {
  const [range, setRange] = useState<TelemetryDateRange>({ preset: '24h' })
  const [history, setHistory] = useState<DeviceBatteryHistoryResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [refreshVersion, setRefreshVersion] = useState(0)

  useEffect(() => {
    let cancelled = false
    const loadHistory = async () => {
      setLoading(true)
      try {
        const selectedRange = resolveTelemetryDateRange(range)
        const response = await getDeviceBatteryHistory(deviceId, {
          ...selectedRange,
          maxPoints: 1_500,
        })
        if (!cancelled) {
          setHistory(response)
          setError(null)
        }
      } catch (loadError: any) {
        if (!cancelled) {
          setError(loadError.message || 'Failed to load battery history')
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
      percentages: items.map((item) => Math.max(0, Math.min(100, item.percentage))),
      modemVoltages: items.map((item) => item.modemReadingValid ? item.modemMillivolts / 1_000 : null),
      adcVoltages: items.map((item) => Number.isFinite(item.adcVoltage) ? item.adcVoltage : null),
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
            <Typography variant="h6">Battery history</Typography>
            <Typography variant="body2" color="text.secondary">
              Percentage and voltage trends across the selected period.
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
            >
              Refresh
            </Button>
          </Stack>
        </Stack>

        {error && <Alert severity="warning" sx={{ mb: 2 }}>{error}</Alert>}
        {history?.isSampled && (
          <Alert severity="info" sx={{ mb: 2 }}>
            The chart is simplified from {history.totalCount.toLocaleString()} stored battery readings.
          </Alert>
        )}

        {loading && !history ? (
          <Box sx={{ minHeight: 360, display: 'grid', placeItems: 'center' }}>
            <CircularProgress />
          </Box>
        ) : chartData.timestamps.length > 0 ? (
          <>
            <Box sx={{ width: '100%', minHeight: 360, flexGrow: 1 }}>
              <LineChart
                height={360}
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
                    id: 'percentage',
                    position: 'left',
                    min: 0,
                    max: 100,
                    width: 58,
                    label: 'Battery (%)',
                    valueFormatter: (value: number) => `${Math.round(value)}%`,
                  },
                  {
                    id: 'voltage',
                    position: 'right',
                    min: 0,
                    max: 5,
                    width: 58,
                    label: 'Voltage (V)',
                    valueFormatter: (value: number) => `${value.toFixed(1)} V`,
                  },
                ]}
                series={[
                  {
                    id: 'percentage',
                    label: 'Battery percentage',
                    data: chartData.percentages,
                    yAxisId: 'percentage',
                    color: seriesColors.percentage,
                    showMark: false,
                    valueFormatter: (value) => value == null ? 'Not available' : `${Math.round(value)}%`,
                  },
                  {
                    id: 'modemVoltage',
                    label: 'Modem voltage',
                    data: chartData.modemVoltages,
                    yAxisId: 'voltage',
                    color: seriesColors.modemVoltage,
                    showMark: false,
                    valueFormatter: (value) => value == null ? 'Invalid reading' : `${value.toFixed(3)} V`,
                  },
                  {
                    id: 'adcVoltage',
                    label: 'ADC voltage',
                    data: chartData.adcVoltages,
                    yAxisId: 'voltage',
                    color: seriesColors.adcVoltage,
                    showMark: false,
                    valueFormatter: (value) => value == null ? 'Not available' : `${value.toFixed(3)} V`,
                  },
                ]}
                grid={{ horizontal: true }}
                axisHighlight={{ x: 'line' }}
                margin={{ left: 8, right: 8, top: 16, bottom: 8 }}
              />
            </Box>
            <Stack
              direction="row"
              flexWrap="wrap"
              gap={2}
              justifyContent="center"
              sx={{ pt: 1 }}
              aria-label="Battery chart legend"
            >
              {legendItems.map((item) => (
                <Stack key={item.label} direction="row" spacing={0.75} alignItems="center">
                  <Box
                    aria-hidden
                    sx={{
                      width: 18,
                      height: 4,
                      borderRadius: 2,
                      bgcolor: item.color,
                    }}
                  />
                  <Typography variant="caption">
                    {item.label} ({item.unit})
                  </Typography>
                </Stack>
              ))}
            </Stack>
          </>
        ) : (
          <Box
            sx={{
              minHeight: 300,
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
              <Typography variant="h6">No battery history in this range</Typography>
              <Typography variant="body2" color="text.secondary">
                Choose another date range or wait for the device to report a battery reading.
              </Typography>
            </Stack>
          </Box>
        )}
      </CardContent>
    </Card>
  )
}
