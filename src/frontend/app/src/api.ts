export interface Temperature {
  id: number
  sensor: string
  celsius: number
  timestamp: string
}

// In Docker/runtime, Nginx proxies /api to API_BASE_URL. In dev, Vite proxy handles /api.
const base = ''

export async function getTemperatures(): Promise<Temperature[]> {
  const res = await fetch(`${base}/api/temperatures`)
  if (!res.ok) throw new Error(`API ${res.status}`)
  return res.json()
}
