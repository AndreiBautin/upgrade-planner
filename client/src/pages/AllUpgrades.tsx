import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { listUpgrades, updateUpgrade } from '../api'
import { CATEGORY_LABELS, STATUS_LABELS, toUpsertInput, UpgradeCategory, type UpgradeDto, UpgradeStatus } from '../types'
import { StatusPill, BlockedPill } from '../components/StatusPill'
import { CategoryTag } from '../components/CategoryTag'
import { formatCost, formatDate } from '../format'

type SortKey = 'priority' | 'cost' | 'title' | 'status' | 'purchasedDate'

function computeDropPriority(draggedId: number, targetId: number, orderedList: UpgradeDto[]): number {
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

export function AllUpgrades() {
  const navigate = useNavigate()
  const [upgrades, setUpgrades] = useState<UpgradeDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<UpgradeStatus | 'all'>('all')
  const [categoryFilter, setCategoryFilter] = useState<UpgradeCategory | 'all'>('all')
  const [sortKey, setSortKey] = useState<SortKey>('priority')
  const [draggedId, setDraggedId] = useState<number | null>(null)
  const [dragOverId, setDragOverId] = useState<number | null>(null)

  function load() {
    return listUpgrades()
      .then(setUpgrades)
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load upgrades.'))
  }

  useEffect(() => {
    load()
  }, [])

  const filtered = useMemo(() => {
    if (!upgrades) return []
    let result = upgrades
    if (statusFilter !== 'all') result = result.filter((u) => u.status === statusFilter)
    if (categoryFilter !== 'all') result = result.filter((u) => u.category === categoryFilter)
    if (search.trim()) {
      const q = search.trim().toLowerCase()
      result = result.filter((u) => u.title.toLowerCase().includes(q))
    }
    const sorted = [...result]
    switch (sortKey) {
      case 'priority':
        sorted.sort((a, b) => b.priority - a.priority)
        break
      case 'cost':
        sorted.sort((a, b) => (b.estimatedCost ?? -1) - (a.estimatedCost ?? -1))
        break
      case 'title':
        sorted.sort((a, b) => a.title.localeCompare(b.title))
        break
      case 'status':
        sorted.sort((a, b) => a.status - b.status)
        break
      case 'purchasedDate':
        sorted.sort((a, b) => (b.purchasedDate ?? '').localeCompare(a.purchasedDate ?? ''))
        break
    }
    return sorted
  }, [upgrades, search, statusFilter, categoryFilter, sortKey])

  const reorderable = sortKey === 'priority' && !search.trim() && statusFilter === 'all' && categoryFilter === 'all'

  async function handleDrop(targetId: number) {
    const wasDragging = draggedId
    setDraggedId(null)
    setDragOverId(null)
    if (!upgrades || wasDragging === null || wasDragging === targetId) return

    const dragged = upgrades.find((u) => u.id === wasDragging)
    if (!dragged) return

    const newPriority = computeDropPriority(wasDragging, targetId, filtered)
    if (newPriority === dragged.priority) return

    // Optimistic local update so the row doesn't jump back before the refetch lands.
    setUpgrades((prev) => prev?.map((u) => (u.id === wasDragging ? { ...u, priority: newPriority } : u)) ?? prev)
    try {
      await updateUpgrade(wasDragging, { ...toUpsertInput(dragged), priority: newPriority })
      load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to reorder upgrade.')
      load()
    }
  }

  return (
    <div>
      <div className="page-header">
        <h1>All Upgrades</h1>
      </div>

      {error && <div className="error-banner">{error}</div>}

      <div className="toolbar">
        <input
          placeholder="Search by title…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value === 'all' ? 'all' : Number(e.target.value))}>
          <option value="all">All statuses</option>
          {Object.entries(STATUS_LABELS).map(([value, label]) => (
            <option key={value} value={value}>{label}</option>
          ))}
        </select>
        <select value={categoryFilter} onChange={(e) => setCategoryFilter(e.target.value === 'all' ? 'all' : Number(e.target.value))}>
          <option value="all">All categories</option>
          {Object.entries(CATEGORY_LABELS).map(([value, label]) => (
            <option key={value} value={value}>{label}</option>
          ))}
        </select>
        <select value={sortKey} onChange={(e) => setSortKey(e.target.value as SortKey)}>
          <option value="priority">Sort: Priority</option>
          <option value="cost">Sort: Cost</option>
          <option value="title">Sort: Title</option>
          <option value="status">Sort: Status</option>
          <option value="purchasedDate">Sort: Purchased Date</option>
        </select>
      </div>

      {reorderable && <p className="hint" style={{ marginBottom: 10 }}>Drag rows to reorder priority.</p>}

      {!error && !upgrades && <p className="empty-state">Loading…</p>}

      {upgrades && filtered.length === 0 && (
        <div className="empty-state">
          {upgrades.length === 0 ? 'No upgrades yet.' : 'No upgrades match your filters.'}
        </div>
      )}

      {filtered.length > 0 && (
        <div className="card">
          {filtered.map((u) => (
            <div
              key={u.id}
              className={`upgrade-row${dragOverId === u.id ? ' drag-over' : ''}${draggedId === u.id ? ' dragging' : ''}`}
              draggable={reorderable}
              onDragStart={() => setDraggedId(u.id)}
              onDragOver={(e) => {
                e.preventDefault()
                if (draggedId !== null && draggedId !== u.id) setDragOverId(u.id)
              }}
              onDragEnd={() => {
                setDraggedId(null)
                setDragOverId(null)
              }}
              onDrop={(e) => {
                e.preventDefault()
                handleDrop(u.id)
              }}
              onClick={() => navigate(`/upgrades/${u.id}`)}
              style={{ cursor: 'pointer' }}
            >
              {reorderable && <span className="drag-handle">⠿</span>}
              <span className="priority-badge">{u.priority}</span>
              <div className="upgrade-row-main">
                <div className="upgrade-title">{u.title}</div>
                <div className="upgrade-meta">
                  <CategoryTag category={u.category} />
                  <StatusPill status={u.status} />
                  {u.isBlocked && <BlockedPill />}
                  {u.status === UpgradeStatus.Purchased && <span>Purchased {formatDate(u.purchasedDate)}</span>}
                </div>
              </div>
              <span className="upgrade-cost">
                {u.status === UpgradeStatus.Purchased && u.actualCost !== null
                  ? formatCost(u.actualCost)
                  : formatCost(u.estimatedCost)}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
