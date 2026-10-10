import { useState } from 'react'
import DashboardPage from './pages/DashboardPage.jsx'
import DeletedProvidersPage from './pages/DeletedProvidersPage.jsx'
import ProvidersPage from './pages/ProvidersPage.jsx'
import ReportsPage from './pages/ReportsPage.jsx'

const VIEWS = [
  { id: 'providers', label: 'Providers', Page: ProvidersPage },
  { id: 'reports', label: 'License reports', Page: ReportsPage },
  { id: 'dashboard', label: 'Dashboard', Page: DashboardPage },
  { id: 'deleted', label: 'Deleted (audit)', Page: DeletedProvidersPage },
]

function App() {
  // Four views do not justify a router dependency; the current view is plain state.
  const [viewId, setViewId] = useState(VIEWS[0].id)
  const { label, Page } = VIEWS.find((view) => view.id === viewId)

  return (
    <div className="app">
      <header className="app-header">
        <span className="app-title">Provider &amp; License Management</span>
        <nav aria-label="Main">
          {VIEWS.map((view) => (
            <button
              key={view.id}
              type="button"
              aria-current={view.id === viewId ? 'page' : undefined}
              onClick={() => setViewId(view.id)}
            >
              {view.label}
            </button>
          ))}
        </nav>
      </header>

      <main>
        <h1>{label}</h1>
        {/* Each view loads its own data when shown, so switching back always shows current data. */}
        <Page />
      </main>
    </div>
  )
}

export default App
