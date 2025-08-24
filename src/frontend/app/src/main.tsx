import React from 'react'
import ReactDOM from 'react-dom/client'
import { createBrowserRouter, RouterProvider, Navigate } from 'react-router-dom'
import MainLayout from './components/MainLayout'
import Sensors from './pages/Sensors'
import Login from './pages/Login'
import Register from './pages/Register'
import Profile from './pages/Profile'

const router = createBrowserRouter([
  {
    path: '/',
    element: (
      <MainLayout>
        <Sensors />
      </MainLayout>
    ),
  },
  { path: '/login', element: <Login /> },
  { path: '/register', element: <Register /> },
  { 
    path: '/profile', 
    element: (
      <MainLayout>
        <Profile />
      </MainLayout>
    )
  },
  { path: '*', element: <Navigate to="/" replace /> },
])

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <RouterProvider router={router} />
  </React.StrictMode>,
)
