import { BrowserRouter, Routes, Route, Link } from 'react-router-dom'
import { Layout } from './components/Layout'
import { ErrorBoundary } from './components/ErrorBoundary'
import { config } from './config'
import { Dashboard } from './pages/Dashboard'
import { AllUpgrades } from './pages/AllUpgrades'
import { AddUpgrade } from './pages/AddUpgrade'
import { UpgradeDetails } from './pages/UpgradeDetails'

/** Shown for a URL that matches nothing, instead of a blank page. */
function NotFound() {
  return (
    <div className="empty-state">
      That page does not exist. <Link to="/">Back to the dashboard</Link>.
    </div>
  )
}

function App() {
  return (
    <ErrorBoundary>
      {/*
        basename comes from config.basePath, which is derived from Vite's BASE_URL,
        which Vite derives from the single `base` option in vite.config.ts. One
        value feeds both the asset URLs and the routes, so a project-page deploy
        under /<repo>/ cannot end up with working assets and 404ing routes.
      */}
      <BrowserRouter basename={config.basePath}>
        <Routes>
          <Route element={<Layout />}>
            <Route index element={<Dashboard />} />
            <Route path="upgrades" element={<AllUpgrades />} />
            <Route path="upgrades/new" element={<AddUpgrade />} />
            <Route path="upgrades/:id" element={<UpgradeDetails />} />
            <Route path="*" element={<NotFound />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </ErrorBoundary>
  )
}

export default App
