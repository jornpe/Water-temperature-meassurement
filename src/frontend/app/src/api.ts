export interface Temperature {
  id: number
  sensor: string
  celsius: number
  timestamp: string
}

const base = import.meta.env.VITE_API_BASE_URL || ''

export async function getTemperatures(): Promise<Temperature[]> {
  const res = await fetch(`${base}/api/temperatures`)
  if (!res.ok) throw new Error(`API ${res.status}`)
  return res.json()
}
