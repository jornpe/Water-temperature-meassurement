import * as React from 'react';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import ColorModeIconDropdown from '../shared-theme/ColorModeIconDropdown';

export default function Header() {
  return (
    <Stack
      direction="row"
      sx={{
        display: { xs: 'none', md: 'flex' },
        width: '100%',
        alignItems: { xs: 'flex-start', md: 'center' },
        justifyContent: 'space-between',
        maxWidth: { sm: '100%', md: '1700px' },
        pt: 1.5,
      }}
      spacing={2}
    >
      <Typography variant="h4" component="h1" sx={{ color: 'text.primary' }}>
        Temperature Sensors
      </Typography>
      <ColorModeIconDropdown />
    </Stack>
  );
}
