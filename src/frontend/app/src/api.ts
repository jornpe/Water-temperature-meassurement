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

export interface DeviceConfiguration {
  reportIntervalSeconds: number
  desiredConfigurationVersion: number
}

export interface DeviceDesiredConfiguration {
  version: number
  reportIntervalSeconds: number
  updatedAtUtc?: string | null
}

export interface DeviceRuntimeConfiguration {
  appliedConfigurationVersion?: number | null
  appliedReportIntervalSeconds?: number | null
  reportedAtUtc?: string | null
}

export interface DeviceSummary {
  id: number
  deviceId: string
  status: 'registered' | 'unregistered'
  name?: string | null
  place?: string | null
  pushToHomeAssistant: boolean
  latestTemperatureCelsius?: number | null
  latestBatteryPercentage?: number | null
  latestBatteryStatus?: string | null
  latestBatteryAtUtc?: string | null
  lastUpdateReceivedAtUtc?: string | null
  lastDiscoveredAtUtc?: string | null
}

export interface DevicePositionSnapshot {
  latitude?: number | null
  longitude?: number | null
  altitudeMeters?: number | null
  gpsTimeUtc?: string | null
  speedKnots?: number | null
  hdop?: number | null
  satellitesVisible?: number | null
  satellitesUsed?: number | null
  recordedAtUtc?: string | null
}

export interface DevicePositionHistoryPoint {
  id: number
  latitude: number
  longitude: number
  altitudeMeters?: number | null
  gpsTimeUtc?: string | null
  speedKnots?: number | null
  hdop?: number | null
  satellitesVisible?: number | null
  satellitesUsed?: number | null
  recordedAtUtc: string
}

export interface DevicePositionHistoryResponse {
  deviceId: number
  deviceIdentifier: string
  fromUtc?: string | null
  toUtc?: string | null
  totalCount: number
  isSampled: boolean
  items: DevicePositionHistoryPoint[]
}

export interface DeviceTemperatureHistoryPoint {
  id: number
  temperatureCelsius: number
  recordedAtUtc: string
}

export interface DeviceTemperatureHistoryResponse {
  deviceId: number
  deviceIdentifier: string
  fromUtc?: string | null
  toUtc?: string | null
  totalCount: number
  isSampled: boolean
  items: DeviceTemperatureHistoryPoint[]
}

export interface DeviceWifiDiagnostics {
  localIp?: string | null
  wifiRssiDbm?: number | null
  ssid?: string | null
  bssid?: string | null
  channel?: number | null
  gatewayIp?: string | null
  subnetMask?: string | null
  dnsIp?: string | null
  macAddress?: string | null
}

export interface DeviceCellularDiagnostics {
  localIp?: string | null
  simStatus?: string | null
  networkConnected?: boolean | null
  gprsConnected?: boolean | null
  operator?: string | null
  signalQuality?: number | null
}

export interface DeviceNetworkDiagnostics {
  transport?: string | null
  wifi?: DeviceWifiDiagnostics | null
  cellular?: DeviceCellularDiagnostics | null
}

export type BatteryState = 'Unknown' | 'NotCharging' | 'Charging' | 'Full'

export interface DeviceBatteryDiagnostics {
  modemReadingValid?: boolean | null
  chargeState?: number | null
  batteryState?: BatteryState | null
  percentage?: number | null
  modemMillivolts?: number | null
  adcVoltage?: number | null
  recordedAtUtc?: string | null
}

export interface DeviceBatteryHistoryPoint {
  id: number
  modemReadingValid: boolean
  chargeState: number
  batteryState: BatteryState | number
  percentage: number
  modemMillivolts: number
  adcVoltage: number
  recordedAtUtc: string
}

export interface DeviceBatteryHistoryResponse {
  deviceId: number
  deviceIdentifier: string
  fromUtc?: string | null
  toUtc?: string | null
  totalCount: number
  isSampled: boolean
  items: DeviceBatteryHistoryPoint[]
}

export interface TelemetryHistoryQuery {
  fromUtc?: string
  toUtc?: string
  maxPoints?: number
}

