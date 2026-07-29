export function formatElapsedTime(value?: string | null, nowMs = Date.now()) {
  if (!value) {
    return 'No updates yet'
  }

  const timestampMs = Date.parse(value)
  if (!Number.isFinite(timestampMs)) {
    return 'No updates yet'
  }

  if (timestampMs > nowMs) {
    return 'Just now'
  }

  const elapsedSeconds = Math.floor((nowMs - timestampMs) / 1000)
  const days = Math.floor(elapsedSeconds / 86_400)
  if (days >= 1) {
    return `${days} ${days === 1 ? 'day' : 'days'} ago`
  }

  const hours = Math.floor(elapsedSeconds / 3_600)
  const minutes = Math.floor((elapsedSeconds % 3_600) / 60)
  const seconds = elapsedSeconds % 60

  if (hours > 0) {
    return `${hours}h ${String(minutes).padStart(2, '0')}m ${String(seconds).padStart(2, '0')}s ago`
  }

  if (minutes > 0) {
    return `${minutes}m ${String(seconds).padStart(2, '0')}s ago`
  }

  return `${seconds}s ago`
}
