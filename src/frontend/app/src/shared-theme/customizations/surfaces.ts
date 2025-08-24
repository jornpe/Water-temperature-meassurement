import { Theme, Components } from '@mui/material/styles';

export const surfacesCustomizations: Components<Theme> = {
  MuiAccordion: {
    styleOverrides: {
      root: ({ theme }) => ({
        border: `1px solid ${theme.palette.divider}`,
        borderRadius: theme.shape.borderRadius,
        '&:before': {
          backgroundColor: 'transparent',
        },
      }),
    },
  },
};
