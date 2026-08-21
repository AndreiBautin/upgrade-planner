/**
 * Everything that differs between local development, the public demo, and a
 * production build, read once and in one place.
 *
 * `readConfig` is the browser-side twin of the API's `AppOptions.Parse`: pure,
 * total, and unable to throw. A mistyped environment variable degrades to a
 * documented default instead of producing a blank page, which on a static host
 * is the difference between a working demo and an unexplained white screen.
 */
export interface AppConfig {
  /**
   * Origin the API lives on. Empty string means "same origin", which is what the
   * Vite dev proxy relies on.
   */
  apiBaseUrl: string

  /** Show the "this is a public demo" banner and the cold-start notice. */
  demoMode: boolean

  /**
   * Path the app is served from, always with a leading and trailing slash.
   *
   * This is *derived* from Vite's `BASE_URL`, which Vite computes from the single
   * `base` option in `vite.config.ts`. The bundler's asset paths and the router's
   * basename therefore come from one value. Keeping two copies is how a static
   * deploy ends up with working assets and a 404 on every route.
   */
  basePath: string

  /** Commit this bundle was built from, so a live page can be tied to a commit. */
  buildSha: string
}

/** The shape `readConfig` consumes. Loosened from `ImportMetaEnv` so tests can pass literals. */
export type RawEnv = Record<string, unknown>

const DEFAULT_BASE_PATH = '/'

/**
 * Accepts the spellings a human actually types. Anything unrecognised is `false`,
 * because for every flag here `false` is the conservative direction — a typo must
 * never silently put the app into demo mode.
 */
function readBoolean(raw: unknown): boolean {
  if (typeof raw !== 'string') return false
  switch (raw.trim().toLowerCase()) {
    case 'true':
    case '1':
    case 'yes':
    case 'on':
      return true
    default:
      return false
  }
}

/** Normalises to a leading and trailing slash; anything unusable becomes `/`. */
export function normalizeBasePath(raw: unknown): string {
  if (typeof raw !== 'string') return DEFAULT_BASE_PATH

  const trimmed = raw.trim()
  if (trimmed === '' || trimmed === '/') return DEFAULT_BASE_PATH

  // A full URL is a plausible mistake to make in a deploy variable, and the
  // router needs a path, so keep only the path portion.
  let path = trimmed
  const schemeMatch = /^https?:\/\/[^/]+(\/.*)?$/i.exec(trimmed)
  if (schemeMatch) path = schemeMatch[1] ?? DEFAULT_BASE_PATH

  if (!path.startsWith('/')) path = `/${path}`
  if (!path.endsWith('/')) path = `${path}/`

  // Collapse accidental doubled slashes ("//upgrade-planner//").
  path = path.replace(/\/{2,}/g, '/')

  return path
}

/** Trims a trailing slash so `${apiBaseUrl}/api/upgrades` never doubles up. */
function normalizeApiBaseUrl(raw: unknown): string {
  if (typeof raw !== 'string') return ''
  const trimmed = raw.trim()
  if (trimmed === '') return ''
  return trimmed.replace(/\/+$/, '')
}

export function readConfig(env: RawEnv): AppConfig {
  return {
    apiBaseUrl: normalizeApiBaseUrl(env.VITE_API_BASE_URL),
    demoMode: readBoolean(env.VITE_DEMO_MODE),
    basePath: normalizeBasePath(env.BASE_URL),
    buildSha: typeof env.VITE_BUILD_SHA === 'string' && env.VITE_BUILD_SHA.trim() !== ''
      ? env.VITE_BUILD_SHA.trim()
      : 'dev',
  }
}

/** The configuration this bundle was built with. */
export const config: AppConfig = readConfig(import.meta.env as unknown as RawEnv)
