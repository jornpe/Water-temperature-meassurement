import { useEffect, useMemo, useState } from 'react'
import L, { LatLngBounds, type LatLngExpression } from 'leaflet'
import {
  CircleMarker,
  MapContainer,
  Marker,
  Polyline,
  Popup,
  TileLayer,
  useMap,
} from 'react-leaflet'
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  FormControlLabel,
  Stack,
  Switch,
  Typography,
} from '@mui/material'
import { Refresh } from '@mui/icons-material'
import {
  getDevicePositions,
  type DevicePositionHistoryPoint,
  type DevicePositionHistoryResponse,
  type DevicePositionSnapshot,
} from '../api'
import TelemetryDateRangeSelector, {
  resolveTelemetryDateRange,
  type TelemetryDateRange,
} from './TelemetryDateRangeSelector'
import './DeviceMap.css'

interface DeviceMapProps {
  deviceId: number
  position: DevicePositionSnapshot
}

const currentPositionIcon = L.divIcon({
  className: 'device-map-pin-wrapper',
  html: '<div class="device-map-pin"><span></span></div>',
  iconSize: [34, 38],
  iconAnchor: [17, 34],
  popupAnchor: [0, -34],
})

function isValidCoordinate(latitude?: number | null, longitude?: number | null) {
  return typeof latitude === 'number'
    && typeof longitude === 'number'
    && Number.isFinite(latitude)
    && Number.isFinite(longitude)
    && latitude >= -90
    && latitude <= 90
    && longitude >= -180
    && longitude <= 180
}

function collapseConsecutiveDuplicates(points: DevicePositionHistoryPoint[]) {
  return points.filter((point, index) => {
    if (!isValidCoordinate(point.latitude, point.longitude)) {
      return false
    }
    if (index === 0) {
      return true
    }

    const previous = points[index - 1]
    return previous.latitude !== point.latitude || previous.longitude !== point.longitude
  })
}

function MapViewport({
  coordinates,
  fitKey,
}: {
  coordinates: LatLngExpression[]
  fitKey: string
}) {
  const map = useMap()

  useEffect(() => {
    if (coordinates.length === 0) {
      return
    }
    if (coordinates.length === 1) {
      map.setView(coordinates[0], 14)
      return
    }

    map.fitBounds(new LatLngBounds(coordinates), {
      padding: [32, 32],
      maxZoom: 16,
    })
    // fitKey changes only for deliberate view changes (route/range toggles).
    // Fresh GPS positions update the marker without interrupting manual panning.
  }, [fitKey, map])

  return null
}

function formatMapTimestamp(value?: string | null) {
  return value ? new Date(value).toLocaleString() : 'Not available'
}

