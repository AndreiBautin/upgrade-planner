export enum UpgradeCategory {
  Home = 0,
  Office = 1,
  Gym = 2,
  Technology = 3,
  Vehicle = 4,
  Lifestyle = 5,
  Other = 6,
}

export enum UpgradeStatus {
  Idea = 0,
  Researching = 1,
  ReadyToBuy = 2,
  Purchased = 3,
  Cancelled = 4,
}

export const CATEGORY_LABELS: Record<UpgradeCategory, string> = {
  [UpgradeCategory.Home]: 'Home',
  [UpgradeCategory.Office]: 'Office',
  [UpgradeCategory.Gym]: 'Gym',
  [UpgradeCategory.Technology]: 'Technology',
  [UpgradeCategory.Vehicle]: 'Vehicle',
  [UpgradeCategory.Lifestyle]: 'Lifestyle',
  [UpgradeCategory.Other]: 'Other',
}

export const STATUS_LABELS: Record<UpgradeStatus, string> = {
  [UpgradeStatus.Idea]: 'Idea',
  [UpgradeStatus.Researching]: 'Researching',
  [UpgradeStatus.ReadyToBuy]: 'Ready to Buy',
  [UpgradeStatus.Purchased]: 'Purchased',
  [UpgradeStatus.Cancelled]: 'Cancelled',
}

/**
 * The API now rejects enum values outside the declared set, but rows written
 * before that validation existed can still hold one. Looking a label up through
 * these functions turns such a row into a visible "Unknown" instead of an empty
 * tag and a `class="pill undefined"`.
 */
export function categoryLabel(category: UpgradeCategory): string {
  return CATEGORY_LABELS[category] ?? 'Unknown'
}

export function statusLabel(status: UpgradeStatus): string {
  return STATUS_LABELS[status] ?? 'Unknown'
}

/**
 * Purchase details only belong on a purchased upgrade — the API rejects the
 * combination outright. Routing every status change through here means a form
 * cannot submit a leftover purchase date on an item moved back to "Idea".
 */
export function withStatus(input: UpsertUpgradeInput, status: UpgradeStatus): UpsertUpgradeInput {
  if (status === UpgradeStatus.Purchased) return { ...input, status }

  return { ...input, status, purchasedDate: null, actualCost: null }
}

export interface UpgradeDto {
  id: number
  title: string
  description: string | null
  category: UpgradeCategory
  priority: number
  estimatedCost: number | null
  status: UpgradeStatus
  notes: string | null
  productLink: string | null
  prerequisiteUpgradeId: number | null
  prerequisiteTitle: string | null
  purchasedDate: string | null
  actualCost: number | null
  createdAt: string
  updatedAt: string
  isBlocked: boolean
  effectivePriority: number
  unlocksUpgradeId: number | null
  unlocksTitle: string | null
}

export interface UpsertUpgradeInput {
  title: string
  description: string | null
  category: UpgradeCategory
  priority: number
  estimatedCost: number | null
  status: UpgradeStatus
  notes: string | null
  productLink: string | null
  prerequisiteUpgradeId: number | null
  purchasedDate: string | null
  actualCost: number | null
}

export function toUpsertInput(u: UpgradeDto): UpsertUpgradeInput {
  return {
    title: u.title,
    description: u.description,
    category: u.category,
    priority: u.priority,
    estimatedCost: u.estimatedCost,
    status: u.status,
    notes: u.notes,
    productLink: u.productLink,
    prerequisiteUpgradeId: u.prerequisiteUpgradeId,
    purchasedDate: u.purchasedDate,
    actualCost: u.actualCost,
  }
}
