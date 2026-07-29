import { useEffect, useId, useState } from 'react'
import dayjs, { type Dayjs } from 'dayjs'
import {
  Button,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Typography,
  type SelectChangeEvent,
} from '@mui/material'
import { DateTimePicker } from '@mui/x-date-pickers/DateTimePicker'

export type TelemetryRangePreset =
  | '1h'
  | '3h'
  | '6h'
  | '12h'
  | '24h'
  | '7d'
  | '14d'
  | '21d'
  | '30d'
  | '90d'
  | 'all'
  | 'custom'

export interface TelemetryDateRange {
  preset: TelemetryRangePreset
  customFromUtc?: string
  customToUtc?: string
}

interface TelemetryDateRangeSelectorProps {
  value: TelemetryDateRange
  onChange: (value: TelemetryDateRange) => void
  disabled?: boolean
  compact?: boolean
}

const presetDurationsMs: Partial<Record<TelemetryRangePreset, number>> = {
  '1h': 60 * 60 * 1000,
  '3h': 3 * 60 * 60 * 1000,
  '6h': 6 * 60 * 60 * 1000,
  '12h': 12 * 60 * 60 * 1000,
  '24h': 24 * 60 * 60 * 1000,
  '7d': 7 * 24 * 60 * 60 * 1000,
  '14d': 14 * 24 * 60 * 60 * 1000,
  '21d': 21 * 24 * 60 * 60 * 1000,
  '30d': 30 * 24 * 60 * 60 * 1000,
  '90d': 90 * 24 * 60 * 60 * 1000,
}

export function resolveTelemetryDateRange(value: TelemetryDateRange, now = new Date()) {
  if (value.preset === 'custom') {
    return {
      fromUtc: value.customFromUtc,
      toUtc: value.customToUtc,
    }
  }

  if (value.preset === 'all') {
    return {
      fromUtc: new Date(0).toISOString(),
      toUtc: now.toISOString(),
    }
  }

  const durationMs = presetDurationsMs[value.preset] ?? presetDurationsMs['24h']!
  return {
    fromUtc: new Date(now.getTime() - durationMs).toISOString(),
    toUtc: now.toISOString(),
  }
}

export default function TelemetryDateRangeSelector({
  value,
  onChange,
  disabled = false,
  compact = false,
}: TelemetryDateRangeSelectorProps) {
  const rangeLabelId = useId()
  const [draftFrom, setDraftFrom] = useState<Dayjs | null>(
    value.customFromUtc ? dayjs(value.customFromUtc) : dayjs().subtract(1, 'day'),
  )
  const [draftTo, setDraftTo] = useState<Dayjs | null>(
    value.customToUtc ? dayjs(value.customToUtc) : dayjs(),
  )
  const [customError, setCustomError] = useState<string | null>(null)

  useEffect(() => {
    if (value.customFromUtc) {
      setDraftFrom(dayjs(value.customFromUtc))
    }
    if (value.customToUtc) {
      setDraftTo(dayjs(value.customToUtc))
    }
  }, [value.customFromUtc, value.customToUtc])

  const handlePresetChange = (event: SelectChangeEvent<TelemetryRangePreset>) => {
    const preset = event.target.value as TelemetryRangePreset
    setCustomError(null)

    if (preset === 'custom') {
      const customFrom = value.customFromUtc ? dayjs(value.customFromUtc) : dayjs().subtract(1, 'day')
      const customTo = value.customToUtc ? dayjs(value.customToUtc) : dayjs()
      setDraftFrom(customFrom)
      setDraftTo(customTo)
      onChange({
        preset,
        customFromUtc: customFrom.toISOString(),
        customToUtc: customTo.toISOString(),
      })
      return
    }

    onChange({ preset })
  }

  const applyCustomRange = () => {
    if (!draftFrom?.isValid() || !draftTo?.isValid()) {
      setCustomError('Choose a valid start and end time.')
      return
    }
    if (!draftFrom.isBefore(draftTo)) {
      setCustomError('The start time must be earlier than the end time.')
      return
    }

    setCustomError(null)
    onChange({
      preset: 'custom',
      customFromUtc: draftFrom.toISOString(),
      customToUtc: draftTo.toISOString(),
    })
  }

  return (
    <Stack spacing={1} sx={{ minWidth: compact ? 180 : 220 }}>
      <FormControl size="small" disabled={disabled}>
        <InputLabel id={rangeLabelId}>Date range</InputLabel>
        <Select
          labelId={rangeLabelId}
          value={value.preset}
          label="Date range"
          onChange={handlePresetChange}
        >
          <MenuItem value="1h">Last 1 hour</MenuItem>
          <MenuItem value="3h">Last 3 hours</MenuItem>
          <MenuItem value="6h">Last 6 hours</MenuItem>
          <MenuItem value="12h">Last 12 hours</MenuItem>
          <MenuItem value="24h">Last 24 hours</MenuItem>
          <MenuItem value="7d">Last 7 days</MenuItem>
          <MenuItem value="14d">Last 14 days</MenuItem>
          <MenuItem value="21d">Last 21 days</MenuItem>
          <MenuItem value="30d">Last 30 days</MenuItem>
          <MenuItem value="90d">Last 90 days</MenuItem>
          <MenuItem value="all">All history</MenuItem>
          <MenuItem value="custom">Custom range</MenuItem>
        </Select>
      </FormControl>

      {value.preset === 'custom' && (
        <Stack spacing={1}>
          <DateTimePicker
            label="From"
            value={draftFrom}
            onChange={setDraftFrom}
            disabled={disabled}
            slotProps={{ textField: { size: 'small', fullWidth: true } }}
          />
          <DateTimePicker
            label="To"
            value={draftTo}
            onChange={setDraftTo}
            disabled={disabled}
            slotProps={{ textField: { size: 'small', fullWidth: true } }}
          />
          {customError && (
            <Typography variant="caption" color="error">
              {customError}
            </Typography>
          )}
          <Button size="small" variant="outlined" onClick={applyCustomRange} disabled={disabled}>
            Apply range
          </Button>
        </Stack>
      )}
    </Stack>
  )
}
