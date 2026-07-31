import { CATEGORY_LABELS, UpgradeCategory } from '../types'

export function CategoryTag({ category }: { category: UpgradeCategory }) {
  return <span className="tag">{CATEGORY_LABELS[category]}</span>
}
