import { config } from './config'
import type { UpgradeDto, UpsertUpgradeInput } from './types'

const BASE = `${config.apiBaseUrl}/api/upgrades`

/**
 * How long a request may run before the UI admits something is happening.
 *
 * The demo API is on a free tier that spins the container down after 15 minutes
 * of quiet and takes up to a minute to come back. Without this, the first
 * visitor of the day stares at a spinner and concludes the app is broken.
 */
const SLOW_REQUEST_MS = 2_500

/** Long enough for a cold start, short enough to eventually give up. */
const REQUEST_TIMEOUT_MS = 90_000

type SlowListener = (isSlow: boolean) => void

const slowListeners = new Set<SlowListener>()
let inFlightSlowRequests = 0

/** Subscribe to "a request is taking unusually long". Returns an unsubscribe. */
export function onSlowRequest(listener: SlowListener): () => void {
  slowListeners.add(listener)
  return () => {
    slowListeners.delete(listener)
  }
}

function publishSlow(isSlow: boolean) {
  for (const listener of slowListeners) listener(isSlow)
}

/**
 * Turns a failed response into a sentence a person can read.
 *
 * The API answers validation failures with an RFC 9457 ProblemDetails body, so
 * without this the UI would print raw JSON — including field names and type
 * URIs — into an error banner.
 */
async function describeFailure(res: Response): Promise<string> {
  const body = await res.text()

  if (body) {
    try {
      const parsed = JSON.parse(body) as {
        errors?: Record<string, string[]>
        title?: string
        detail?: string
      }

      if (parsed.errors) {
        const messages = Object.values(parsed.errors).flat()
        if (messages.length > 0) return messages.join(' ')
      }

      if (parsed.detail) return parsed.detail
      if (parsed.title) return parsed.title
    } catch {
      // Not JSON. The API also returns bare strings from BadRequest("..."),
      // which are already human-readable.
      return body
    }
  }

  if (res.status === 429) {
    return 'Too many requests in a short time. Wait a moment and try again.'
  }

  return `Request failed with ${res.status}.`
}

async function handle<T>(res: Response): Promise<T> {
  if (!res.ok) throw new Error(await describeFailure(res))
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

/**
 * One place where every network call goes, so the slow-request signal and the
 * timeout cannot be forgotten at an individual call site.
 */
async function request<T>(url: string, init?: RequestInit): Promise<T> {
  inFlightSlowRequests += 1
  const slowTimer = setTimeout(() => publishSlow(true), SLOW_REQUEST_MS)

  try {
    const res = await fetch(url, { ...init, signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS) })
    return await handle<T>(res)
  } catch (err) {
    if (err instanceof DOMException && err.name === 'TimeoutError') {
      throw new Error('The server did not respond in time. It may still be starting up — try again in a moment.')
    }
    if (err instanceof TypeError) {
      // fetch rejects with TypeError for network-level failures, including a
      // CORS rejection, which is the single most likely misconfiguration here.
      throw new Error('Could not reach the API. It may be starting up, or unreachable from this page.')
    }
    throw err
  } finally {
    clearTimeout(slowTimer)
    inFlightSlowRequests -= 1
    if (inFlightSlowRequests === 0) publishSlow(false)
  }
}

const jsonHeaders = { 'Content-Type': 'application/json' }

export function listUpgrades(): Promise<UpgradeDto[]> {
  return request<UpgradeDto[]>(BASE)
}

export function getRecommendations(): Promise<UpgradeDto[]> {
  return request<UpgradeDto[]>(`${BASE}/recommendations`)
}

export function getUpgrade(id: number): Promise<UpgradeDto> {
  return request<UpgradeDto>(`${BASE}/${id}`)
}

export function createUpgrade(input: UpsertUpgradeInput): Promise<UpgradeDto> {
  return request<UpgradeDto>(BASE, {
    method: 'POST',
    headers: jsonHeaders,
    body: JSON.stringify(input),
  })
}

export function updateUpgrade(id: number, input: UpsertUpgradeInput): Promise<UpgradeDto> {
  return request<UpgradeDto>(`${BASE}/${id}`, {
    method: 'PUT',
    headers: jsonHeaders,
    body: JSON.stringify(input),
  })
}

export function deleteUpgrade(id: number): Promise<void> {
  return request<void>(`${BASE}/${id}`, { method: 'DELETE' })
}
