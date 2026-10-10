import { getDashboardSummary } from '../api/providers.js'
import { EmptyState, ErrorAlert, Loading } from '../components/Feedback.jsx'
import { formatDate } from '../format.js'
import { useAsync } from '../hooks/useAsync.js'

/**
 * Read-only overview. Every number is computed by GET /api/dashboard/summary with the same
 * license rules as the rest of the API; this page only lays the numbers out.
 */
export default function DashboardPage() {
  const { data: summary, error, loading, reload } = useAsync((signal) => getDashboardSummary(signal), [])

  if (loading && !summary) {
    return <Loading label="Loading dashboard…" />
  }

  if (error) {
    return <ErrorAlert error={error} onRetry={reload} />
  }

  return (
    <>
      <p className="hint">Evaluated as of {formatDate(summary.asOfDate)} (US Eastern). Deleted providers are not counted.</p>

      <div className="cards">
        <Card label="Providers" value={summary.totalProviders} />
        <Card label="Licenses" value={summary.totalLicenses} />
        <Card
          label="Active providers with no valid license"
          value={summary.activeProvidersWithoutValidLicense}
          alert={summary.activeProvidersWithoutValidLicense > 0}
        />
        <Card
          label="Active providers with an expired license"
          value={summary.activeProvidersWithExpiredLicense}
          alert={summary.activeProvidersWithExpiredLicense > 0}
        />
      </div>

      <div className="charts">
        <BarChart title="Providers by status" items={summary.providersByStatus} />
        <BarChart title="Licenses by validity today" items={summary.licensesByEffectiveStatus.filter(isShown)} />
        <BarChart title="Providers by county (largest)" items={summary.providersByCounty} />
      </div>

      <section className="panel">
        <h2>Valid licenses expiring in the next {summary.expiringWindowDays} days</h2>
        {summary.expiringLicenses.length === 0 ? (
          <EmptyState>No valid license expires in this period.</EmptyState>
        ) : (
          <table>
            <thead>
              <tr>
                <th scope="col">Provider</th>
                <th scope="col">License number</th>
                <th scope="col">Expires</th>
                <th scope="col">Days remaining</th>
              </tr>
            </thead>
            <tbody>
              {summary.expiringLicenses.map((license) => (
                <tr key={`${license.providerId}-${license.licenseNumber}`}>
                  <td>{license.providerName}</td>
                  <td>{license.licenseNumber}</td>
                  <td>{formatDate(license.expirationDate)}</td>
                  <td>{license.daysRemaining}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </>
  )
}

/** The catch-all bucket is only worth showing when something is in it. */
function isShown(item) {
  return item.name !== 'Other not valid' || item.count > 0
}

function Card({ label, value, alert = false }) {
  return (
    <div className={`card${alert ? ' card-alert' : ''}`}>
      <div className="card-value">{value}</div>
      <div className="card-label">{label}</div>
    </div>
  )
}

/** A horizontal bar chart drawn with plain CSS, so no charting library is needed for a few counts. */
function BarChart({ title, items }) {
  const max = Math.max(1, ...items.map((item) => item.count))

  return (
    <section className="panel chart">
      <h2>{title}</h2>
      {items.length === 0 ? (
        <EmptyState>No data.</EmptyState>
      ) : (
        <ul>
          {items.map((item) => (
            <li key={item.name}>
              <span className="chart-label">{item.name}</span>
              <span className="chart-track" aria-hidden="true">
                <span className="chart-bar" style={{ width: `${(item.count / max) * 100}%` }} />
              </span>
              <span className="chart-value">{item.count}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
