import { STATUS_LABELS, UpgradeStatus } from '../types'

const CLASS_BY_STATUS: Record<UpgradeStatus, string> = {
  [UpgradeStatus.Idea]: 'pill-idea',
  [UpgradeStatus.Researching]: 'pill-researching',
  [UpgradeStatus.ReadyToBuy]: 'pill-readytobuy',
  [UpgradeStatus.Purchased]: 'pill-purchased',
  [UpgradeStatus.Cancelled]: 'pill-cancelled',
}

export function StatusPill({ status }: { status: UpgradeStatus }) {
  return <span className={`pill ${CLASS_BY_STATUS[status]}`}>{STATUS_LABELS[status]}</span>
}

export function BlockedPill() {
  return <span className="pill pill-blocked">Blocked</span>
}
