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
  const res = await fetch(`${base}/api/temperatures`, {
    headers: authHeader(),
  })
  if (!res.ok) throw new Error(`API ${res.status}`)
  return res.json()
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
  })
  if (!res.ok) throw new Error('Invalid credentials')
  const data = await res.json()
  localStorage.setItem('token', data.token)
  localStorage.setItem('user', JSON.stringify(data.profile))
  return data.profile
}

export async function getUserProfile(): Promise<UserProfile> {
  const res = await fetch(`${base}/api/auth/profile`, {
    headers: authHeader(),
  })
  if (!res.ok) throw new Error(`Failed to get profile ${res.status}`)
  const profile = await res.json()
  localStorage.setItem('user', JSON.stringify(profile))
  return profile
}

export async function updateProfile(data: UpdateProfileData): Promise<UserProfile> {
  const res = await fetch(`${base}/api/auth/profile`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', ...authHeader() },
    body: JSON.stringify(data),
  })
  if (!res.ok) {
    const errorData = await res.json().catch(() => ({ message: `Update failed ${res.status}` }))
    throw new Error(errorData.message || `Update failed ${res.status}`)
  }
  const profile = await res.json()
  localStorage.setItem('user', JSON.stringify(profile))
  return profile
}

export async function changePassword(data: ChangePasswordData): Promise<void> {
  const res = await fetch(`${base}/api/auth/profile/change-password`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...authHeader() },
    body: JSON.stringify(data),
  })
  if (!res.ok) {
    const errorData = await res.json().catch(() => ({ message: `Password change failed ${res.status}` }))
    throw new Error(errorData.message || `Password change failed ${res.status}`)
  }
}

export async function uploadProfilePicture(file: File): Promise<UserProfile> {
  const formData = new FormData()
  formData.append('picture', file)
  
  const res = await fetch(`${base}/api/auth/profile/picture`, {
    method: 'POST',
    headers: authHeader(),
    body: formData,
  })
  if (!res.ok) {
    const errorData = await res.json().catch(() => ({ message: `Upload failed ${res.status}` }))
    throw new Error(errorData.message || `Upload failed ${res.status}`)
  }
  const profile = await res.json()
  localStorage.setItem('user', JSON.stringify(profile))
  return profile
}

export async function deleteProfilePicture(): Promise<UserProfile> {
  const res = await fetch(`${base}/api/auth/profile/picture`, {
    method: 'DELETE',
    headers: authHeader(),
  })
  if (!res.ok) {
    const errorData = await res.json().catch(() => ({ message: `Delete failed ${res.status}` }))
    throw new Error(errorData.message || `Delete failed ${res.status}`)
  }
  const profile = await res.json()
  localStorage.setItem('user', JSON.stringify(profile))
  return profile
}

export function getCurrentUser(): UserProfile | null {
  const userJson = localStorage.getItem('user')
  return userJson ? JSON.parse(userJson) : null
}

export function logout() {
  localStorage.removeItem('token')
  localStorage.removeItem('user')
}

export function authHeader(): Record<string, string> {
  const t = localStorage.getItem('token')
  return t ? { Authorization: `Bearer ${t}` } : {}
}

export function getProfilePictureUrl(userId: number): string {
  const baseUrl = import.meta.env.VITE_API_BASE_URL || ''
  return `${baseUrl}/api/auth/profile/picture/${userId}`
}
