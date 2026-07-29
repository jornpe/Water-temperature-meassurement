import * as React from 'react';
import Grid from '@mui/material/Grid';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import CardActionArea from '@mui/material/CardActionArea';
import Stack from '@mui/material/Stack';
import Chip from '@mui/material/Chip';
import { Thermostat, Sensors, FmdGood, WarningAmber } from '@mui/icons-material';
import type { DeviceSummary } from '../api';
import BatteryIndicator from './BatteryIndicator';
import { formatElapsedTime } from '../utils/formatElapsedTime';

interface MainGridProps {
  devices: DeviceSummary[];
  onSelectDevice: (device: DeviceSummary) => void;
}

export default function MainGrid({ devices, onSelectDevice }: MainGridProps) {
  const [nowMs, setNowMs] = React.useState(() => Date.now())
  const registeredDevices = devices.filter((device) => device.status === 'registered')
  const unregisteredDevices = devices.length - registeredDevices.length
  const reportingDevices = devices.filter((device) => Boolean(device.lastUpdateReceivedAtUtc)).length
  const averageTemperature = registeredDevices.length > 0
    ? registeredDevices
        .filter((device) => typeof device.latestTemperatureCelsius === 'number')
        .reduce((sum, device, _, list) => sum + (device.latestTemperatureCelsius ?? 0) / list.length, 0)
    : 0
  
  const StatCard = ({ 
    title, 
    value, 
    unit, 
    icon, 
    trend 
  }: { 
    title: string; 
    value: string | number; 
    unit?: string; 
    icon: React.ReactNode; 
    trend?: string;
  }) => (
    <Card sx={{ height: '100%' }}>
      <CardContent>
        <Stack direction="row" sx={{ justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <Stack spacing={1}>
            <Typography variant="overline" sx={{ color: 'text.secondary' }}>
              {title}
            </Typography>
            <Typography variant="h4" component="div">
              {value}{unit}
            </Typography>
            {trend && (
              <Typography variant="caption" sx={{ color: 'success.main' }}>
                {trend}
              </Typography>
            )}
          </Stack>
          <Box sx={{ color: 'primary.main', opacity: 0.7 }}>
            {icon}
          </Box>
        </Stack>
      </CardContent>
    </Card>
  );

  React.useEffect(() => {
    const intervalId = window.setInterval(() => setNowMs(Date.now()), 1000)
    return () => window.clearInterval(intervalId)
  }, [])

  return (
    <Box sx={{ width: '100%', maxWidth: { sm: '100%', md: '1700px' } }}>
      <Typography component="h2" variant="h6" sx={{ mb: 2 }}>
        Overview
      </Typography>
      <Grid
        container
        spacing={{ xs: 2, md: 3 }}
        columns={12}
        sx={{ mb: (theme) => theme.spacing(2) }}
      >
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
          <StatCard
            title="Devices"
            value={devices.length}
            icon={<Thermostat fontSize="large" />}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
          <StatCard
            title="Unregistered"
            value={unregisteredDevices}
            icon={<WarningAmber fontSize="large" />}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
          <StatCard
            title="Reporting"
            value={reportingDevices}
            icon={<Sensors fontSize="large" />}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
          <StatCard
            title="Average Temperature"
            value={registeredDevices.length > 0 ? averageTemperature.toFixed(1) : '--'}
            unit={registeredDevices.length > 0 ? '°C' : undefined}
            icon={<FmdGood fontSize="large" />}
          />
        </Grid>
      </Grid>

      <Grid container spacing={{ xs: 2, md: 3 }} columns={12}>
        <Grid size={{ xs: 12 }}>
          <Card>
            <CardContent>
              <Typography component="h2" variant="h6" sx={{ mb: 2 }}>
                Devices
              </Typography>
              {devices.length > 0 ? (
                <Grid container spacing={2} columns={12}>
                  {devices.map((device) => (
                    <Grid key={device.id} size={{ xs: 12, md: 6, xl: 4 }}>
                      <Card
                        variant="outlined"
                        sx={{
                          height: '100%',
                          borderColor: device.status === 'unregistered' ? 'warning.main' : 'divider',
                        }}
                      >
                        <CardActionArea sx={{ height: '100%' }} onClick={() => onSelectDevice(device)}>
                          <CardContent>
                            <Stack spacing={2}>
                              <Stack direction="row" justifyContent="space-between" alignItems="center" spacing={2}>
                                <Box>
                                  <Typography variant="h6" sx={{ fontWeight: 600 }}>
                                    {device.name || device.deviceId}
                                  </Typography>
                                  <Typography variant="body2" color="text.secondary">
                                    {device.place || 'No place assigned'}
                                  </Typography>
                                </Box>
                                {device.status === 'unregistered' && (
                                  <Chip
                                    label="Unregistered"
                                    color="warning"
                                    size="small"
                                  />
                                )}
                              </Stack>

                              <Stack
                                direction={{ xs: 'column', sm: 'row' }}
                                justifyContent="space-between"
                                alignItems={{ xs: 'flex-start', sm: 'flex-end' }}
                                flexWrap="wrap"
                                gap={2}
                              >
                                <Box>
                                  <Typography variant="overline" color="text.secondary">
                                    Latest Temperature
                                  </Typography>
                                  <Typography variant="h4">
                                    {typeof device.latestTemperatureCelsius === 'number'
                                      ? `${device.latestTemperatureCelsius.toFixed(1)}°C`
                                      : '--'}
                                  </Typography>
                                </Box>
                                <BatteryIndicator
                                  percentage={device.latestBatteryPercentage}
                                  status={device.latestBatteryStatus}
                                  size="compact"
                                />
                                <Box sx={{ textAlign: 'right' }}>
                                  <Typography variant="overline" color="text.secondary">
                                    {device.lastUpdateReceivedAtUtc ? 'Last update' : 'Last discovered'}
                                  </Typography>
                                  <Typography variant="body2" sx={{ fontVariantNumeric: 'tabular-nums' }}>
                                    {formatElapsedTime(
                                      device.lastUpdateReceivedAtUtc || device.lastDiscoveredAtUtc,
                                      nowMs,
                                    )}
                                  </Typography>
                                </Box>
                              </Stack>

                              <Typography variant="caption" color="text.secondary">
                                {device.deviceId}
                              </Typography>
                            </Stack>
                          </CardContent>
                        </CardActionArea>
                      </Card>
                    </Grid>
                  ))}
                </Grid>
              ) : (
                <Typography variant="body2" color="text.secondary">
                  No devices have been discovered yet.
                </Typography>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
}
