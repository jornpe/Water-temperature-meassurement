import { Theme, Components } from '@mui/material/styles';
import { alpha } from '@mui/material/styles';

export const navigationCustomizations: Components<Theme> = {
  MuiDrawer: {
    styleOverrides: {
      paper: ({ theme }) => ({
        backgroundColor: theme.palette.background.paper,
      }),
    },
  },
  MuiListItemButton: {
    styleOverrides: {
      root: ({ theme }) => ({
        borderRadius: theme.shape.borderRadius,
        padding: '8px 12px',
        '&.Mui-selected': {
          backgroundColor: alpha(theme.palette.primary.main, 0.1),
          '&:hover': {
            backgroundColor: alpha(theme.palette.primary.main, 0.15),
          },
        },
        '&:hover': {
          backgroundColor: alpha(theme.palette.action.hover, 0.5),
        },
      }),
    },
  },
};
