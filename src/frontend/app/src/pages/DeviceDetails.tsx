import { useEffect, useRef, useState } from 'react'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControl,
  FormControlLabel,
  Grid,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Switch,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material'
import { AccessTime, ArrowBack, Thermostat } from '@mui/icons-material'
import { useNavigate, useParams } from 'react-router-dom'
import {
  clearDeviceTelemetry,
  deleteDevice,
  deleteDeviceLogs,
  getDevice,
  getDeviceLogs,
  regenerateDeviceKey,
  registerDevice,
  updateDevice,
  type DeviceDetail,
  type DeviceLogsResponse,
} from '../api'
import BatteryHistoryChart from '../components/BatteryHistoryChart'
import BatteryIndicator from '../components/BatteryIndicator'
import DeviceMap from '../components/DeviceMap'

type DeviceTab = 0 | 1 | 2
type PendingAction = 'regenerate' | 'clear-temperature' | 'clear-position' | 'delete-all-logs' | 'delete' | null

const POLL_INTERVAL_MS = 5000
const HIDDEN_POLL_INTERVAL_MS = 20000
const LOG_PAGE_SIZE = 100
const LOG_SEARCH_DEBOUNCE_MS = 350
const HOME_ASSISTANT_DEVICE_NAME_REGEX = /^[\p{L}\p{N} _\-().]+$/u

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleString() : 'Not available'
}

function formatValue(value?: string | number | boolean | null, suffix = '') {
  if (value === null || value === undefined || value === '') {
    return 'Not available'
  }

  if (typeof value === 'boolean') {
    return value ? 'Yes' : 'No'
  }

  return `${value}${suffix}`
}

function formatBatteryState(value?: number | null) {
  return value === 0 ? 'Not charging' : value === 1 ? 'Charging' : value === 2 ? 'Full' : 'Unknown'
}

function toUtcIso(value: string) {
  return value ? new Date(value).toISOString() : undefined
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <Stack spacing={0.5} sx={{ minWidth: 180 }}>
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="body2">{value}</Typography>
    </Stack>
  )
}

