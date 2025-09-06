import * as React from 'react';
import type {} from '@mui/x-date-pickers/themeAugmentation';
import type {} from '@mui/x-charts/themeAugmentation';
// import type {} from '@mui/x-data-grid-pro/themeAugmentation';
import type {} from '@mui/x-tree-view/themeAugmentation';
import { alpha } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import AppNavbar from './AppNavbar';
import SideMenu from './SideMenu';
import AppTheme from '../shared-theme/AppTheme';

interface MainLayoutProps {
  children: React.ReactNode;
}

export default function MainLayout({ children }: MainLayoutProps) {
  return (
    <AppTheme>
      <CssBaseline enableColorScheme />
      <Box sx={{ display: 'flex', minHeight: '100vh' }}>
        <SideMenu />
        <AppNavbar />
        {/* Main content */}
        <Box
          component="main"
          sx={(theme) => ({
            flexGrow: 1,
            backgroundColor: alpha(theme.palette.background.default, 1),
            overflow: 'auto',
            minHeight: '100vh',
            // Account for AppNavbar on mobile
            marginTop: { xs: '64px', md: 0 },
            width: { xs: '100%', md: 'calc(100% - 240px)' },
          })}
        >
          <Stack
            spacing={2}
            sx={{
              alignItems: 'stretch',
              mx: { xs: 2, sm: 3, md: 3 },
              py: { xs: 2, sm: 3, md: 3 },
              minHeight: { xs: 'calc(100vh - 64px)', md: '100vh' },
            }}
          >
            {children}
          </Stack>
        </Box>
      </Box>
    </AppTheme>
  );
}
