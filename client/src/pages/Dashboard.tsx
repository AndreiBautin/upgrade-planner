import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getRecommendations, updateUpgrade } from '../api'
import { toUpsertInput, type UpgradeDto, UpgradeStatus } from '../types'
import { StatusPill, BlockedPill } from '../components/StatusPill'
import { CategoryTag } from '../components/CategoryTag'
import { formatCost } from '../format'

export function Dashboard() {
  const [upgrades, setUpgrades] = useState<UpgradeDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [budget, setBudget] = useState('')
  const [marking, setMarking] = useState<number | null>(null)

  function load() {
    getRecommendations()
      .then(setUpgrades)
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load recommendations.'))
  }

  useEffect(load, [])

  async function markPurchased(u: UpgradeDto) {
    setMarking(u.id)
    try {
      await updateUpgrade(u.id, {
        ...toUpsertInput(u),
        status: UpgradeStatus.Purchased,
        purchasedDate: new Date().toISOString(),
        actualCost: u.estimatedCost,
      })
      load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update upgrade.')
    } finally {
      setMarking(null)
    }
  }

  if (error) return <div className="error-banner">{error}</div>
  if (!upgrades) return <p className="empty-state">Loading…</p>

  if (upgrades.length === 0) {
    return (
      <div className="empty-state">
        No active upgrades yet. <Link to="/upgrades/new">Add your first one</Link>.
      </div>
    )
  }

  const topPick = upgrades.find((u) => !u.isBlocked) ?? null
  const budgetValue = budget === '' ? null : Number(budget)

  return (
    <div>
      <div className="page-header">
        <h1>Best Use of Your Free Money</h1>
      </div>

      <div className="budget-bar">
        <span>I have</span>
        <input
          type="number"
          min={0}
          placeholder="$ amount"
          value={budget}
          onChange={(e) => setBudget(e.target.value)}
        />
        <span>to spend today</span>
      </div>

      {topPick && (
        <div className="card hero-card">
          <span className="hero-eyebrow">Recommended Next</span>
          <span className="hero-title">{topPick.title}</span>
          <div className="hero-detail-row">
            <span>Priority {topPick.priority}</span>
            <span>{formatCost(topPick.estimatedCost)}</span>
            <CategoryTag category={topPick.category} />
            <StatusPill status={topPick.status} />
            {topPick.unlocksTitle && <span>Unlocks: {topPick.unlocksTitle}</span>}
            {budgetValue !== null && topPick.estimatedCost !== null && topPick.estimatedCost <= budgetValue && (
              <span className="affordable">✓ within budget</span>
            )}
          </div>
          <div className="form-actions">
            <Link to={`/upgrades/${topPick.id}`} className="btn">View Details</Link>
            <button
              className="btn btn-primary"
              disabled={marking === topPick.id}
              onClick={() => markPurchased(topPick)}
            >
              {marking === topPick.id ? 'Marking…' : 'Mark as Purchased'}
            </button>
          </div>
        </div>
      )}

      <div className="card">
        {upgrades.map((u, i) => (
          <Link key={u.id} to={`/upgrades/${u.id}`} className="upgrade-row">
            <span className="rank-num">{i + 1}</span>
            <span className="priority-badge">{u.priority}</span>
            <div className="upgrade-row-main">
              <div className="upgrade-title">{u.title}</div>
              <div className="upgrade-meta">
                <CategoryTag category={u.category} />
                <StatusPill status={u.status} />
                {u.isBlocked && <BlockedPill />}
                {u.isBlocked && u.prerequisiteTitle && <span>Requires: {u.prerequisiteTitle}</span>}
                {!u.isBlocked && u.unlocksTitle && <span>Unlocks: {u.unlocksTitle}</span>}
              </div>
            </div>
            <span
              className={
                budgetValue !== null && u.estimatedCost !== null && u.estimatedCost <= budgetValue
                  ? 'upgrade-cost affordable'
                  : 'upgrade-cost'
              }
            >
              {formatCost(u.estimatedCost)}
            </span>
          </Link>
        ))}
      </div>
    </div>
  )
}
