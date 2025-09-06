import * as React from 'react';
import { styled } from '@mui/material/styles';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import MuiToolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import MenuRoundedIcon from '@mui/icons-material/MenuRounded';
import ThermostatIcon from '@mui/icons-material/Thermostat';
import SideMenuMobile from './SideMenuMobile';
import MenuButton from './MenuButton';
import ColorModeIconDropdown from '../shared-theme/ColorModeIconDropdown';

const Toolbar = styled(MuiToolbar)({
  width: '100%',
  padding: '8px 16px',
  display: 'flex',
  flexDirection: 'row',
  alignItems: 'center',
  justifyContent: 'space-between',
  minHeight: '64px',
  flexShrink: 0,
});

export default function AppNavbar() {
  const [open, setOpen] = React.useState(false);

  const toggleDrawer = (newOpen: boolean) => () => {
    setOpen(newOpen);
  };

  return (
    <AppBar
      position="fixed"
      sx={{
        display: { xs: 'flex', md: 'none' },
        boxShadow: 0,
        bgcolor: 'background.paper',
        backgroundImage: 'none',
        borderBottom: '1px solid',
        borderColor: 'divider',
        zIndex: (theme) => theme.zIndex.drawer + 1,
      }}
    >
      <Toolbar variant="regular">
        <Stack
          direction="row"
          spacing={1}
          sx={{ 
            alignItems: 'center',
            flex: 1,
          }}
        >
          <CustomIcon />
          <Typography 
            variant="h6" 
            component="h1" 
            sx={{ 
              color: 'text.primary',
              fontWeight: 600,
              fontSize: { xs: '1.1rem', sm: '1.25rem' },
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            Water Temp Monitor
          </Typography>
        </Stack>
        
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <ColorModeIconDropdown />
          <MenuButton 
            aria-label="Open navigation menu" 
            onClick={toggleDrawer(true)}
            sx={{
              minWidth: '44px',
              minHeight: '44px',
            }}
          >
            <MenuRoundedIcon />
          </MenuButton>
        </Stack>
        
        <SideMenuMobile open={open} toggleDrawer={toggleDrawer} />
      </Toolbar>
    </AppBar>
  );
}

export function CustomIcon() {
  return (
    <Box
      sx={{
        width: '2rem',
        height: '2rem',
        bgcolor: 'primary.main',
        borderRadius: '8px',
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        color: 'primary.contrastText',
        boxShadow: 1,
      }}
    >
      <ThermostatIcon sx={{ fontSize: '1.25rem' }} />
    </Box>
  );
}