export interface DeviceDetail {
  id: number
  deviceId: string
  status: 'registered' | 'unregistered'
  name?: string | null
  place?: string | null
  pushToHomeAssistant: boolean
  homeAssistantDeviceName: string
  firmwareVersion?: string | null
  reportIntervalSeconds: number
  createdAtUtc: string
  registeredAtUtc?: string | null
  lastDiscoveredAtUtc?: string | null
  lastSeenAtUtc?: string | null
  lastUpdateReceivedAtUtc?: string | null
  latestTemperatureCelsius?: number | null
  latestTemperatureAtUtc?: string | null
  desiredConfiguration: DeviceDesiredConfiguration
  runtimeConfiguration: DeviceRuntimeConfiguration
  hasPendingConfiguration: boolean
  position: DevicePositionSnapshot
  networkDiagnostics: DeviceNetworkDiagnostics
  battery: DeviceBatteryDiagnostics
  temperatureHistoryCount: number
  positionHistoryCount: number
}

export interface RegisterDeviceData {
  name: string
  place: string
  reportIntervalSeconds: number
  pushToHomeAssistant: boolean
  homeAssistantDeviceName?: string | null
}

export interface DeviceRegistrationResponse {
  id: number
  deviceId: string
  status: string
  configuration: DeviceConfiguration
  apiKeyIssuedAtUtc: string
}

export interface DeviceKeyRegenerationResponse {
  id: number
  deviceId: string
  apiKeyIssuedAtUtc: string
  configuration: DeviceConfiguration
}

export interface UpdateRegisteredDeviceData {
  name: string
  place: string
  reportIntervalSeconds: number
  pushToHomeAssistant: boolean
  homeAssistantDeviceName?: string | null
}

export interface HomeAssistantSettings {
  pushDataToHomeAssistant: boolean
  ipAddress?: string | null
  port: number
  user?: string | null
  password?: string | null
  updatedAtUtc?: string | null
}

export interface UpdateHomeAssistantSettingsData {
  pushDataToHomeAssistant: boolean
  ipAddress?: string | null
  port: number
  user?: string | null
  password?: string | null
}

export interface DeviceTelemetryClearResponse {
  id: number
  deviceId: string
  category: string
  deletedCount: number
}

export interface DeviceDeleteResponse {
  id: number
  deviceId: string
  message: string
}

export interface DeviceLogEntry {
  id: number
  level?: string | null
  message: string
  timestampUtc: string
}

export interface DeviceLogsResponse {
  deviceId: number
  deviceIdentifier: string
  page: number
  pageSize: number
  totalCount?: number | null
  hasMore: boolean
  nextBeforeId?: number | null
  items: DeviceLogEntry[]
}

export interface DeviceLogQuery {
  pageSize?: number
  search?: string
  level?: string
  fromUtc?: string
  toUtc?: string
  beforeId?: number
  afterId?: number
  includeTotalCount?: boolean
}

export interface DeviceLogsDeleteResponse {
  id: number
  deviceId: string
  deletedBeforeUtc?: string | null
  deletedCount: number
}

// In Docker/runtime, Nginx proxies /api to API_BASE_URL. In dev, Vite proxy handles /api.
const base = ''

export async function getTemperatures(): Promise<Temperature[]> {
  return authenticatedFetch(`${base}/api/temperatures`)
}

export async function getDevices(): Promise<DeviceSummary[]> {
  return authenticatedFetch(`${base}/api/devices`)
}

export async function getDevice(id: number): Promise<DeviceDetail> {
  return authenticatedFetch(`${base}/api/devices/${id}`)
}

export async function getDevicePositions(
  id: number,
  options: TelemetryHistoryQuery = {},
): Promise<DevicePositionHistoryResponse> {
  const query = new URLSearchParams()
  if (options.fromUtc) query.set('fromUtc', options.fromUtc)
  if (options.toUtc) query.set('toUtc', options.toUtc)
  if (options.maxPoints !== undefined) query.set('maxPoints', String(options.maxPoints))
  const suffix = query.size > 0 ? `?${query.toString()}` : ''

  return authenticatedFetch(`${base}/api/devices/${id}/positions${suffix}`)
}