export default function DeviceMap({ deviceId, position }: DeviceMapProps) {
  const [showRoute, setShowRoute] = useState(false)
  const [range, setRange] = useState<TelemetryDateRange>({ preset: '24h' })
  const [route, setRoute] = useState<DevicePositionHistoryResponse | null>(null)
  const [routeLoading, setRouteLoading] = useState(false)
  const [routeError, setRouteError] = useState<string | null>(null)
  const [refreshVersion, setRefreshVersion] = useState(0)

  useEffect(() => {
    if (!showRoute) {
      return
    }

    let cancelled = false
    const loadRoute = async () => {
      setRouteLoading(true)
      try {
        const selectedRange = resolveTelemetryDateRange(range)
        const response = await getDevicePositions(deviceId, {
          ...selectedRange,
          maxPoints: 10_000,
        })
        if (!cancelled) {
          setRoute(response)
          setRouteError(null)
        }
      } catch (error: any) {
        if (!cancelled) {
          setRouteError(error.message || 'Failed to load the device route')
        }
      } finally {
        if (!cancelled) {
          setRouteLoading(false)
        }
      }
    }

    void loadRoute()
    return () => {
      cancelled = true
    }
  }, [deviceId, range, refreshVersion, showRoute])

  const routePoints = useMemo(
    () => collapseConsecutiveDuplicates(route?.items ?? []),
    [route?.items],
  )
  const routeCoordinates = useMemo<LatLngExpression[]>(
    () => routePoints.map((point) => [point.latitude, point.longitude]),
    [routePoints],
  )
  const hasCurrentPosition = isValidCoordinate(position.latitude, position.longitude)
  const currentCoordinate = useMemo(
    () => hasCurrentPosition
      ? [position.latitude!, position.longitude!] as LatLngExpression
      : null,
    [hasCurrentPosition, position.latitude, position.longitude],
  )
  const fitCoordinates = useMemo(() => {
    const coordinates = showRoute ? [...routeCoordinates] : []
    if (currentCoordinate) {
      coordinates.push(currentCoordinate)
    }
    return coordinates
  }, [currentCoordinate, routeCoordinates, showRoute])
  const initialCenter = currentCoordinate ?? (showRoute ? routeCoordinates[0] : null) ?? [59.9139, 10.7522]
  const hasMapData = Boolean(currentCoordinate) || (showRoute && routeCoordinates.length > 0)
  const fitKey = showRoute
    ? `${range.preset}-${routePoints[0]?.id ?? 'empty'}-${routePoints[routePoints.length - 1]?.id ?? 'empty'}-${refreshVersion}`
    : 'current-position'

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
            <Typography variant="h6">Location and route</Typography>
            <Typography variant="body2" color="text.secondary">
              Latest GPS fix with optional position history.
            </Typography>
            <FormControlLabel
              control={
                <Switch
                  checked={showRoute}
                  onChange={(_, checked) => {
                    setShowRoute(checked)
                    setRouteError(null)
                  }}
                />
              }
              label="Show route"
            />
          </Stack>

          {showRoute && (
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} alignItems="flex-start">
              <TelemetryDateRangeSelector
                value={range}
                onChange={setRange}
                disabled={routeLoading}
                compact
              />
              <Button
                variant="outlined"
                size="small"
                startIcon={<Refresh />}
                onClick={() => setRefreshVersion((value) => value + 1)}
                disabled={routeLoading}
              >
                Refresh
              </Button>
            </Stack>
          )}
        </Stack>

        {routeError && <Alert severity="warning" sx={{ mb: 2 }}>{routeError}</Alert>}
        {route?.isSampled && (
          <Alert severity="info" sx={{ mb: 2 }}>
            This route is simplified from {route.totalCount.toLocaleString()} stored GPS updates. Choose a shorter range for more detail.
          </Alert>
        )}

        {routeLoading && !hasMapData ? (
          <Box sx={{ minHeight: 420, display: 'grid', placeItems: 'center' }}>
            <CircularProgress />
          </Box>
        ) : hasMapData ? (
          <Box
            sx={{
              height: { xs: 340, md: 440 },
              minHeight: 340,
              overflow: 'hidden',
              borderRadius: 2,
              border: 1,
              borderColor: 'divider',
            }}
          >
            <MapContainer
              center={initialCenter}
              zoom={14}
              scrollWheelZoom
              style={{ height: '100%', width: '100%' }}
            >
              <TileLayer
                attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
                url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
              />
              {showRoute && routeCoordinates.length > 1 && (
                <Polyline
                  positions={routeCoordinates}
                  pathOptions={{ color: '#42a5f5', weight: 4, opacity: 0.85 }}
                />
              )}
              {showRoute && routePoints.length > 0 && (
                <CircleMarker
                  center={[routePoints[0].latitude, routePoints[0].longitude]}
                  radius={7}
                  pathOptions={{ color: '#fff', fillColor: '#2e7d32', fillOpacity: 1, weight: 2 }}
                >
                  <Popup>
                    <strong>Route start</strong>
                    <br />
                    {formatMapTimestamp(routePoints[0].recordedAtUtc)}
                  </Popup>
                </CircleMarker>
              )}
              {currentCoordinate && (
                <Marker position={currentCoordinate} icon={currentPositionIcon}>
                  <Popup>
                    <strong>Current device position</strong>
                    <br />
                    {position.latitude?.toFixed(6)}, {position.longitude?.toFixed(6)}
                    <br />
                    Recorded: {formatMapTimestamp(position.recordedAtUtc)}
                    {typeof position.speedKnots === 'number' && (
                      <>
                        <br />
                        Speed: {position.speedKnots.toFixed(1)} kn
                      </>
                    )}
                  </Popup>
                </Marker>
              )}
              <MapViewport coordinates={fitCoordinates} fitKey={fitKey} />
            </MapContainer>
          </Box>
        ) : (
          <Box
            sx={{
              minHeight: 300,
              display: 'grid',
              placeItems: 'center',
              border: 1,
              borderStyle: 'dashed',
              borderColor: 'divider',
              borderRadius: 2,
              bgcolor: 'action.hover',
              textAlign: 'center',
              px: 3,
            }}
          >
            <Stack spacing={0.5}>
              <Typography variant="h6">No GPS position available</Typography>
              <Typography variant="body2" color="text.secondary">
                The map will appear after the device reports its first valid GPS fix.
              </Typography>
            </Stack>
          </Box>
        )}
      </CardContent>
    </Card>
  )
}
