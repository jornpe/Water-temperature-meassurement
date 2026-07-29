import {
  Battery0Bar,
  Battery1Bar,
  Battery2Bar,
  Battery3Bar,
  Battery4Bar,
  Battery5Bar,
  Battery6Bar,
  BatteryAlert,
  BatteryChargingFull,
  BatteryFull,
  BatteryUnknown,
} from '@mui/icons-material'
import { Box, Stack, Typography } from '@mui/material'
import type { BatteryState } from '../api'

type BatteryIndicatorSize = 'compact' | 'standard' | 'hero'

interface BatteryIndicatorProps {
  percentage?: number | null
  chargeState?: number | null
  batteryState?: BatteryState | number | null
  status?: string | null
  label?: string
  size?: BatteryIndicatorSize
}

export function getBatteryStatus(
  chargeState?: number | null,
  batteryState?: BatteryState | number | null,
  suppliedStatus?: string | null,
) {
  if (suppliedStatus?.trim()) {
    return suppliedStatus.trim()
  }

  if (chargeState === 1 || batteryState === 1 || batteryState === 'Charging') {
    return 'Charging'
  }
  if (chargeState === 2 || batteryState === 2 || batteryState === 'Full') {
    return 'Full'
  }
  if (chargeState === 0 || batteryState === 0 || batteryState === 'NotCharging') {
    return 'Not charging'
  }

  return 'Unknown'
}

function getBatteryVisual(percentage?: number | null, status?: string | null) {
  const normalizedPercentage = typeof percentage === 'number'
    ? Math.max(0, Math.min(100, percentage))
    : null
  const normalizedStatus = status?.toLowerCase()

  if (normalizedStatus === 'charging') {
    return { Icon: BatteryChargingFull, color: 'info.main' as const }
  }
  if (normalizedStatus === 'full') {
    return { Icon: BatteryFull, color: 'success.main' as const }
  }
  if (normalizedPercentage === null) {
    return { Icon: BatteryUnknown, color: 'text.secondary' as const }
  }
  if (normalizedPercentage <= 10) {
    return { Icon: BatteryAlert, color: 'error.main' as const }
  }
  if (normalizedPercentage <= 20) {
    return { Icon: Battery0Bar, color: 'error.main' as const }
  }
  if (normalizedPercentage <= 35) {
    return { Icon: Battery1Bar, color: 'warning.main' as const }
  }
  if (normalizedPercentage <= 50) {
    return { Icon: Battery2Bar, color: 'warning.main' as const }
  }
  if (normalizedPercentage <= 65) {
    return { Icon: Battery3Bar, color: 'success.main' as const }
  }
  if (normalizedPercentage <= 75) {
    return { Icon: Battery4Bar, color: 'success.main' as const }
  }
  if (normalizedPercentage <= 90) {
    return { Icon: Battery5Bar, color: 'success.main' as const }
  }
  if (normalizedPercentage < 100) {
    return { Icon: Battery6Bar, color: 'success.main' as const }
  }

  return { Icon: BatteryFull, color: 'success.main' as const }
}

export default function BatteryIndicator({
  percentage,
  chargeState,
  batteryState,
  status,
  label = 'Battery',
  size = 'standard',
}: BatteryIndicatorProps) {
  const displayStatus = getBatteryStatus(chargeState, batteryState, status)
  const { Icon, color } = getBatteryVisual(percentage, displayStatus)
  const iconSize = size === 'hero' ? 58 : size === 'compact' ? 34 : 44
  const valueVariant = size === 'hero' ? 'h3' : size === 'compact' ? 'h6' : 'h4'

  return (
    <Stack direction="row" spacing={1.5} alignItems="center">
      <Box
        sx={{
          display: 'grid',
          placeItems: 'center',
          color,
          flexShrink: 0,
        }}
      >
        <Icon sx={{ fontSize: iconSize }} />
      </Box>
      <Stack spacing={0.15}>
        <Typography variant="overline" color="text.secondary" sx={{ lineHeight: 1.2 }}>
          {label}
        </Typography>
        <Typography variant={valueVariant} sx={{ fontWeight: 700, lineHeight: 1.05 }}>
          {typeof percentage === 'number' ? `${Math.round(percentage)}%` : '--'}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          {displayStatus}
        </Typography>
      </Stack>
    </Stack>
  )
}
