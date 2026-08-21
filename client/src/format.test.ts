import { describe, expect, it } from 'vitest'
import { formatCost, formatDate } from './format'

describe('formatCost', () => {
  it('formats an amount as whole dollars', () => {
    expect(formatCost(700)).toBe('$700')
    expect(formatCost(1250)).toBe('$1,250')
  })

  it('shows an em dash rather than $0 when no cost is recorded', () => {
    // The demo fixture deliberately contains an upgrade with no cost, because
    // "we do not know yet" and "it is free" must not look the same.
    expect(formatCost(null)).toBe('—')
  })

  it('distinguishes a genuine zero from an absent value', () => {
    expect(formatCost(0)).toBe('$0')
  })
})

describe('formatDate', () => {
  it('formats an ISO timestamp', () => {
    expect(formatDate('2026-03-14T00:00:00Z')).toMatch(/Mar \d{1,2}, 2026/)
  })

  it('shows an em dash for a missing date', () => {
    expect(formatDate(null)).toBe('—')
    expect(formatDate('')).toBe('—')
  })
})
