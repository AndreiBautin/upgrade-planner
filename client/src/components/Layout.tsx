import { NavLink, Outlet } from 'react-router-dom'

const isDemoMode = import.meta.env.VITE_DEMO_MODE === 'true'

export function Layout() {
  return (
    <>
      {isDemoMode && (
        <div className="demo-banner">
          This is a public demo with fake, auto-resetting data — not connected to the author's real instance.
        </div>
      )}
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
    </>
  )
}
