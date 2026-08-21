import { describe, expect, it } from 'vitest'
import { normalizeBasePath, readConfig } from './config'

/**
 * The base path is the single value that both the bundler's asset URLs and the
 * router's basename are derived from. Getting it wrong on a project page deploy
 * produces the classic static-hosting failure: assets load, every route 404s.
 * So it is tested rather than trusted.
 */
describe('normalizeBasePath', () => {
  it('defaults to root when nothing is set', () => {
    expect(normalizeBasePath(undefined)).toBe('/')
    expect(normalizeBasePath('')).toBe('/')
    expect(normalizeBasePath('   ')).toBe('/')
  })

  it('leaves a well-formed project path alone', () => {
    expect(normalizeBasePath('/upgrade-planner/')).toBe('/upgrade-planner/')
  })

  it('adds the slashes people forget', () => {
    expect(normalizeBasePath('upgrade-planner')).toBe('/upgrade-planner/')
    expect(normalizeBasePath('/upgrade-planner')).toBe('/upgrade-planner/')
    expect(normalizeBasePath('upgrade-planner/')).toBe('/upgrade-planner/')
  })

  it('collapses accidental doubled slashes', () => {
    expect(normalizeBasePath('//upgrade-planner//')).toBe('/upgrade-planner/')
  })

  it('reduces a full URL to its path, because that is a plausible thing to paste', () => {
    expect(normalizeBasePath('https://user.github.io/upgrade-planner/')).toBe('/upgrade-planner/')
    expect(normalizeBasePath('https://user.github.io')).toBe('/')
  })

  it('handles a nested path', () => {
    expect(normalizeBasePath('/a/b/c')).toBe('/a/b/c/')
  })

  it('never throws, whatever it is handed', () => {
    for (const value of [null, undefined, 42, {}, [], true, Symbol('x')]) {
      expect(() => normalizeBasePath(value)).not.toThrow()
      expect(normalizeBasePath(value)).toBe('/')
    }
  })
})

describe('readConfig', () => {
  it('produces a usable configuration from an empty environment', () => {
    const config = readConfig({})

    expect(config.apiBaseUrl).toBe('')
    expect(config.demoMode).toBe(false)
    expect(config.basePath).toBe('/')
    expect(config.buildSha).toBe('dev')
  })

  it('reads demo mode from the spellings people type', () => {
    for (const raw of ['true', 'TRUE', ' True ', '1', 'yes', 'on']) {
      expect(readConfig({ VITE_DEMO_MODE: raw }).demoMode).toBe(true)
    }
  })

  it('treats a typo as demo mode off', () => {
    // False is the conservative direction: an accidental demo banner on the
    // author's private instance would be wrong, and silently switching modes on
    // a misspelling is exactly what config parsing must not do.
    for (const raw of ['ture', 'enabled', 'y', '', 'maybe', 'false', '0', 'off']) {
      expect(readConfig({ VITE_DEMO_MODE: raw }).demoMode).toBe(false)
    }
  })

  it('strips a trailing slash off the API origin so URLs do not double up', () => {
    expect(readConfig({ VITE_API_BASE_URL: 'https://api.example.com/' }).apiBaseUrl)
      .toBe('https://api.example.com')
    expect(readConfig({ VITE_API_BASE_URL: 'https://api.example.com///' }).apiBaseUrl)
      .toBe('https://api.example.com')
  })

  it('keeps an API origin without a trailing slash as-is', () => {
    expect(readConfig({ VITE_API_BASE_URL: 'https://api.example.com' }).apiBaseUrl)
      .toBe('https://api.example.com')
  })

  it('falls back to an empty API origin, meaning same-origin', () => {
    // Empty is what makes the Vite dev proxy work locally.
    expect(readConfig({ VITE_API_BASE_URL: '  ' }).apiBaseUrl).toBe('')
    expect(readConfig({ VITE_API_BASE_URL: 42 }).apiBaseUrl).toBe('')
  })

  it('carries a build sha through when CI injects one', () => {
    expect(readConfig({ VITE_BUILD_SHA: 'abc1234' }).buildSha).toBe('abc1234')
  })

  it('marks an uninjected build as dev rather than empty', () => {
    expect(readConfig({ VITE_BUILD_SHA: '   ' }).buildSha).toBe('dev')
  })

  it('never throws on a hostile environment', () => {
    const hostile = {
      VITE_API_BASE_URL: null,
      VITE_DEMO_MODE: {},
      BASE_URL: [],
      VITE_BUILD_SHA: 0,
    }

    expect(() => readConfig(hostile)).not.toThrow()
  })
})
