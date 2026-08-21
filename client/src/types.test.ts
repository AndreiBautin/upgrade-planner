import { describe, expect, it } from 'vitest'
import {
  categoryLabel,
  statusLabel,
  toUpsertInput,
  UpgradeCategory,
  UpgradeStatus,
  withStatus,
  type UpgradeDto,
  type UpsertUpgradeInput,
} from './types'

function input(overrides: Partial<UpsertUpgradeInput> = {}): UpsertUpgradeInput {
  return {
    title: 'Thing',
    description: null,
    category: UpgradeCategory.Home,
    priority: 50,
    estimatedCost: 100,
    status: UpgradeStatus.Purchased,
    notes: null,
    productLink: null,
    prerequisiteUpgradeId: null,
    purchasedDate: '2026-01-01T00:00:00Z',
    actualCost: 95,
    ...overrides,
  }
}

describe('withStatus', () => {
  it('clears purchase details when moving away from Purchased', () => {
    // The API rejects a purchase date on a non-purchased upgrade. The inputs
    // holding those values are hidden at that point, so a user could not see
    // what the resulting validation error referred to.
    const result = withStatus(input(), UpgradeStatus.Idea)

    expect(result.status).toBe(UpgradeStatus.Idea)
    expect(result.purchasedDate).toBeNull()
    expect(result.actualCost).toBeNull()
  })

  it.each([
    UpgradeStatus.Idea,
    UpgradeStatus.Researching,
    UpgradeStatus.ReadyToBuy,
    UpgradeStatus.Cancelled,
  ])('clears purchase details for status %i', (status) => {
    const result = withStatus(input(), status)

    expect(result.purchasedDate).toBeNull()
    expect(result.actualCost).toBeNull()
  })

  it('keeps purchase details when the status stays Purchased', () => {
    const result = withStatus(input(), UpgradeStatus.Purchased)

    expect(result.purchasedDate).toBe('2026-01-01T00:00:00Z')
    expect(result.actualCost).toBe(95)
  })

  it('leaves every other field untouched', () => {
    const original = input({ title: 'Desk', notes: 'keep me', priority: 71 })

    const result = withStatus(original, UpgradeStatus.Idea)

    expect(result.title).toBe('Desk')
    expect(result.notes).toBe('keep me')
    expect(result.priority).toBe(71)
  })

  it('does not mutate its argument', () => {
    const original = input()

    withStatus(original, UpgradeStatus.Idea)

    expect(original.purchasedDate).toBe('2026-01-01T00:00:00Z')
    expect(original.status).toBe(UpgradeStatus.Purchased)
  })
})

describe('label lookups', () => {
  it('names every declared category', () => {
    expect(categoryLabel(UpgradeCategory.Home)).toBe('Home')
    expect(categoryLabel(UpgradeCategory.Technology)).toBe('Technology')
    expect(categoryLabel(UpgradeCategory.Other)).toBe('Other')
  })

  it('names every declared status', () => {
    expect(statusLabel(UpgradeStatus.ReadyToBuy)).toBe('Ready to Buy')
    expect(statusLabel(UpgradeStatus.Cancelled)).toBe('Cancelled')
  })

  it('falls back for a value outside the enum instead of rendering nothing', () => {
    // Rows written before the API validated enum ranges can still hold one.
    expect(categoryLabel(99 as UpgradeCategory)).toBe('Unknown')
    expect(statusLabel(99 as UpgradeStatus)).toBe('Unknown')
  })
})

describe('toUpsertInput', () => {
  it('drops the fields the server owns', () => {
    const dto: UpgradeDto = {
      id: 7,
      title: 'Desk',
      description: null,
      category: UpgradeCategory.Office,
      priority: 70,
      estimatedCost: 700,
      status: UpgradeStatus.ReadyToBuy,
      notes: null,
      productLink: null,
      prerequisiteUpgradeId: null,
      prerequisiteTitle: null,
      purchasedDate: null,
      actualCost: null,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-02T00:00:00Z',
      isBlocked: false,
      effectivePriority: 92,
      unlocksUpgradeId: 3,
      unlocksTitle: 'Arm',
    }

    const result = toUpsertInput(dto) as unknown as Record<string, unknown>

    // Identity, timestamps and derived recommendation fields are the server's to
    // decide; a round-trip must not offer to overwrite them.
    for (const owned of ['id', 'createdAt', 'updatedAt', 'isBlocked', 'effectivePriority', 'unlocksUpgradeId', 'unlocksTitle', 'prerequisiteTitle']) {
      expect(result).not.toHaveProperty(owned)
    }
    expect(result.title).toBe('Desk')
    expect(result.priority).toBe(70)
  })
})