export async function getDeviceBatteryHistory(
  id: number,
  options: TelemetryHistoryQuery = {},
): Promise<DeviceBatteryHistoryResponse> {
  const query = new URLSearchParams()
  if (options.fromUtc) query.set('fromUtc', options.fromUtc)
  if (options.toUtc) query.set('toUtc', options.toUtc)
  if (options.maxPoints !== undefined) query.set('maxPoints', String(options.maxPoints))
  const suffix = query.size > 0 ? `?${query.toString()}` : ''

  return authenticatedFetch(`${base}/api/devices/${id}/battery-history${suffix}`)
}

export async function getDeviceTemperatureHistory(
  id: number,
  options: TelemetryHistoryQuery = {},
): Promise<DeviceTemperatureHistoryResponse> {
  const query = new URLSearchParams()
  if (options.fromUtc) query.set('fromUtc', options.fromUtc)
  if (options.toUtc) query.set('toUtc', options.toUtc)
  if (options.maxPoints !== undefined) query.set('maxPoints', String(options.maxPoints))
  const suffix = query.size > 0 ? `?${query.toString()}` : ''

  return authenticatedFetch(`${base}/api/devices/${id}/temperature-history${suffix}`)
}

export async function getDeviceLogs(id: number, options: DeviceLogQuery = {}): Promise<DeviceLogsResponse> {
  const query = new URLSearchParams({ pageSize: String(options.pageSize ?? 100) })

  if (options.search?.trim()) query.set('search', options.search.trim())
  if (options.level?.trim()) query.set('level', options.level.trim())
  if (options.fromUtc) query.set('fromUtc', options.fromUtc)
  if (options.toUtc) query.set('toUtc', options.toUtc)
  if (options.beforeId !== undefined) query.set('beforeId', String(options.beforeId))
  if (options.afterId !== undefined) query.set('afterId', String(options.afterId))
  if (options.includeTotalCount !== undefined) query.set('includeTotalCount', String(options.includeTotalCount))

  return authenticatedFetch(`${base}/api/devices/${id}/logs?${query.toString()}`)
}

export async function deleteDeviceLogs(id: number, beforeUtc?: string): Promise<DeviceLogsDeleteResponse> {
  const query = new URLSearchParams()
  if (beforeUtc) query.set('beforeUtc', beforeUtc)
  const suffix = query.size > 0 ? `?${query.toString()}` : ''

  return authenticatedFetch(`${base}/api/devices/${id}/logs${suffix}`, {
    method: 'DELETE',
  })
}

export async function registerDevice(id: number, data: RegisterDeviceData): Promise<DeviceRegistrationResponse> {
  return authenticatedFetch(`${base}/api/devices/${id}/register`, {
    method: 'POST',
    body: JSON.stringify(data),
  })
}

export async function updateDevice(id: number, data: UpdateRegisteredDeviceData): Promise<DeviceDetail> {
  return authenticatedFetch(`${base}/api/devices/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  })
}

export async function regenerateDeviceKey(id: number): Promise<DeviceKeyRegenerationResponse> {
  return authenticatedFetch(`${base}/api/devices/${id}/regenerate-key`, {
    method: 'POST',
  })
}

export async function clearDeviceTelemetry(id: number, category: 'temperature' | 'position'): Promise<DeviceTelemetryClearResponse> {
  return authenticatedFetch(`${base}/api/devices/${id}/telemetry/${category}`, {
    method: 'DELETE',
  })
}

export async function deleteDevice(id: number): Promise<DeviceDeleteResponse> {
  return authenticatedFetch(`${base}/api/devices/${id}`, {
    method: 'DELETE',
  })
}

export async function getHomeAssistantSettings(): Promise<HomeAssistantSettings> {
  return authenticatedFetch(`${base}/api/settings/home-assistant`)
}

export async function updateHomeAssistantSettings(data: UpdateHomeAssistantSettingsData): Promise<HomeAssistantSettings> {
  return authenticatedFetch(`${base}/api/settings/home-assistant`, {
    method: 'PUT',
    body: JSON.stringify(data),
  })
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
