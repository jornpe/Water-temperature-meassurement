import { Theme, Components } from '@mui/material/styles';

export const dataDisplayCustomizations: Components<Theme> = {
  MuiCard: {
    styleOverrides: {
      root: ({ theme }) => {
        const palette = theme.vars ?? theme;

        return {
          borderRadius: theme.shape.borderRadius,
          border: `1px solid ${palette.palette.divider}`,
          boxShadow: 'none',
          backgroundImage: 'none',
          backgroundColor: palette.palette.background.paper,
        };
      },
    },
  },
};
