import type { UpgradeDto } from './types'

/**
 * Turns a drag-and-drop position into a priority number.
 *
 * Dropping onto a row means "insert before this row": the dragged item takes the
 * target's place and the target moves down. The new priority is the midpoint of
 * the two rows it lands between, which keeps the rest of the list untouched — a
 * reorder is one PUT, not a renumbering of everything below it.
 *
 * The clamp is load-bearing. The API rejects a priority outside 1–100 and the
 * database enforces it with a CHECK constraint, so an unclamped midpoint at the
 * boundary would produce a rejected save rather than an odd-looking number.
 */
export function computeDropPriority(draggedId: number, targetId: number, orderedList: UpgradeDto[]): number {
  const withoutDragged = orderedList.filter((u) => u.id !== draggedId)
  const targetIndex = withoutDragged.findIndex((u) => u.id === targetId)
  const prev = withoutDragged[targetIndex - 1]
  const next = withoutDragged[targetIndex]

  let priority: number
  if (prev && next) {
    priority = Math.round((prev.priority + next.priority) / 2)
  } else if (!prev && next) {
    priority = next.priority + 1
  } else if (prev && !next) {
    priority = prev.priority - 1
  } else {
    priority = 50
  }
  return Math.max(1, Math.min(100, priority))
}