export default function DeviceDetails() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const deviceId = Number(id)

  const [device, setDevice] = useState<DeviceDetail | null>(null)
  const [logs, setLogs] = useState<DeviceLogsResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [pageError, setPageError] = useState<string | null>(null)
  const [logsError, setLogsError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)
  const [actionLoading, setActionLoading] = useState(false)
  const [tab, setTab] = useState<DeviceTab>(0)
  const [pendingAction, setPendingAction] = useState<PendingAction>(null)
  const [name, setName] = useState('')
  const [place, setPlace] = useState('')
  const [reportIntervalSeconds, setReportIntervalSeconds] = useState('30')
  const [pushToHomeAssistant, setPushToHomeAssistant] = useState(false)
  const [homeAssistantDeviceName, setHomeAssistantDeviceName] = useState('')
  const [homeAssistantDeviceNameError, setHomeAssistantDeviceNameError] = useState<string | null>(null)
  const [savedConfiguration, setSavedConfiguration] = useState<{
    name: string
    place: string
    reportIntervalSeconds: string
    pushToHomeAssistant: boolean
    homeAssistantDeviceName: string
  } | null>(null)
  const inFlightRef = useRef(false)
  const logsContainerRef = useRef<HTMLDivElement | null>(null)
  const [isFollowingLogs, setIsFollowingLogs] = useState(true)
  const formDirtyRef = useRef(false)
  const [logSeverityFilter, setLogSeverityFilter] = useState('all')
  const [logFromTime, setLogFromTime] = useState('')
  const [logToTime, setLogToTime] = useState('')
  const [logTextFilter, setLogTextFilter] = useState('')
  const [debouncedLogTextFilter, setDebouncedLogTextFilter] = useState('')
  const [logsLoading, setLogsLoading] = useState(false)
  const [olderLogsLoading, setOlderLogsLoading] = useState(false)
  const [logReloadVersion, setLogReloadVersion] = useState(0)
  const [retentionDialogOpen, setRetentionDialogOpen] = useState(false)
  const [retentionPresetHours, setRetentionPresetHours] = useState('24')
  const [customRetentionDays, setCustomRetentionDays] = useState('30')

  const logEntries = logs
    ? [...logs.items].sort((left, right) => {
        const timestampDifference = Date.parse(left.timestampUtc) - Date.parse(right.timestampUtc)
        return timestampDifference !== 0 ? timestampDifference : left.id - right.id
      })
    : []

  const availableLogSeverities = Array.from(
    new Set([
      'info',
      'warning',
      'error',
      ...logEntries.map((entry) => entry.level).filter((level): level is string => Boolean(level)),
    ]),
  ).sort()

  const filteredLogEntries = logEntries

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setDebouncedLogTextFilter(logTextFilter.trim())
    }, LOG_SEARCH_DEBOUNCE_MS)

    return () => window.clearTimeout(timeoutId)
  }, [logTextFilter])

  useEffect(() => {
    if (!device) {
      return
    }

    if (formDirtyRef.current) {
      // The user has unsaved edits in the form; a background poll refresh must not wipe them.
      return
    }

    const nextName = device.name || ''
    const nextPlace = device.place || ''
    const nextReportIntervalSeconds = String(device.reportIntervalSeconds || 30)
    const nextHomeAssistantDeviceName =
      device.homeAssistantDeviceName === (device.name || device.deviceId) ? '' : device.homeAssistantDeviceName || ''

    setName(nextName)
    setPlace(nextPlace)
    setReportIntervalSeconds(nextReportIntervalSeconds)
    setPushToHomeAssistant(device.pushToHomeAssistant)
    setHomeAssistantDeviceName(nextHomeAssistantDeviceName)
    setHomeAssistantDeviceNameError(null)
    setSavedConfiguration({
      name: nextName,
      place: nextPlace,
      reportIntervalSeconds: nextReportIntervalSeconds,
      pushToHomeAssistant: device.pushToHomeAssistant,
      homeAssistantDeviceName: nextHomeAssistantDeviceName,
    })
  }, [device])

  useEffect(() => {
    if (!Number.isInteger(deviceId) || deviceId <= 0) {
      setLoading(false)
      setPageError('Invalid device ID')
      return
    }

    let cancelled = false
    let timeoutId: number | undefined

    const getDelay = () => (document.visibilityState === 'visible' ? POLL_INTERVAL_MS : HIDDEN_POLL_INTERVAL_MS)

    const schedule = (delay: number) => {
      timeoutId = window.setTimeout(() => {
        void loadPage(false)
      }, delay)
    }

    const loadPage = async (isInitialLoad: boolean) => {
      if (inFlightRef.current || cancelled) {
        return
      }

      inFlightRef.current = true

      if (isInitialLoad) {
        setLoading(true)
      }

      try {
        const detailResult = await getDevice(deviceId)

        if (cancelled) {
          return
        }

        setDevice(detailResult)
        setPageError(null)
      } catch (err: any) {
        if (!cancelled) {
          setPageError(err.message || 'Failed to load device details')
        }
      } finally {
        inFlightRef.current = false

        if (!cancelled) {
          if (isInitialLoad) {
            setLoading(false)
          }

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

    void loadPage(true)
    document.addEventListener('visibilitychange', handleVisibilityChange)

    return () => {
      cancelled = true
      document.removeEventListener('visibilitychange', handleVisibilityChange)

      if (timeoutId !== undefined) {
        window.clearTimeout(timeoutId)
      }
    }
  }, [deviceId])

  useEffect(() => {
    if (!Number.isInteger(deviceId) || deviceId <= 0) {
      return
    }

    let cancelled = false
    let timeoutId: number | undefined
    let latestSeenId = 0
    let logsInitialized = false

    const query = {
      pageSize: LOG_PAGE_SIZE,
      search: debouncedLogTextFilter || undefined,
      level: logSeverityFilter === 'all' ? undefined : logSeverityFilter,
      fromUtc: toUtcIso(logFromTime),
      toUtc: toUtcIso(logToTime),
    }

    const getDelay = () => (document.visibilityState === 'visible' ? POLL_INTERVAL_MS : HIDDEN_POLL_INTERVAL_MS)

    const schedule = (delay: number) => {
      timeoutId = window.setTimeout(() => {
        if (logsInitialized) {
          void pollForNewLogs()
        } else {
          void loadInitialLogs()
        }
      }, delay)
    }

    const mergeNewLogs = (newItems: DeviceLogsResponse['items']) => {
      if (newItems.length === 0) {
        return
      }

      setLogs((current) => {
        if (!current) {
          return current
        }

        const entriesById = new Map(current.items.map((entry) => [entry.id, entry]))
        let addedCount = 0

        for (const entry of newItems) {
          if (!entriesById.has(entry.id)) {
            addedCount += 1
          }
          entriesById.set(entry.id, entry)
        }

        return {
          ...current,
          totalCount: current.totalCount == null ? current.totalCount : current.totalCount + addedCount,
          items: Array.from(entriesById.values()).sort((left, right) => right.id - left.id),
        }
      })
    }

    const pollForNewLogs = async () => {
      if (cancelled) {
        return
      }

      let caughtUp = true

      try {
        let hasMore = false
        let batchesProcessed = 0

        do {
          const response = await getDeviceLogs(deviceId, {
            ...query,
            afterId: latestSeenId,
            includeTotalCount: false,
          })

          if (cancelled) {
            return
          }

          if (response.items.length > 0) {
            latestSeenId = Math.max(latestSeenId, ...response.items.map((entry) => entry.id))
            mergeNewLogs(response.items)
          }

          hasMore = response.hasMore && response.items.length > 0
          batchesProcessed += 1
        } while (hasMore && batchesProcessed < 10)

        caughtUp = !hasMore
        setLogsError(null)
      } catch (err: any) {
        if (!cancelled) {
          setLogsError(err.message || 'Failed to load new device logs')
        }
      } finally {
        if (!cancelled) {
          schedule(caughtUp ? getDelay() : 0)
        }
      }
    }

    const loadInitialLogs = async () => {
      setLogsLoading(true)
      setLogs(null)

      try {
        const response = await getDeviceLogs(deviceId, {
          ...query,
          includeTotalCount: true,
        })

        if (cancelled) {
          return
        }

        latestSeenId = response.items.reduce((highest, entry) => Math.max(highest, entry.id), 0)
        logsInitialized = true
        setLogs(response)
        setLogsError(null)
      } catch (err: any) {
        if (!cancelled) {
          setLogsError(err.message || 'Failed to load device logs')
        }
      } finally {
        if (!cancelled) {
          setLogsLoading(false)
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

    void loadInitialLogs()
    document.addEventListener('visibilitychange', handleVisibilityChange)

    return () => {
      cancelled = true
      document.removeEventListener('visibilitychange', handleVisibilityChange)

      if (timeoutId !== undefined) {
        window.clearTimeout(timeoutId)
      }
    }
  }, [
    debouncedLogTextFilter,
    deviceId,
    logFromTime,
    logReloadVersion,
    logSeverityFilter,
    logToTime,
  ])

  useEffect(() => {
    if (tab !== 2 || !isFollowingLogs) {
      return
    }

    const container = logsContainerRef.current
    if (!container) {
      return
    }

    window.requestAnimationFrame(() => {
      container.scrollTop = container.scrollHeight
    })
  }, [isFollowingLogs, logEntries, tab])

  const refreshPage = async () => {
    const detail = await getDevice(deviceId)
    setDevice(detail)
    setPageError(null)
  }

  const handleLogsScroll = () => {
    const container = logsContainerRef.current
    if (!container) {
      return
    }

    const distanceFromBottom = container.scrollHeight - container.scrollTop - container.clientHeight
    setIsFollowingLogs(distanceFromBottom <= 24)
  }

  const jumpToLatestLogs = () => {
    const container = logsContainerRef.current
    if (!container) {
      return
    }

    container.scrollTop = container.scrollHeight
    setIsFollowingLogs(true)
  }

  const loadOlderLogs = async () => {
    if (!logs || !logs.hasMore || olderLogsLoading || logs.items.length === 0) {
      return
    }

    setOlderLogsLoading(true)
    setLogsError(null)

    try {
      const response = await getDeviceLogs(deviceId, {
        pageSize: LOG_PAGE_SIZE,
        search: debouncedLogTextFilter || undefined,
        level: logSeverityFilter === 'all' ? undefined : logSeverityFilter,
        fromUtc: toUtcIso(logFromTime),
        toUtc: toUtcIso(logToTime),
        beforeId: logs.nextBeforeId ?? Math.min(...logs.items.map((entry) => entry.id)),
        includeTotalCount: false,
      })

      setLogs((current) => {
        if (!current) {
          return response
        }

        const entriesById = new Map(current.items.map((entry) => [entry.id, entry]))
        for (const entry of response.items) {
          entriesById.set(entry.id, entry)
        }

        return {
          ...current,
          hasMore: response.hasMore,
          nextBeforeId: response.nextBeforeId,
          items: Array.from(entriesById.values()).sort((left, right) => right.id - left.id),
        }
      })
    } catch (err: any) {
      setLogsError(err.message || 'Failed to load older device logs')
    } finally {
      setOlderLogsLoading(false)
    }
  }

  const withAction = async (action: () => Promise<void>, success: string) => {
    setActionLoading(true)
    setActionError(null)
    setSuccessMessage(null)

    try {
      await action()
      formDirtyRef.current = false
      if (pendingAction !== 'delete') {
        await refreshPage()
      }
      setSuccessMessage(success)
    } catch (err: any) {
      setActionError(err.message || 'Action failed')
    } finally {
      setActionLoading(false)
      setPendingAction(null)
    }
  }

  const deleteLogsOlderThanRetention = async () => {
    const retentionHours =
      retentionPresetHours === 'custom'
        ? Number(customRetentionDays) * 24
        : Number(retentionPresetHours)

    if (!Number.isFinite(retentionHours) || retentionHours <= 0) {
      setActionError('The number of days to keep must be greater than zero.')
      return
    }

    setActionLoading(true)
    setActionError(null)
    setSuccessMessage(null)

    try {
      const cutoffUtc = new Date(Date.now() - retentionHours * 60 * 60 * 1000).toISOString()
      const response = await deleteDeviceLogs(deviceId, cutoffUtc)
      setRetentionDialogOpen(false)
      setLogReloadVersion((version) => version + 1)
      setSuccessMessage(`${response.deletedCount.toLocaleString()} old log entries deleted.`)
    } catch (err: any) {
      setActionError(err.message || 'Failed to delete old device logs')
    } finally {
      setActionLoading(false)
    }
  }

  const getEffectiveHomeAssistantDeviceName = (nextName = name, configuredValue = homeAssistantDeviceName) => {
    const normalizedName = nextName.trim()
    return (configuredValue?.trim() || normalizedName || device?.deviceId || 'Device').trim()
  }

  const validateHomeAssistantDeviceName = (value: string) => {
    const normalizedValue = value.trim()

    if (!normalizedValue) {
      return 'Home Assistant device name is required'
    }

    if (normalizedValue.length > 100) {
      return 'Home Assistant device name must be 100 characters or fewer'
    }

    if (!HOME_ASSISTANT_DEVICE_NAME_REGEX.test(normalizedValue)) {
      return 'Home Assistant device name may only contain letters, numbers, spaces, hyphens, underscores, periods, and parentheses'
    }

    return null
  }

  const getNormalizedHomeAssistantDeviceName = (value: string, nextName = name) => {
    const normalizedValue = value.trim()
    const normalizedName = nextName.trim()

    if (!normalizedValue || normalizedValue === normalizedName) {
      return null
    }

    return normalizedValue
  }

  const handleHomeAssistantDeviceNameBlur = () => {
    const validationError = validateHomeAssistantDeviceName(getEffectiveHomeAssistantDeviceName())
    setHomeAssistantDeviceNameError(validationError)
  }

  const isConfigurationDirty =
    savedConfiguration !== null &&
    (name !== savedConfiguration.name ||
      place !== savedConfiguration.place ||
      reportIntervalSeconds !== savedConfiguration.reportIntervalSeconds ||
      pushToHomeAssistant !== savedConfiguration.pushToHomeAssistant ||
      homeAssistantDeviceName !== savedConfiguration.homeAssistantDeviceName)

  const isRegistrationFormComplete =
    name.trim() !== '' &&
    place.trim() !== '' &&
    reportIntervalSeconds.trim() !== '' &&
    Number(reportIntervalSeconds) > 0

  const handleRegisterSubmit = async (event: React.FormEvent) => {
    event.preventDefault()

    const effectiveHomeAssistantDeviceName = getEffectiveHomeAssistantDeviceName()
    const validationError = validateHomeAssistantDeviceName(effectiveHomeAssistantDeviceName)
    if (validationError) {
      setHomeAssistantDeviceNameError(validationError)
      return
    }

    await withAction(async () => {
      await registerDevice(deviceId, {
        name: name.trim(),
        place: place.trim(),
        reportIntervalSeconds: Number(reportIntervalSeconds),
        pushToHomeAssistant,
        homeAssistantDeviceName: getNormalizedHomeAssistantDeviceName(homeAssistantDeviceName),
      })
    }, 'Device registered successfully.')
  }

  const handleSaveConfiguration = async (event: React.FormEvent) => {
    event.preventDefault()

    const effectiveHomeAssistantDeviceName = getEffectiveHomeAssistantDeviceName()
    const validationError = validateHomeAssistantDeviceName(effectiveHomeAssistantDeviceName)
    if (validationError) {
      setHomeAssistantDeviceNameError(validationError)
      return
    }

    await withAction(async () => {
      const updatedDevice = await updateDevice(deviceId, {
        name: name.trim(),
        place: place.trim(),
        reportIntervalSeconds: Number(reportIntervalSeconds),
        pushToHomeAssistant,
        homeAssistantDeviceName: getNormalizedHomeAssistantDeviceName(homeAssistantDeviceName),
      })
      setDevice(updatedDevice)
    }, 'Device configuration updated.')
  }

  const runPendingAction = async () => {
    if (pendingAction === 'regenerate') {
      await withAction(async () => {
        await regenerateDeviceKey(deviceId)
      }, 'API key regenerated. The device will receive the new key on discovery.')
      return
    }

    if (pendingAction === 'clear-temperature') {
      await withAction(async () => {
        await clearDeviceTelemetry(deviceId, 'temperature')
      }, 'Temperature history cleared.')
      return
    }

    if (pendingAction === 'clear-position') {
      await withAction(async () => {
        await clearDeviceTelemetry(deviceId, 'position')
      }, 'Position history cleared.')
      return
    }

    if (pendingAction === 'delete-all-logs') {
      await withAction(async () => {
        await deleteDeviceLogs(deviceId)
        setLogReloadVersion((version) => version + 1)
      }, 'All device logs deleted.')
      return
    }

    if (pendingAction === 'delete') {
      await withAction(async () => {
        await deleteDevice(deviceId)
        navigate('/')
      }, 'Device deleted.')
    }
  }

  if (loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: 320 }}>
        <CircularProgress />
      </Box>
    )
  }

  if (pageError && !device) {
    return (
      <Stack spacing={2}>
        <Button startIcon={<ArrowBack />} onClick={() => navigate('/')} sx={{ alignSelf: 'flex-start' }}>
          Back to devices
        </Button>
        <Alert severity="error">{pageError}</Alert>
      </Stack>
    )
  }

  const deviceTitle = device?.name || device?.deviceId || 'Device details'

  return (
    <Stack spacing={3}>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} justifyContent="space-between" alignItems={{ xs: 'flex-start', sm: 'center' }}>
        <Stack spacing={1}>
          <Button startIcon={<ArrowBack />} onClick={() => navigate('/')} sx={{ alignSelf: 'flex-start' }}>
            Back to devices
          </Button>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} alignItems={{ xs: 'flex-start', sm: 'center' }}>
            <Typography variant="h4">{deviceTitle}</Typography>
            <Chip
              label={device?.status === 'registered' ? 'Registered' : 'Unregistered'}
              color={device?.status === 'registered' ? 'success' : 'warning'}
              size="small"
            />
            {device?.status === 'registered' && (
              <Chip
                label={device.hasPendingConfiguration ? 'Config pending sync' : 'Config applied'}
                color={device.hasPendingConfiguration ? 'warning' : 'success'}
                variant={device.hasPendingConfiguration ? 'filled' : 'outlined'}
                size="small"
              />
            )}
          </Stack>
          <Typography variant="body2" color="text.secondary">
            {device?.deviceId}
          </Typography>
        </Stack>
      </Stack>

      {pageError && <Alert severity="error">{pageError}</Alert>}
      {actionError && <Alert severity="error">{actionError}</Alert>}
      {successMessage && <Alert severity="success">{successMessage}</Alert>}

      <Card>
        <CardContent>
          <Tabs value={tab} onChange={(_, value: DeviceTab) => setTab(value)} variant="scrollable" allowScrollButtonsMobile>
            <Tab label="Sensor information" value={0} />
            <Tab label="Network and configuration" value={1} />
            <Tab label="Device logs" value={2} />
          </Tabs>

          <Divider sx={{ my: 2 }} />

          {tab === 0 && device && (
            <Stack spacing={3}>
              <Card
                sx={{
                  overflow: 'hidden',
                  background: (theme) =>
                    `linear-gradient(135deg, ${theme.palette.primary.main}18 0%, ${theme.palette.background.paper} 52%, ${theme.palette.success.main}12 100%)`,
                  border: 1,
                  borderColor: 'divider',
                }}
              >
                <CardContent>
                  <Grid container spacing={3} alignItems="center">
                    <Grid size={{ xs: 12, md: 4 }}>
                      <Stack direction="row" spacing={1.5} alignItems="center">
                        <Box
                          sx={{
                            width: 56,
                            height: 56,
                            display: 'grid',
                            placeItems: 'center',
                            borderRadius: 2,
                            color: 'primary.main',
                            bgcolor: 'primary.main',
                            backgroundColor: 'color-mix(in srgb, currentColor 12%, transparent)',
                          }}
                        >
                          <Thermostat sx={{ fontSize: 38 }} />
                        </Box>
                        <Stack spacing={0.25}>
                          <Typography variant="overline" color="text.secondary">
                            Water temperature
                          </Typography>
                          <Typography variant="h3" sx={{ fontWeight: 700, lineHeight: 1 }}>
                            {typeof device.latestTemperatureCelsius === 'number'
                              ? `${device.latestTemperatureCelsius.toFixed(1)}°C`
                              : '--'}
                          </Typography>
                          <Typography variant="caption" color="text.secondary">
                            {formatDate(device.latestTemperatureAtUtc)}
                          </Typography>
                        </Stack>
                      </Stack>
                    </Grid>
                    <Grid size={{ xs: 12, md: 4 }}>
                      <BatteryIndicator
                        percentage={device.battery.percentage}
                        chargeState={device.battery.chargeState}
                        batteryState={device.battery.batteryState}
                        size="hero"
                      />
                    </Grid>
                    <Grid size={{ xs: 12, md: 4 }}>
                      <Stack direction="row" spacing={1.5} alignItems="center">
                        <AccessTime sx={{ fontSize: 42, color: 'text.secondary' }} />
                        <Stack spacing={0.25}>
                          <Typography variant="overline" color="text.secondary">
                            Last seen
                          </Typography>
                          <Typography variant="h6" sx={{ fontWeight: 650 }}>
                            {formatDate(device.lastSeenAtUtc)}
                          </Typography>
                          <Typography variant="caption" color="text.secondary">
                            Device activity
                          </Typography>
                        </Stack>
                      </Stack>
                    </Grid>
                  </Grid>
                </CardContent>
              </Card>

              <Grid container spacing={3}>
                <Grid size={{ xs: 12, lg: 8 }}>
                  <DeviceMap deviceId={device.id} position={device.position} />
                </Grid>
                <Grid size={{ xs: 12, lg: 4 }}>
                  <Card variant="outlined" sx={{ height: '100%' }}>
                    <CardContent>
                      <Typography variant="h6" sx={{ mb: 2 }}>
                        Position snapshot
                      </Typography>
                      <Stack direction="row" flexWrap="wrap" gap={2}>
                        <DetailRow label="Latitude" value={formatValue(device.position.latitude)} />
                        <DetailRow label="Longitude" value={formatValue(device.position.longitude)} />
                        <DetailRow label="Altitude" value={formatValue(device.position.altitudeMeters, ' m')} />
                        <DetailRow label="GPS time" value={formatDate(device.position.gpsTimeUtc)} />
                        <DetailRow label="Speed" value={formatValue(device.position.speedKnots, ' kn')} />
                        <DetailRow label="HDOP" value={formatValue(device.position.hdop)} />
                        <DetailRow label="Satellites visible" value={formatValue(device.position.satellitesVisible)} />
                        <DetailRow label="Satellites used" value={formatValue(device.position.satellitesUsed)} />
                        <DetailRow label="Recorded at" value={formatDate(device.position.recordedAtUtc)} />
                      </Stack>
                    </CardContent>
                  </Card>
                </Grid>

                <Grid size={{ xs: 12, lg: 8 }}>
                  <BatteryHistoryChart deviceId={device.id} />
                </Grid>
                <Grid size={{ xs: 12, lg: 4 }}>
                  <Card variant="outlined" sx={{ height: '100%' }}>
                    <CardContent>
                      <Typography variant="h6" sx={{ mb: 2 }}>
                        Battery status
                      </Typography>
                      <Stack spacing={2.5}>
                        <BatteryIndicator
                          percentage={device.battery.percentage}
                          chargeState={device.battery.chargeState}
                          batteryState={device.battery.batteryState}
                        />
                        <Divider />
                        <Stack direction="row" flexWrap="wrap" gap={2}>
                          <DetailRow label="Charge state" value={formatBatteryState(device.battery.chargeState)} />
                          <DetailRow label="Modem voltage" value={formatValue(device.battery.modemMillivolts, ' mV')} />
                          <DetailRow label="ADC voltage" value={formatValue(device.battery.adcVoltage, ' V')} />
                          <DetailRow label="Modem reading valid" value={formatValue(device.battery.modemReadingValid)} />
                          <DetailRow label="Recorded at" value={formatDate(device.battery.recordedAtUtc)} />
                        </Stack>
                      </Stack>
                    </CardContent>
                  </Card>
                </Grid>

                <Grid size={{ xs: 12, lg: 6 }}>
                  <Card variant="outlined" sx={{ height: '100%' }}>
                    <CardContent>
                      <Typography variant="h6" sx={{ mb: 2 }}>
                        Latest sensor snapshot
                      </Typography>
                      <Stack direction="row" flexWrap="wrap" gap={2}>
                        <DetailRow label="Temperature" value={formatValue(device.latestTemperatureCelsius, '°C')} />
                        <DetailRow label="Last seen" value={formatDate(device.lastSeenAtUtc)} />
                        <DetailRow label="Last discovered" value={formatDate(device.lastDiscoveredAtUtc)} />
                        <DetailRow label="Registered at" value={formatDate(device.registeredAtUtc)} />
                        <DetailRow label="Latest temperature at" value={formatDate(device.latestTemperatureAtUtc)} />
                      </Stack>
                    </CardContent>
                  </Card>
                </Grid>

                <Grid size={{ xs: 12, lg: 6 }}>
                  <Card variant="outlined" sx={{ height: '100%' }}>
                    <CardContent>
                      <Typography variant="h6" sx={{ mb: 2 }}>
                        Stored telemetry
                      </Typography>
                      <Stack direction="row" flexWrap="wrap" gap={2} alignItems="center">
                        <DetailRow label="Temperature history entries" value={String(device.temperatureHistoryCount)} />
                        <DetailRow label="Position history entries" value={String(device.positionHistoryCount)} />
                        {device.status === 'registered' ? null : (
                          <Typography variant="body2" color="text.secondary">
                            Register this device below before editing runtime configuration.
                          </Typography>
                        )}
                      </Stack>
                    </CardContent>
                  </Card>
                </Grid>
              </Grid>

              {device.status === 'unregistered' && (
                <Card variant="outlined">
                  <CardContent>
                    <Typography variant="h6" sx={{ mb: 2 }}>
                      Register device
                    </Typography>
                    <Box component="form" onSubmit={handleRegisterSubmit}>
                      <Stack spacing={2}>
                        <TextField
                          label="Name"
                          value={name}
                          onChange={(event) => {
                            formDirtyRef.current = true
                            setName(event.target.value)
                          }}
                          required
                          fullWidth
                        />
                        <TextField
                          label="Place"
                          value={place}
                          onChange={(event) => {
                            formDirtyRef.current = true
                            setPlace(event.target.value)
                          }}
                          required
                          fullWidth
                        />
                        <TextField
                          label="Report interval seconds"
                          value={reportIntervalSeconds}
                          onChange={(event) => {
                            formDirtyRef.current = true
                            setReportIntervalSeconds(event.target.value)
                          }}
                          type="number"
                          inputProps={{ min: 1 }}
                          required
                          fullWidth
                        />
                        <FormControlLabel
                          control={
                            <Switch
                              checked={pushToHomeAssistant}
                              onChange={(_, checked) => {
                                formDirtyRef.current = true
                                setPushToHomeAssistant(checked)
                              }}
                            />
                          }
                          label="Send data to Home Assistant"
                        />
                        <TextField
                          label="Home Assistant device name"
                          value={homeAssistantDeviceName}
                          onChange={(event) => {
                            formDirtyRef.current = true
                            setHomeAssistantDeviceName(event.target.value)
                            setHomeAssistantDeviceNameError(null)
                          }}
                          onBlur={handleHomeAssistantDeviceNameBlur}
                          error={Boolean(homeAssistantDeviceNameError)}
                          helperText={
                            homeAssistantDeviceNameError ||
                            `Defaults to the device name in this system (${getEffectiveHomeAssistantDeviceName()}).`
                          }
                          placeholder={getEffectiveHomeAssistantDeviceName()}
                          fullWidth
                        />
                        <Button
                          type="submit"
                          variant="contained"
                          disabled={actionLoading || !isRegistrationFormComplete}
                          sx={{ alignSelf: 'flex-start' }}
                        >
                          {actionLoading ? 'Registering...' : 'Register device'}
                        </Button>
                      </Stack>
                    </Box>
                  </CardContent>
                </Card>
              )}
            </Stack>
          )}

          {tab === 1 && device && (
            <Stack spacing={3}>
              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" sx={{ mb: 2 }}>
                    Device metadata and desired configuration
                  </Typography>

                  {device.status === 'registered' ? (
                    <Box component="form" onSubmit={handleSaveConfiguration}>
                      <Stack spacing={2}>
                        <TextField
                          label="Name"
                          value={name}
                          onChange={(event) => {
                            formDirtyRef.current = true
                            setName(event.target.value)
                          }}
                          required
                          fullWidth
                        />
                        <TextField
                          label="Place"
                          value={place}
                          onChange={(event) => {
                            formDirtyRef.current = true
                            setPlace(event.target.value)
                          }}
                          required
                          fullWidth
                        />
                        <TextField
                          label="Report interval seconds"
                          value={reportIntervalSeconds}
                          onChange={(event) => {
                            formDirtyRef.current = true
                            setReportIntervalSeconds(event.target.value)
                          }}
                          type="number"
                          inputProps={{ min: 1 }}
                          required
                          fullWidth
                        />
                        <FormControlLabel
                          control={
                            <Switch
                              checked={pushToHomeAssistant}
                              onChange={(_, checked) => {
                                formDirtyRef.current = true
                                setPushToHomeAssistant(checked)
                              }}
                            />
                          }
                          label="Send data to Home Assistant"
                        />
                        <TextField
                          label="Home Assistant device name"
                          value={homeAssistantDeviceName}
                          onChange={(event) => {
                            formDirtyRef.current = true
                            setHomeAssistantDeviceName(event.target.value)
                            setHomeAssistantDeviceNameError(null)
                          }}
                          onBlur={handleHomeAssistantDeviceNameBlur}
                          error={Boolean(homeAssistantDeviceNameError)}
                          helperText={
                            homeAssistantDeviceNameError ||
                            `Changing this will change the Home Assistant device name. Defaults to "${name.trim() || device.deviceId}" when left blank.`
                          }
                          placeholder={getEffectiveHomeAssistantDeviceName()}
                          fullWidth
                        />
                        <Button
                          type="submit"
                          variant="contained"
                          disabled={actionLoading || !isConfigurationDirty}
                          sx={{ alignSelf: 'flex-start' }}
                        >
                          {actionLoading ? 'Saving...' : 'Save changes'}
                        </Button>
                      </Stack>
                    </Box>
                  ) : (
                    <Alert severity="info">Register the device before editing name, place, or report interval.</Alert>
                  )}
                </CardContent>
              </Card>

              <Stack direction={{ xs: 'column', lg: 'row' }} spacing={3}>
                <Card variant="outlined" sx={{ flex: 1 }}>
                  <CardContent>
                    <Typography variant="h6" sx={{ mb: 2 }}>
                      Configuration sync state
                    </Typography>
                    <Stack direction="row" flexWrap="wrap" gap={2}>
                      <DetailRow label="Desired version" value={String(device.desiredConfiguration.version)} />
                      <DetailRow label="Desired interval" value={formatValue(device.desiredConfiguration.reportIntervalSeconds, ' seconds')} />
                      <DetailRow label="Desired updated" value={formatDate(device.desiredConfiguration.updatedAtUtc)} />
                      <DetailRow label="Applied version" value={formatValue(device.runtimeConfiguration.appliedConfigurationVersion)} />
                      <DetailRow label="Applied interval" value={formatValue(device.runtimeConfiguration.appliedReportIntervalSeconds, ' seconds')} />
                      <DetailRow label="Runtime reported" value={formatDate(device.runtimeConfiguration.reportedAtUtc)} />
                    </Stack>
                  </CardContent>
                </Card>

                <Card variant="outlined" sx={{ flex: 1 }}>
                  <CardContent>
                    <Typography variant="h6" sx={{ mb: 2 }}>
                      Network diagnostics
                    </Typography>
                    <Stack direction="row" flexWrap="wrap" gap={2}>
                      <DetailRow label="Transport" value={formatValue(device.networkDiagnostics.transport)} />
                      <DetailRow label="Wi-Fi SSID" value={formatValue(device.networkDiagnostics.wifi?.ssid)} />
                      <DetailRow label="Wi-Fi RSSI" value={formatValue(device.networkDiagnostics.wifi?.wifiRssiDbm, ' dBm')} />
                      <DetailRow label="Wi-Fi IP" value={formatValue(device.networkDiagnostics.wifi?.localIp)} />
                      <DetailRow label="Cellular operator" value={formatValue(device.networkDiagnostics.cellular?.operator)} />
                      <DetailRow label="Cellular signal" value={formatValue(device.networkDiagnostics.cellular?.signalQuality)} />
                      <DetailRow label="Cellular GPRS" value={formatValue(device.networkDiagnostics.cellular?.gprsConnected)} />
                      <DetailRow label="Firmware" value={formatValue(device.firmwareVersion)} />
                    </Stack>
                  </CardContent>
                </Card>
              </Stack>

              <Card variant="outlined">
                <CardContent>
                  <Typography variant="h6" sx={{ mb: 2 }}>
                    Device actions
                  </Typography>
                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} flexWrap="wrap">
                    <Button variant="outlined" onClick={() => setPendingAction('regenerate')} disabled={actionLoading || device.status !== 'registered'}>
                      Regenerate API key
                    </Button>
                    <Button variant="outlined" color="warning" onClick={() => setPendingAction('clear-temperature')} disabled={actionLoading}>
                      Clear temperature history
                    </Button>
                    <Button variant="outlined" color="warning" onClick={() => setPendingAction('clear-position')} disabled={actionLoading}>
                      Clear position history
                    </Button>
                    <Button variant="outlined" color="error" onClick={() => setPendingAction('delete')} disabled={actionLoading}>
                      Delete device
                    </Button>
                  </Stack>
                </CardContent>
              </Card>
            </Stack>
          )}

          {tab === 2 && (
            <Stack spacing={2}>
              {logsError && <Alert severity="warning">{logsError}</Alert>}
              <Card variant="outlined">
                <CardContent>
                  <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} justifyContent="space-between" alignItems={{ xs: 'flex-start', sm: 'center' }} sx={{ mb: 2 }}>
                    <Typography variant="h6">Device log history</Typography>
                    <Stack direction={{ xs: 'column', md: 'row' }} spacing={1} alignItems={{ xs: 'flex-start', md: 'center' }}>
                      <Typography variant="body2" color="text.secondary">
                        Loaded {filteredLogEntries.length}
                        {logs?.totalCount != null ? ` of ${logs.totalCount.toLocaleString()}` : ''} matching log entries
                      </Typography>
                      {!isFollowingLogs && logEntries.length > 0 ? (
                        <Button size="small" onClick={jumpToLatestLogs}>
                          Jump to latest
                        </Button>
                      ) : null}
                      <Button
                        size="small"
                        variant="outlined"
                        color="warning"
                        onClick={() => setRetentionDialogOpen(true)}
                        disabled={actionLoading}
                      >
                        Delete old logs
                      </Button>
                      <Button
                        size="small"
                        variant="outlined"
                        color="error"
                        onClick={() => setPendingAction('delete-all-logs')}
                        disabled={actionLoading}
                      >
                        Delete all logs
                      </Button>
                    </Stack>
                  </Stack>

                  <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ mb: 2 }}>
                    <FormControl size="small" sx={{ minWidth: 160 }}>
                      <InputLabel id="log-severity-filter-label">Severity</InputLabel>
                      <Select
                        labelId="log-severity-filter-label"
                        label="Severity"
                        value={logSeverityFilter}
                        onChange={(event) => setLogSeverityFilter(event.target.value)}
                      >
                        <MenuItem value="all">All severities</MenuItem>
                        {availableLogSeverities.map((severity) => (
                          <MenuItem key={severity} value={severity}>
                            {severity}
                          </MenuItem>
                        ))}
                      </Select>
                    </FormControl>
                    <TextField
                      label="From"
                      type="datetime-local"
                      size="small"
                      value={logFromTime}
                      onChange={(event) => setLogFromTime(event.target.value)}
                      InputLabelProps={{ shrink: true }}
                    />
                    <TextField
                      label="To"
                      type="datetime-local"
                      size="small"
                      value={logToTime}
                      onChange={(event) => setLogToTime(event.target.value)}
                      InputLabelProps={{ shrink: true }}
                    />
                    <TextField
                      label="Search log line"
                      size="small"
                      fullWidth
                      value={logTextFilter}
                      onChange={(event) => setLogTextFilter(event.target.value)}
                    />
                  </Stack>

                  {logs?.hasMore && logEntries.length > 0 ? (
                    <Button
                      size="small"
                      onClick={() => void loadOlderLogs()}
                      disabled={olderLogsLoading}
                      sx={{ mb: 1.5 }}
                    >
                      {olderLogsLoading ? 'Loading...' : 'Load older matching logs'}
                    </Button>
                  ) : null}

                  {logsLoading ? (
                    <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
                      <CircularProgress size={28} />
                    </Box>
                  ) : logEntries.length > 0 ? (
                    filteredLogEntries.length > 0 ? (
                      <Box
                        ref={logsContainerRef}
                        onScroll={handleLogsScroll}
                        sx={{
                          height: 'calc(100vh - 420px)',
                          minHeight: 320,
                          overflowY: 'auto',
                          border: 1,
                          borderColor: 'divider',
                          borderRadius: 2,
                          bgcolor: 'background.default',
                        }}
                      >
                        <Box
                          sx={{
                            display: 'grid',
                            gridTemplateColumns: {
                              xs: 'minmax(120px, 140px) minmax(72px, 96px) minmax(0, 1fr)',
                              sm: 'minmax(180px, 200px) minmax(88px, 110px) minmax(0, 1fr)',
                            },
                            gap: 2,
                            px: 2,
                            py: 1.5,
                            position: 'sticky',
                            top: 0,
                            zIndex: 1,
                            borderBottom: 1,
                            borderColor: 'divider',
                            bgcolor: 'background.paper',
                          }}
                        >
                          <Typography variant="caption" color="text.secondary">
                            Time
                          </Typography>
                          <Typography variant="caption" color="text.secondary">
                            Severity
                          </Typography>
                          <Typography variant="caption" color="text.secondary">
                            Log line
                          </Typography>
                        </Box>

                        {filteredLogEntries.map((entry) => (
                          <Box
                            key={entry.id}
                            sx={{
                              display: 'grid',
                              gridTemplateColumns: {
                                xs: 'minmax(120px, 140px) minmax(72px, 96px) minmax(0, 1fr)',
                                sm: 'minmax(180px, 200px) minmax(88px, 110px) minmax(0, 1fr)',
                              },
                              gap: 2,
                              px: 2,
                              py: 1,
                              borderBottom: 1,
                              borderColor: 'divider',
                              alignItems: 'start',
                              '&:last-child': {
                                borderBottom: 'none',
                              },
                            }}
                          >
                            <Typography variant="body2" color="text.secondary" sx={{ pt: 0.25 }}>
                              {new Date(entry.timestampUtc).toLocaleString()}
                            </Typography>
                            <Chip
                              label={entry.level ?? 'unknown'}
                              size="small"
                              color={
                                entry.level?.toLowerCase() === 'error'
                                  ? 'error'
                                  : entry.level?.toLowerCase() === 'warning'
                                    ? 'warning'
                                    : 'default'
                              }
                              sx={{ justifySelf: 'start' }}
                            />
                            <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
                              {entry.message}
                            </Typography>
                          </Box>
                        ))}
                      </Box>
                    ) : (
                      <Typography variant="body2" color="text.secondary">
                        No log entries match the current filters.
                      </Typography>
                    )
                  ) : (
                    <Typography variant="body2" color="text.secondary">
                      No device logs have been stored yet.
                    </Typography>
                  )}
                </CardContent>
              </Card>
            </Stack>
          )}
        </CardContent>
      </Card>

      <Dialog open={pendingAction !== null} onClose={() => setPendingAction(null)}>
        <DialogTitle>Confirm action</DialogTitle>
        <DialogContent>
          <Typography>
            {pendingAction === 'regenerate' && 'This invalidates the current device credential and requires the device to rediscover before sending updates again.'}
            {pendingAction === 'clear-temperature' && 'This permanently removes all stored temperature history for this device.'}
            {pendingAction === 'clear-position' && 'This permanently removes all stored position history for this device.'}
            {pendingAction === 'delete-all-logs' && 'This permanently removes every stored log entry for this device. New device logs will continue to be collected.'}
            {pendingAction === 'delete' && 'This permanently deletes the device together with its telemetry and logs.'}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPendingAction(null)} disabled={actionLoading}>
            Cancel
          </Button>
          <Button
            onClick={() => void runPendingAction()}
            color={pendingAction === 'delete' || pendingAction === 'delete-all-logs' ? 'error' : 'primary'}
            disabled={actionLoading}
          >
            {actionLoading ? 'Working...' : 'Confirm'}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={retentionDialogOpen} onClose={() => !actionLoading && setRetentionDialogOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>Delete old device logs</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Typography variant="body2" color="text.secondary">
              Choose how much recent history to keep. Older entries will be permanently deleted.
            </Typography>
            <FormControl fullWidth>
              <InputLabel id="log-retention-label">Keep recent logs</InputLabel>
              <Select
                labelId="log-retention-label"
                label="Keep recent logs"
                value={retentionPresetHours}
                onChange={(event) => setRetentionPresetHours(event.target.value)}
              >
                <MenuItem value="1">Last 1 hour</MenuItem>
                <MenuItem value="24">Last 24 hours</MenuItem>
                <MenuItem value="168">Last 7 days (1 week)</MenuItem>
                <MenuItem value="custom">Custom number of days</MenuItem>
              </Select>
            </FormControl>
            {retentionPresetHours === 'custom' ? (
              <TextField
                label="Days to keep"
                type="number"
                value={customRetentionDays}
                onChange={(event) => setCustomRetentionDays(event.target.value)}
                inputProps={{ min: 1, step: 1 }}
                fullWidth
              />
            ) : null}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRetentionDialogOpen(false)} disabled={actionLoading}>
            Cancel
          </Button>
          <Button color="error" onClick={() => void deleteLogsOlderThanRetention()} disabled={actionLoading}>
            {actionLoading ? 'Deleting...' : 'Delete older logs'}
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  )
}
