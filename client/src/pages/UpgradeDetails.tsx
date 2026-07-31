import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams, Link } from 'react-router-dom'
import { deleteUpgrade, listUpgrades, updateUpgrade } from '../api'
import { CATEGORY_LABELS, STATUS_LABELS, UpgradeCategory, type UpgradeDto, UpgradeStatus, type UpsertUpgradeInput } from '../types'
import { StatusPill, BlockedPill } from '../components/StatusPill'
import { formatCost } from '../format'

function descendantIds(id: number, all: UpgradeDto[]): Set<number> {
  const childrenByParent = new Map<number, number[]>()
  for (const u of all) {
    if (u.prerequisiteUpgradeId !== null) {
      const list = childrenByParent.get(u.prerequisiteUpgradeId) ?? []
      list.push(u.id)
      childrenByParent.set(u.prerequisiteUpgradeId, list)
    }
  }
  const result = new Set<number>([id])
  const stack = [id]
  while (stack.length > 0) {
    const current = stack.pop()!
    for (const childId of childrenByParent.get(current) ?? []) {
      if (!result.has(childId)) {
        result.add(childId)
        stack.push(childId)
      }
    }
  }
  return result
}

export function UpgradeDetails() {
  const { id } = useParams()
  const upgradeId = Number(id)
  const navigate = useNavigate()

  const [all, setAll] = useState<UpgradeDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [form, setForm] = useState<UpsertUpgradeInput | null>(null)

  useEffect(() => {
    listUpgrades()
      .then((upgrades) => {
        setAll(upgrades)
        const current = upgrades.find((u) => u.id === upgradeId)
        if (current) {
          setForm({
            title: current.title,
            description: current.description,
            category: current.category,
            priority: current.priority,
            estimatedCost: current.estimatedCost,
            status: current.status,
            notes: current.notes,
            productLink: current.productLink,
            prerequisiteUpgradeId: current.prerequisiteUpgradeId,
            purchasedDate: current.purchasedDate,
            actualCost: current.actualCost,
          })
        }
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load upgrade.'))
  }, [upgradeId])

  const current = all?.find((u) => u.id === upgradeId) ?? null
  const blocks = useMemo(() => all?.filter((u) => u.prerequisiteUpgradeId === upgradeId) ?? [], [all, upgradeId])
  const prerequisiteOptions = useMemo(() => {
    if (!all) return []
    const unavailable = descendantIds(upgradeId, all)
    return all.filter((u) => !unavailable.has(u.id))
  }, [all, upgradeId])

  if (error) return <div className="error-banner">{error}</div>
  if (!all || !form) return <p className="empty-state">Loading…</p>
  if (!current) return <div className="empty-state">Upgrade not found. <Link to="/upgrades">Back to all upgrades</Link></div>

  async function handleSave(e: React.FormEvent) {
    e.preventDefault()
    if (!form!.title.trim()) {
      setError('Title is required.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      await updateUpgrade(upgradeId, form!)
      const refreshed = await listUpgrades()
      setAll(refreshed)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save changes.')
    } finally {
      setSaving(false)
    }
  }

  async function handleDelete() {
    if (!window.confirm(`Delete "${current!.title}"? This can't be undone.`)) return
    try {
      await deleteUpgrade(upgradeId)
      navigate('/upgrades')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete upgrade.')
    }
  }

  function update<K extends keyof UpsertUpgradeInput>(key: K, value: UpsertUpgradeInput[K]) {
    setForm((f) => (f ? { ...f, [key]: value } : f))
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1>{current.title}</h1>
          <div className="upgrade-meta" style={{ marginTop: 6 }}>
            <StatusPill status={current.status} />
            {current.isBlocked && <BlockedPill />}
            {current.unlocksTitle && (
              <span className="hint">Unlocks: {current.unlocksTitle} (effective priority {current.effectivePriority})</span>
            )}
          </div>
        </div>
        <button className="btn btn-danger" onClick={handleDelete}>Delete</button>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <form className="card" style={{ padding: 24 }} onSubmit={handleSave}>
        <div className="field">
          <label htmlFor="title">Title</label>
          <input id="title" value={form.title} onChange={(e) => update('title', e.target.value)} />
        </div>

        <div className="field">
          <label htmlFor="description">Description</label>
          <textarea
            id="description"
            value={form.description ?? ''}
            onChange={(e) => update('description', e.target.value || null)}
          />
        </div>

        <div className="field-row">
          <div className="field">
            <label htmlFor="category">Category</label>
            <select
              id="category"
              value={form.category}
              onChange={(e) => update('category', Number(e.target.value) as UpgradeCategory)}
            >
              {Object.entries(CATEGORY_LABELS).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="status">Status</label>
            <select
              id="status"
              value={form.status}
              onChange={(e) => update('status', Number(e.target.value) as UpgradeStatus)}
            >
              {Object.entries(STATUS_LABELS).map(([value, label]) => (
                <option key={value} value={value}>{label}</option>
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
              value={form.priority}
              onChange={(e) => update('priority', Number(e.target.value))}
            />
          </div>

          <div className="field">
            <label htmlFor="cost">Estimated cost</label>
            <input
              id="cost"
              type="number"
              min={0}
              step="0.01"
              value={form.estimatedCost ?? ''}
              onChange={(e) => update('estimatedCost', e.target.value === '' ? null : Number(e.target.value))}
            />
          </div>
        </div>

        <div className="field">
          <label htmlFor="prerequisite">Prerequisite</label>
          <select
            id="prerequisite"
            value={form.prerequisiteUpgradeId ?? ''}
            onChange={(e) => update('prerequisiteUpgradeId', e.target.value === '' ? null : Number(e.target.value))}
          >
            <option value="">None</option>
            {prerequisiteOptions.map((u) => (
              <option key={u.id} value={u.id}>{u.title}</option>
            ))}
          </select>
          <span className="hint">This upgrade is blocked until its prerequisite is Purchased.</span>
        </div>

        {blocks.length > 0 && (
          <div className="field">
            <label>Blocks</label>
            <div>
              {blocks.map((b) => (
                <Link key={b.id} to={`/upgrades/${b.id}`} className="tag" style={{ marginRight: 6 }}>
                  {b.title}
                </Link>
              ))}
            </div>
          </div>
        )}

        <div className="field">
          <label htmlFor="link">Product link</label>
          <input
            id="link"
            type="url"
            placeholder="optional"
            value={form.productLink ?? ''}
            onChange={(e) => update('productLink', e.target.value || null)}
          />
        </div>

        <div className="field">
          <label htmlFor="notes">Notes</label>
          <textarea
            id="notes"
            value={form.notes ?? ''}
            onChange={(e) => update('notes', e.target.value || null)}
          />
        </div>

        {form.status === UpgradeStatus.Purchased && (
          <div className="field-row">
            <div className="field">
              <label htmlFor="purchasedDate">Purchased date</label>
              <input
                id="purchasedDate"
                type="date"
                value={form.purchasedDate ? form.purchasedDate.slice(0, 10) : ''}
                onChange={(e) => update('purchasedDate', e.target.value ? new Date(e.target.value).toISOString() : null)}
              />
            </div>
            <div className="field">
              <label htmlFor="actualCost">Actual cost</label>
              <input
                id="actualCost"
                type="number"
                min={0}
                step="0.01"
                value={form.actualCost ?? ''}
                onChange={(e) => update('actualCost', e.target.value === '' ? null : Number(e.target.value))}
              />
            </div>
          </div>
        )}

        <div className="form-actions">
          <button type="submit" className="btn btn-primary" disabled={saving}>
            {saving ? 'Saving…' : 'Save Changes'}
          </button>
          {current.estimatedCost !== null && (
            <span className="hint">Estimated {formatCost(current.estimatedCost)}</span>
          )}
        </div>
      </form>
    </div>
  )
}
