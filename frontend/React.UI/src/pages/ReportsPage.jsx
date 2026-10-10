import { useState } from 'react'
import { getActiveProvidersWithActiveLicenses, getActiveProvidersWithExpiredLicenses } from '../api/providers.js'
import { EmptyState, ErrorAlert, Loading } from '../components/Feedback.jsx'
import LicenseTable from '../components/LicenseTable.jsx'
import { formatDate, plural } from '../format.js'
import { useAsync } from '../hooks/useAsync.js'

// The two required license scenarios. Each is one API call; the API decides what is valid or expired.
const REPORTS = [
  {
    id: 'active',
    label: 'Active providers and their active licenses',
    description:
      'Every provider with status Active, with the licenses that are valid today. A provider with no valid license is still listed, so the gap is visible.',
    load: getActiveProvidersWithActiveLicenses,
    emptyLicenses: 'No valid license.',
    empty: 'There are no active providers.',
  },
  {
    id: 'expired',
    label: 'Active providers with expired licenses',
    description:
      'Providers that appear Active but hold at least one license whose expiration date has passed, including licenses whose stored status was never updated.',
    load: getActiveProvidersWithExpiredLicenses,
    emptyLicenses: 'No expired license.',
    empty: 'No active provider has an expired license.',
  },
]

export default function ReportsPage() {
  const [reportId, setReportId] = useState(REPORTS[0].id)
  const report = REPORTS.find((candidate) => candidate.id === reportId)
  const { data, error, loading, reload } = useAsync((signal) => report.load(signal), [reportId])

  return (
    <>
      <div className="segmented" role="group" aria-label="Report">
        {REPORTS.map((candidate) => (
          <button
            key={candidate.id}
            type="button"
            aria-pressed={candidate.id === reportId}
            onClick={() => setReportId(candidate.id)}
          >
            {candidate.label}
          </button>
        ))}
      </div>

      <p className="hint">{report.description}</p>

      {loading && <Loading label="Loading report…" />}
      {error && <ErrorAlert error={error} onRetry={reload} />}

      {data && !loading && (
        <>
          <p className="hint">
            {plural(data.items.length, 'provider')} · evaluated as of {formatDate(data.asOfDate)} (US Eastern)
          </p>

          {data.items.length === 0 && <EmptyState>{report.empty}</EmptyState>}

          {data.items.map((row) => (
            <section className="report-row" key={row.providerId}>
              <h3>
                {row.providerName} <span className="muted">· {row.county} County</span>
                {row.hasNoValidLicense && <span className="badge badge-warning">No valid license</span>}
              </h3>
              <LicenseTable licenses={row.licenses} emptyMessage={report.emptyLicenses} />
            </section>
          ))}
        </>
      )}
    </>
  )
}
