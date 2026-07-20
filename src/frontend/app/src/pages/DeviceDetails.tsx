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
  FormControlLabel,
  Stack,
  Switch,
  Tab,
  Tabs,
  TextField,
  Typography,
} from '@mui/material'
import { ArrowBack } from '@mui/icons-material'
import { useNavigate, useParams } from 'react-router-dom'
import {
  clearDeviceTelemetry,
  deleteDevice,
  getDevice,
  getDeviceLogs,
  regenerateDeviceKey,
  registerDevice,
  updateDevice,
  type DeviceDetail,
  type DeviceLogsResponse,
} from '../api'

type DeviceTab = 0 | 1 | 2
type PendingAction = 'regenerate' | 'clear-temperature' | 'clear-position' | 'delete' | null

const POLL_INTERVAL_MS = 5000
const HIDDEN_POLL_INTERVAL_MS = 20000
const LOG_PAGE_SIZE = 100
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

  const logEntries = logs ? [...logs.items].reverse() : []

  useEffect(() => {
    if (!device) {
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
        const [detailResult, logsResult] = await Promise.allSettled([
          getDevice(deviceId),
          getDeviceLogs(deviceId, 1, LOG_PAGE_SIZE),
        ])

        if (cancelled) {
          return
        }

        if (detailResult.status === 'fulfilled') {
          setDevice(detailResult.value)
          setPageError(null)
        } else {
          const message = detailResult.reason?.message || 'Failed to load device details'
          setPageError(message)
        }

        if (logsResult.status === 'fulfilled') {
          setLogs(logsResult.value)
          setLogsError(null)
        } else {
          setLogsError(logsResult.reason?.message || 'Failed to load device logs')
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
    const [detail, deviceLogs] = await Promise.all([
      getDevice(deviceId),
      getDeviceLogs(deviceId, 1, LOG_PAGE_SIZE),
    ])

    setDevice(detail)
    setLogs(deviceLogs)
    setPageError(null)
    setLogsError(null)
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

  const withAction = async (action: () => Promise<void>, success: string) => {
    setActionLoading(true)
    setActionError(null)
    setSuccessMessage(null)

    try {
      await action()
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
              <Stack direction={{ xs: 'column', lg: 'row' }} spacing={3}>
                <Card variant="outlined" sx={{ flex: 1 }}>
                  <CardContent>
                    <Typography variant="h6" sx={{ mb: 2 }}>
                      Latest sensor snapshot
                    </Typography>
                    <Stack direction="row" flexWrap="wrap" gap={2}>
                      <DetailRow label="Temperature" value={formatValue(device.latestTemperatureCelsius, '°C')} />
                      <DetailRow label="Last seen" value={formatDate(device.lastSeenAtUtc)} />
                      <DetailRow label="Last update" value={formatDate(device.lastUpdateReceivedAtUtc)} />
                      <DetailRow label="Last discovered" value={formatDate(device.lastDiscoveredAtUtc)} />
                      <DetailRow label="Registered at" value={formatDate(device.registeredAtUtc)} />
                      <DetailRow label="Latest temperature at" value={formatDate(device.latestTemperatureAtUtc)} />
                    </Stack>
                  </CardContent>
                </Card>

                <Card variant="outlined" sx={{ flex: 1 }}>
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
              </Stack>

              <Card variant="outlined">
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

              {device.status === 'unregistered' && (
                <Card variant="outlined">
                  <CardContent>
                    <Typography variant="h6" sx={{ mb: 2 }}>
                      Register device
                    </Typography>
                    <Box component="form" onSubmit={handleRegisterSubmit}>
                      <Stack spacing={2}>
                        <TextField label="Name" value={name} onChange={(event) => setName(event.target.value)} required fullWidth />
                        <TextField label="Place" value={place} onChange={(event) => setPlace(event.target.value)} required fullWidth />
                        <TextField
                          label="Report interval seconds"
                          value={reportIntervalSeconds}
                          onChange={(event) => setReportIntervalSeconds(event.target.value)}
                          type="number"
                          inputProps={{ min: 1 }}
                          required
                          fullWidth
                        />
                        <FormControlLabel
                          control={<Switch checked={pushToHomeAssistant} onChange={(_, checked) => setPushToHomeAssistant(checked)} />}
                          label="Send data to Home Assistant"
                        />
                        <TextField
                          label="Home Assistant device name"
                          value={homeAssistantDeviceName}
                          onChange={(event) => {
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
                        <Button type="submit" variant="contained" disabled={actionLoading} sx={{ alignSelf: 'flex-start' }}>
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
                        <TextField label="Name" value={name} onChange={(event) => setName(event.target.value)} required fullWidth />
                        <TextField label="Place" value={place} onChange={(event) => setPlace(event.target.value)} required fullWidth />
                        <TextField
                          label="Report interval seconds"
                          value={reportIntervalSeconds}
                          onChange={(event) => setReportIntervalSeconds(event.target.value)}
                          type="number"
                          inputProps={{ min: 1 }}
                          required
                          fullWidth
                        />
                        <FormControlLabel
                          control={<Switch checked={pushToHomeAssistant} onChange={(_, checked) => setPushToHomeAssistant(checked)} />}
                          label="Send data to Home Assistant"
                        />
                        <TextField
                          label="Home Assistant device name"
                          value={homeAssistantDeviceName}
                          onChange={(event) => {
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
                    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} alignItems={{ xs: 'flex-start', sm: 'center' }}>
                      <Typography variant="body2" color="text.secondary">
                        Showing {logs?.items.length ?? 0} of {logs?.totalCount ?? 0} log entries
                      </Typography>
                      {!isFollowingLogs && logEntries.length > 0 ? (
                        <Button size="small" onClick={jumpToLatestLogs}>
                          Jump to latest
                        </Button>
                      ) : null}
                    </Stack>
                  </Stack>

                  {logEntries.length > 0 ? (
                    <Box
                      ref={logsContainerRef}
                      onScroll={handleLogsScroll}
                      sx={{
                        maxHeight: 480,
                        overflowY: 'auto',
                        border: 1,
                        borderColor: 'divider',
                        borderRadius: 2,
                        bgcolor: 'background.default',
                        fontFamily: 'Monaco, Menlo, Consolas, "Courier New", monospace',
                      }}
                    >
                      <Box
                        sx={{
                          display: 'grid',
                          gridTemplateColumns: { xs: 'minmax(120px, 132px) minmax(0, 1fr)', sm: 'minmax(180px, 220px) minmax(0, 1fr)' },
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
                        <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'inherit' }}>
                          Time
                        </Typography>
                        <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'inherit' }}>
                          Log line
                        </Typography>
                      </Box>

                      {logEntries.map((entry) => (
                        <Box
                          key={entry.id}
                          sx={{
                            display: 'grid',
                            gridTemplateColumns: { xs: 'minmax(120px, 132px) minmax(0, 1fr)', sm: 'minmax(180px, 220px) minmax(0, 1fr)' },
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
                          <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'inherit', pt: 0.25 }}>
                            {new Date(entry.receivedAtUtc).toLocaleString()}
                          </Typography>
                          <Typography variant="body2" sx={{ fontFamily: 'inherit', whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
                            {entry.level ? `[${entry.level}] ` : ''}
                            {entry.message}
                            <Box component="span" sx={{ ml: 1, color: 'text.secondary', fontSize: '0.75rem' }}>
                              #{entry.sequenceNumber}
                            </Box>
                          </Typography>
                        </Box>
                      ))}
                    </Box>
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
            {pendingAction === 'delete' && 'This permanently deletes the device together with its telemetry and logs.'}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPendingAction(null)} disabled={actionLoading}>
            Cancel
          </Button>
          <Button onClick={() => void runPendingAction()} color={pendingAction === 'delete' ? 'error' : 'primary'} disabled={actionLoading}>
            {actionLoading ? 'Working...' : 'Confirm'}
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  )
}