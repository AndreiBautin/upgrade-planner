import type { UpgradeDto, UpsertUpgradeInput } from './types'

const API_ORIGIN = import.meta.env.VITE_API_BASE_URL ?? ''
const BASE = `${API_ORIGIN}/api/upgrades`

async function handle<T>(res: Response): Promise<T> {
  if (!res.ok) {
    const text = await res.text()
    throw new Error(text || `Request failed with ${res.status}`)
  }
  if (res.status === 204) return undefined as T
  return res.json() as Promise<T>
}

export function listUpgrades(): Promise<UpgradeDto[]> {
  return fetch(BASE).then((r) => handle(r))
}

export function getRecommendations(): Promise<UpgradeDto[]> {
  return fetch(`${BASE}/recommendations`).then((r) => handle(r))
}

export function getUpgrade(id: number): Promise<UpgradeDto> {
  return fetch(`${BASE}/${id}`).then((r) => handle(r))
}

export function createUpgrade(input: UpsertUpgradeInput): Promise<UpgradeDto> {
  return fetch(BASE, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  }).then((r) => handle(r))
}

export function updateUpgrade(id: number, input: UpsertUpgradeInput): Promise<UpgradeDto> {
  return fetch(`${BASE}/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  }).then((r) => handle(r))
}

export function deleteUpgrade(id: number): Promise<void> {
  return fetch(`${BASE}/${id}`, { method: 'DELETE' }).then((r) => handle(r))
}
