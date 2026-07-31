import { NavLink, Outlet } from 'react-router-dom'

export function Layout() {
  return (
    <>
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
