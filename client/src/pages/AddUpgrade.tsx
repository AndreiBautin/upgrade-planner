import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { createUpgrade } from '../api'
import { CATEGORY_LABELS, UpgradeCategory, UpgradeStatus } from '../types'

export function AddUpgrade() {
  const navigate = useNavigate()
  const [title, setTitle] = useState('')
  const [category, setCategory] = useState(UpgradeCategory.Home)
  const [priority, setPriority] = useState(50)
  const [estimatedCost, setEstimatedCost] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!title.trim()) {
      setError('Title is required.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      const created = await createUpgrade({
        title: title.trim(),
        description: null,
        category,
        priority,
        estimatedCost: estimatedCost === '' ? null : Number(estimatedCost),
        status: UpgradeStatus.Idea,
        notes: null,
        productLink: null,
        prerequisiteUpgradeId: null,
        purchasedDate: null,
        actualCost: null,
      })
      navigate(`/upgrades/${created.id}`)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create upgrade.')
      setSaving(false)
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>Add Upgrade</h1>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <form className="card" style={{ padding: 24 }} onSubmit={handleSubmit}>
        <div className="field">
          <label htmlFor="title">Title</label>
          <input
            id="title"
            autoFocus
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="e.g. Upgrade Office Chair"
          />
        </div>

        <div className="field-row">
          <div className="field">
            <label htmlFor="category">Category</label>
            <select
              id="category"
              value={category}
              onChange={(e) => setCategory(Number(e.target.value) as UpgradeCategory)}
            >
              {Object.entries(CATEGORY_LABELS).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="priority">Priority (1–100)</label>
            <input
              id="priority"
              type="number"
              min={1}
              max={100}
              value={priority}
              onChange={(e) => setPriority(Number(e.target.value))}
            />
          </div>

          <div className="field">
            <label htmlFor="cost">Estimated cost</label>
            <input
              id="cost"
              type="number"
              min={0}
              step="0.01"
              placeholder="optional"
              value={estimatedCost}
              onChange={(e) => setEstimatedCost(e.target.value)}
            />
          </div>
        </div>

        <div className="form-actions">
          <button type="submit" className="btn btn-primary" disabled={saving}>
            {saving ? 'Adding…' : 'Add Upgrade'}
          </button>
          <span className="hint">You can add notes, a product link, or a prerequisite afterward.</span>
        </div>
      </form>
    </div>
  )
}
