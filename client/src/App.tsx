import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { Layout } from './components/Layout'
import { Dashboard } from './pages/Dashboard'
import { AllUpgrades } from './pages/AllUpgrades'
import { AddUpgrade } from './pages/AddUpgrade'
import { UpgradeDetails } from './pages/UpgradeDetails'

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<Dashboard />} />
          <Route path="upgrades" element={<AllUpgrades />} />
          <Route path="upgrades/new" element={<AddUpgrade />} />
          <Route path="upgrades/:id" element={<UpgradeDetails />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}

export default App
