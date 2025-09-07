import { authenticatedFetch, setAccessToken, logoutUser, getAuthHeader } from './utils/apiClient'

export interface Temperature {
  id: number
  sensor: string
  celsius: number
  timestamp: string
}

export interface UserProfile {
  id: number
  userName: string
  email?: string
  firstName?: string
  lastName?: string
  hasProfilePicture: boolean
  createdAt: string
}

export interface RegisterData {
  userName: string
  password: string
  email?: string
  firstName?: string
  lastName?: string
}

export interface UpdateProfileData {
  email?: string
  firstName?: string
  lastName?: string
}

export interface ChangePasswordData {
  currentPassword: string
  newPassword: string
}

// In Docker/runtime, Nginx proxies /api to API_BASE_URL. In dev, Vite proxy handles /api.
const base = ''

export async function getTemperatures(): Promise<Temperature[]> {
  return authenticatedFetch(`${base}/api/temperatures`)
}

export async function usersExist(): Promise<boolean> {
  const res = await fetch(`${base}/api/auth/users/exists`)
  if (!res.ok) throw new Error(`API ${res.status}`)
  const data = await res.json()
  return Boolean(data.exists)
}

export async function registerAdmin(data: RegisterData): Promise<void> {
  const res = await fetch(`${base}/api/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  })
  if (!res.ok) {
    const errorData = await res.json().catch(() => ({ message: `Register failed ${res.status}` }))
    throw new Error(errorData.message || `Register failed ${res.status}`)
  }
}

export async function login(userName: string, password: string): Promise<UserProfile> {
  const res = await fetch(`${base}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userName, password }),
    credentials: 'include', // Include cookies for refresh token
  })
  if (!res.ok) throw new Error('Invalid credentials')
  const data = await res.json()
  
  // Store the access token in memory and user profile in localStorage
  setAccessToken(data.token)
  localStorage.setItem('user', JSON.stringify(data.profile))
  return data.profile
}

export async function getUserProfile(): Promise<UserProfile> {
  const profile = await authenticatedFetch(`${base}/api/auth/profile`)
  localStorage.setItem('user', JSON.stringify(profile))
  return profile
}

export async function updateProfile(data: UpdateProfileData): Promise<UserProfile> {
  const profile = await authenticatedFetch(`${base}/api/auth/profile`, {
    method: 'PUT',
    body: JSON.stringify(data),
  })
  localStorage.setItem('user', JSON.stringify(profile))
  return profile
}

export async function changePassword(data: ChangePasswordData): Promise<void> {
  await authenticatedFetch(`${base}/api/auth/profile/change-password`, {
    method: 'POST',
    body: JSON.stringify(data),
  })
}

export async function uploadProfilePicture(file: File): Promise<UserProfile> {
  const formData = new FormData()
  formData.append('picture', file)
  
  const profile = await authenticatedFetch(`${base}/api/auth/profile/picture`, {
    method: 'POST',
    body: formData,
  })
  localStorage.setItem('user', JSON.stringify(profile))
  return profile
}

export async function deleteProfilePicture(): Promise<UserProfile> {
  const profile = await authenticatedFetch(`${base}/api/auth/profile/picture`, {
    method: 'DELETE',
  })
  localStorage.setItem('user', JSON.stringify(profile))
  return profile
}

export function getCurrentUser(): UserProfile | null {
  const userJson = localStorage.getItem('user')
  return userJson ? JSON.parse(userJson) : null
}

export function logout() {
  logoutUser()
}

export function authHeader(): Record<string, string> {
  return getAuthHeader()
}

export function getProfilePictureUrl(userId: number): string {
  const baseUrl = import.meta.env.VITE_API_BASE_URL || ''
  return `${baseUrl}/api/auth/profile/picture/${userId}`
}
