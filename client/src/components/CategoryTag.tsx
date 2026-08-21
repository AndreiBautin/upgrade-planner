import { categoryLabel, type UpgradeCategory } from '../types'

export function CategoryTag({ category }: { category: UpgradeCategory }) {
  return <span className="tag">{categoryLabel(category)}</span>
}
