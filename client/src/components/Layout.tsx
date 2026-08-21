import { useEffect, useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { onSlowRequest } from '../api'
import { config } from '../config'

/**
 * Tells the visitor the server is waking up rather than dead.
 *
 * The demo API runs on a free instance that spins down after 15 minutes idle and
 * takes up to a minute to start. Saying so is the difference between "the free
 * tier is doing its thing" and "this person's project is broken".
 */
function ColdStartNotice() {
  const [isSlow, setIsSlow] = useState(false)

  useEffect(() => onSlowRequest(setIsSlow), [])

  if (!isSlow) return null

  return (
    <div className="cold-start-notice" role="status">
      Waking the API up — the free instance sleeps after 15 minutes idle, so the first request can take up to a minute.
    </div>
  )
}

export function Layout() {
  return (
    <>
      {config.demoMode && (
        <div className="demo-banner">
          Public demo. The data is generated and disposable — nothing here is connected to the author's real instance.
        </div>
      )}
      <ColdStartNotice />
      <header className="app-nav">
        <span className="brand">Upgrade Planner</span>
        <nav>
          <NavLink to="/" end className={({ isActive }) => (isActive ? 'active' : '')}>
            Dashboard
          </NavLink>
          <NavLink to="/upgrades" className={({ isActive }) => (isActive ? 'active' : '')}>
            All Upgrades
          </NavLink>
        </nav>
        <NavLink to="/upgrades/new" className="btn btn-primary btn-sm">
          + Add Upgrade
        </NavLink>
      </header>
      <main className="app-main">
        <Outlet />
      </main>
      <footer className="app-footer">
        <span>Upgrade Planner</span>
        {/* Ties the page a reviewer is looking at back to a specific commit. */}
        <span className="build-tag" title="Build this page was served from">
          build {config.buildSha}
        </span>
      </footer>
    </>
  )
}
