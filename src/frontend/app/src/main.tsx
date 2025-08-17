import React from 'react'
import ReactDOM from 'react-dom/client'
import { createBrowserRouter, RouterProvider, Navigate } from 'react-router-dom'
import { ThemeProvider, CssBaseline, createTheme, Container } from '@mui/material'
import App from './App'
import Login from './pages/Login'
import Register from './pages/Register'

const theme = createTheme({
  palette: { mode: 'light', primary: { main: '#0ea5e9' } },
})

const router = createBrowserRouter([
  {
    path: '/',
    element: <App />,
  },
  { path: '/login', element: <Login /> },
  { path: '/register', element: <Register /> },
  { path: '*', element: <Navigate to="/" replace /> },
])

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <Container maxWidth="md" sx={{ py: 4 }}>
        <RouterProvider router={router} />
      </Container>
    </ThemeProvider>
  </React.StrictMode>,
)
