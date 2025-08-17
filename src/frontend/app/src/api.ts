export interface Temperature {
  id: number
  sensor: string
  celsius: number
  timestamp: string
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

export async function registerAdmin(userName: string, password: string): Promise<void> {
  const res = await fetch(`${base}/api/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userName, password }),
  })
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(text || `Register failed ${res.status}`)
  }
}

export async function login(userName: string, password: string): Promise<void> {
  const res = await fetch(`${base}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ userName, password }),
  })
  if (!res.ok) throw new Error('Invalid credentials')
  const data = await res.json()
  localStorage.setItem('token', data.token)
}

export function logout() {
  localStorage.removeItem('token')
}

export function authHeader(): Record<string, string> {
  const t = localStorage.getItem('token')
  return t ? { Authorization: `Bearer ${t}` } : {}
}
