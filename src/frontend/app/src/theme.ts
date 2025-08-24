import { createTheme } from '@mui/material/styles';

// Create theme with dark mode as default and light mode support
export const theme = createTheme({
  // Enable CSS variables and color schemes
  cssVariables: true,
  colorSchemes: {
    light: {
      palette: {
        primary: {
          main: '#0ea5e9',
        },
        secondary: {
          main: '#64748b',
        },
        background: {
          default: '#ffffff',
          paper: '#f8fafc',
        },
      },
    },
    dark: {
      palette: {
        primary: {
          main: '#0ea5e9',
        },
        secondary: {
          main: '#94a3b8',
        },
        background: {
          default: '#0f172a',
          paper: '#1e293b',
        },
        text: {
          primary: '#f1f5f9',
          secondary: '#cbd5e1',
        },
      },
    },
  },
  // Set dark as default
  defaultColorScheme: 'dark',
  
  // Typography configuration
  typography: {
    fontFamily: '"Roboto", "Helvetica", "Arial", sans-serif',
    h1: {
      fontSize: '2.5rem',
      fontWeight: 600,
    },
    h2: {
      fontSize: '2rem',
      fontWeight: 600,
    },
    h3: {
      fontSize: '1.75rem',
      fontWeight: 600,
    },
    h4: {
      fontSize: '1.5rem',
      fontWeight: 600,
    },
    h5: {
      fontSize: '1.25rem',
      fontWeight: 600,
    },
    h6: {
      fontSize: '1rem',
      fontWeight: 600,
    },
  },
  
  // Shape configuration for consistent border radius
  shape: {
    borderRadius: 8,
  },
  
  // Component overrides for better theming
  components: {
    MuiButton: {
      styleOverrides: {
        root: {
          textTransform: 'none',
          fontWeight: 600,
        },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          boxShadow: '0 1px 3px 0 rgb(0 0 0 / 0.1), 0 1px 2px -1px rgb(0 0 0 / 0.1)',
        },
      },
    },
    MuiTextField: {
      styleOverrides: {
        root: {
          '& .MuiOutlinedInput-root': {
            borderRadius: 8,
          },
        },
      },
    },
  },
});
