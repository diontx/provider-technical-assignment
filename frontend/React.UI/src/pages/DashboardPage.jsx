import { useState } from 'react'
import { getDashboardLicenses, getDashboardProviders, getDashboardSummary } from '../api/providers.js'
import { BarChart, PieChart } from '../components/Charts.jsx'
import { EmptyState, ErrorAlert, Loading } from '../components/Feedback.jsx'
import ProviderDetail from '../components/ProviderDetail.jsx'
import RecordsDialog from '../components/RecordsDialog.jsx'
import { formatDate } from '../format.js'
import { useAsync } from '../hooks/useAsync.js'

// Colours for the license pie, by the bucket key the API returns.
const VALIDITY_COLORS = {
  Valid: '#1c6b3a',
  Expired: '#b3261e',
  Suspended: '#c98a00',
  OtherNotValid: '#7b8794',
}

/**
 * Read-only overview. Every number is computed by GET /api/dashboard/summary with the same
 * license rules as the rest of the API, and every number opens the records behind it, which
 * the API also selects. This page lays the numbers out and decides nothing.
 */
export default function DashboardPage() {
  const { data: summary, error, loading, reload } = useAsync((signal) => getDashboardSummary(signal), [])
  const [records, setRecords] = useState(null) // { title, kind, load } while a record list is open
  const [providerId, setProviderId] = useState(null)

  if (loading && !summary) {
    return <Loading label="Loading dashboard…" />
  }

  if (error) {
    return <ErrorAlert error={error} onRetry={reload} />
  }

  const showProviders = (title, filters) =>
    setRecords({ title, kind: 'providers', load: (signal) => getDashboardProviders(filters, signal) })

  const showLicenses = (title, validity) =>
    setRecords({ title, kind: 'licenses', load: (signal) => getDashboardLicenses(validity, signal) })

  return (
    <>
      <p className="hint">
        Evaluated as of {formatDate(summary.asOfDate)} (US Eastern). Deleted providers are not counted. Select any
        number to see the records behind it.
      </p>

      <div className="cards">
        <Card label="Providers" value={summary.totalProviders} onSelect={() => showProviders('All Providers', {})} />
        <Card label="Licenses" value={summary.totalLicenses} onSelect={() => showLicenses('All Licenses')} />
        <Card
          label="Active providers with no valid license"
          value={summary.activeProvidersWithoutValidLicense}
          alert
          onSelect={() => showProviders('Active Providers with No Valid License', { licenseGap: 'NoValidLicense' })}
        />
        <Card
          label="Active providers with an expired license"
          value={summary.activeProvidersWithExpiredLicense}
          alert
          onSelect={() => showProviders('Active Providers with an Expired License', { licenseGap: 'ExpiredLicense' })}
        />
      </div>

      <div className="charts">
        <BarChart
          title="Providers by Status"
          unit="provider"
          items={summary.providersByStatus}
          onSelect={(item) => showProviders(`${item.name} Providers`, { status: item.key })}
        />
        <PieChart
          title="Expired vs. Valid Licenses"
          unit="license"
          items={summary.licensesByEffectiveStatus
            .filter(isShown)
            .map((item) => ({ ...item, color: VALIDITY_COLORS[item.key] ?? VALIDITY_COLORS.OtherNotValid }))}
          onSelect={(item) => showLicenses(`${item.name} Licenses`, item.key)}
        />
        <BarChart
          title="Licenses per Provider (Most)"
          unit="license"
          items={summary.licensesPerProvider.map((provider) => ({
            key: provider.providerId,
            name: provider.providerName,
            count: provider.licenseCount,
          }))}
          onSelect={(item) => setProviderId(item.key)}
        />
        <BarChart
          title="Providers by County (Largest)"
          unit="provider"
          items={summary.providersByCounty}
          onSelect={(item) => showProviders(`Providers in ${item.name} County`, { county: item.key })}
        />
      </div>

      <section className="panel">
        <h2>Valid Licenses Expiring in the Next {summary.expiringWindowDays} Days</h2>
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
                  <td>
                    <button
                      type="button"
                      className="link"
                      aria-label={`View licenses for ${license.providerName}`}
                      onClick={() => setProviderId(license.providerId)}
                    >
                      {license.providerName}
                    </button>
                  </td>
                  <td>{license.licenseNumber}</td>
                  <td>{formatDate(license.expirationDate)}</td>
                  <td>{license.daysRemaining}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      {records && (
        // The key gives each list its own dialog, so one list never shows another's rows while loading.
        <RecordsDialog key={records.title} {...records} onClose={() => setRecords(null)} />
      )}

      {providerId !== null && <ProviderDetail providerId={providerId} onClose={() => setProviderId(null)} />}
    </>
  )
}

/** The catch-all bucket is only worth showing when something is in it. */
function isShown(item) {
  return item.key !== 'OtherNotValid' || item.count > 0
}

/** A summary number. The whole card is the button that opens its records. */
function Card({ label, value, alert = false, onSelect }) {
  return (
    <button
      type="button"
      className={`card${alert && value > 0 ? ' card-alert' : ''}`}
      disabled={value === 0}
      aria-label={`${label}: ${value}. View records`}
      onClick={onSelect}
    >
      <span className="card-value">{value}</span>
      <span className="card-label">{label}</span>
    </button>
  )
}
