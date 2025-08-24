import { Theme, Components } from '@mui/material/styles';

export const dataDisplayCustomizations: Components<Theme> = {
  MuiCard: {
    styleOverrides: {
      root: ({ theme }) => ({
        borderRadius: theme.shape.borderRadius,
        border: `1px solid ${theme.palette.divider}`,
        boxShadow: 'none',
        backgroundImage: 'none',
        backgroundColor: theme.palette.background.paper,
      }),
    },
  },
};
