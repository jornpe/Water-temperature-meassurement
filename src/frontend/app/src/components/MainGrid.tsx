import * as React from 'react';
import Grid from '@mui/material/Grid2';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Stack from '@mui/material/Stack';
import { Thermostat, Sensors, TrendingUp } from '@mui/icons-material';

interface MainGridProps {
  temperatures: Array<{
    id: number;
    value: number;
    timestamp: string;
    sensor?: string;
  }>;
}

export default function MainGrid({ temperatures = [] }: MainGridProps) {
  const latestTemp = temperatures.length > 0 ? temperatures[temperatures.length - 1] : null;
  const avgTemp = temperatures.length > 0 
    ? temperatures.reduce((sum, temp) => sum + temp.value, 0) / temperatures.length 
    : 0;
  
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

  return (
    <Box sx={{ width: '100%', maxWidth: { sm: '100%', md: '1700px' } }}>
      <Typography component="h2" variant="h6" sx={{ mb: 2 }}>
        Overview
      </Typography>
      <Grid
        container
        spacing={2}
        columns={12}
        sx={{ mb: (theme) => theme.spacing(2) }}
      >
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
          <StatCard
            title="Current Temperature"
            value={latestTemp?.value.toFixed(1) || '--'}
            unit="°C"
            icon={<Thermostat fontSize="large" />}
            trend="+2.5% from yesterday"
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
          <StatCard
            title="Average Temperature"
            value={avgTemp.toFixed(1)}
            unit="°C"
            icon={<TrendingUp fontSize="large" />}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
          <StatCard
            title="Active Sensors"
            value={temperatures.length > 0 ? 1 : 0}
            icon={<Sensors fontSize="large" />}
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}>
          <StatCard
            title="Total Readings"
            value={temperatures.length}
            icon={<Thermostat fontSize="large" />}
            trend="+12% this week"
          />
        </Grid>
      </Grid>

      <Grid container spacing={2} columns={12}>
        <Grid size={{ xs: 12 }}>
          <Card>
            <CardContent>
              <Typography component="h2" variant="h6" sx={{ mb: 2 }}>
                Recent Temperature Readings
              </Typography>
              {temperatures.length > 0 ? (
                <Stack spacing={2}>
                  {temperatures.slice(-10).map((temp) => (
                    <Box
                      key={temp.id}
                      sx={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        p: 2,
                        border: 1,
                        borderColor: 'divider',
                        borderRadius: 1,
                      }}
                    >
                      <Typography variant="body1">
                        {new Date(temp.timestamp).toLocaleString()}
                      </Typography>
                      <Typography variant="h6" color="primary">
                        {temp.value.toFixed(1)}°C
                      </Typography>
                    </Box>
                  ))}
                </Stack>
              ) : (
                <Typography variant="body2" color="text.secondary">
                  No temperature data available yet.
                </Typography>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
}
