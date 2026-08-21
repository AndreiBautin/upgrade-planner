import { statusLabel, UpgradeStatus } from '../types'

const CLASS_BY_STATUS: Record<UpgradeStatus, string> = {
  [UpgradeStatus.Idea]: 'pill-idea',
  [UpgradeStatus.Researching]: 'pill-researching',
  [UpgradeStatus.ReadyToBuy]: 'pill-readytobuy',
  [UpgradeStatus.Purchased]: 'pill-purchased',
  [UpgradeStatus.Cancelled]: 'pill-cancelled',
}

export function StatusPill({ status }: { status: UpgradeStatus }) {
  // A row stored before the API validated enum ranges can still carry a value
  // outside the set. Falling back keeps the class name from becoming
  // "pill undefined" and the label from rendering empty.
  const className = CLASS_BY_STATUS[status] ?? 'pill-unknown'
  return <span className={`pill ${className}`}>{statusLabel(status)}</span>
}

export function BlockedPill() {
  return <span className="pill pill-blocked">Blocked</span>
}
