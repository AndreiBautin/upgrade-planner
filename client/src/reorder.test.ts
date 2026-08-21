import { describe, expect, it } from 'vitest'
import { computeDropPriority } from './reorder'
import { UpgradeCategory, UpgradeStatus, type UpgradeDto } from './types'

/**
 * Drag-to-reorder turns a drop position into a priority number.
 *
 * The clamp matters more than it used to: the API now rejects a priority outside
 * 1–100 and the database enforces it with a check constraint, so a computation
 * that drifted out of range would produce a rejected save rather than an odd
 * number.
 */
function row(id: number, priority: number): UpgradeDto {
  return {
    id,
    title: `Item ${id}`,
    description: null,
    category: UpgradeCategory.Home,
    priority,
    estimatedCost: null,
    status: UpgradeStatus.Idea,
    notes: null,
    productLink: null,
    prerequisiteUpgradeId: null,
    prerequisiteTitle: null,
    purchasedDate: null,
    actualCost: null,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    isBlocked: false,
    effectivePriority: priority,
    unlocksUpgradeId: null,
    unlocksTitle: null,
  }
}

describe('computeDropPriority', () => {
  it('lands halfway between the neighbours when dropped in the middle', () => {
    const list = [row(1, 90), row(2, 70), row(3, 50), row(4, 30)]

    // Drag item 4 onto item 3's position: neighbours are 70 and 50.
    expect(computeDropPriority(4, 3, list)).toBe(60)
  })

  it('goes above the first item when dropped at the top', () => {
    const list = [row(1, 90), row(2, 70), row(3, 50)]

    expect(computeDropPriority(3, 1, list)).toBe(91)
  })

  it('inserts before the target row, not after it', () => {
    // Dropping onto a row takes that row's place and pushes it down. Dragging 1
    // onto 3 leaves [70, 50] and slots in between them at 60 - above item 3.
    const list = [row(1, 90), row(2, 70), row(3, 50)]

    expect(computeDropPriority(1, 3, list)).toBe(60)
  })

  it('cannot place an item below the last row', () => {
    // A consequence of "insert before the target": the only drop targets are
    // existing rows, so there is no gesture for the tail position. Documented in
    // docs/TESTING.md as a known limitation rather than silently accepted.
    const list = [row(1, 90), row(2, 70), row(3, 50)]

    expect(computeDropPriority(1, 3, list)).toBeGreaterThan(50)
  })

  it('clamps to the top of the range instead of exceeding it', () => {
    // Dropping onto a row already at 100 would compute 101.
    const list = [row(1, 100), row(2, 40)]

    expect(computeDropPriority(2, 1, list)).toBe(100)
  })

  it('stays inside the range when the target is already at the floor', () => {
    const list = [row(1, 60), row(2, 1)]
    const result = computeDropPriority(1, 2, list)

    expect(result).toBeGreaterThanOrEqual(1)
    expect(result).toBeLessThanOrEqual(100)
  })

  it('always produces a value the API will accept', () => {
    // The boundaries are where a drop is most likely to compute out of range.
    const cases: Array<[number, number, UpgradeDto[]]> = [
      [2, 1, [row(1, 100), row(2, 100)]],
      [1, 2, [row(1, 1), row(2, 1)]],
      [2, 1, [row(1, 1), row(2, 100)]],
      [1, 2, [row(1, 100), row(2, 1)]],
    ]

    for (const [dragged, target, list] of cases) {
      const result = computeDropPriority(dragged, target, list)
      expect(result).toBeGreaterThanOrEqual(1)
      expect(result).toBeLessThanOrEqual(100)
      expect(Number.isInteger(result)).toBe(true)
    }
  })

  it('produces a valid priority for a single-item list', () => {
    const list = [row(1, 50)]
    const result = computeDropPriority(1, 1, list)

    expect(result).toBeGreaterThanOrEqual(1)
    expect(result).toBeLessThanOrEqual(100)
  })
})
