import { useState } from 'react'
import { getActiveProvidersWithActiveLicenses, getActiveProvidersWithExpiredLicenses } from '../api/providers.js'
import { EmptyState, ErrorAlert, Loading } from '../components/Feedback.jsx'
import IssueLicenseDialog from '../components/IssueLicenseDialog.jsx'
import LicenseTable from '../components/LicenseTable.jsx'
import { formatDate, plural } from '../format.js'
import { useAsync } from '../hooks/useAsync.js'

// The two required license scenarios. Each is one API call; the API decides what is valid or expired.
const REPORTS = [
  {
    id: 'active',
    label: 'Active Providers and License Status',
    description: 'All active providers and their current license status.',
    load: getActiveProvidersWithActiveLicenses,
    emptyLicenses: 'No valid license.',
    empty: 'There are no active providers.',
  },
  {
    id: 'expired',
    label: 'Active Providers with Expired Licenses',
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
  const [issuing, setIssuing] = useState(null)
  const [notice, setNotice] = useState('')

  function onIssued(license) {
    setNotice(`License ${license.licenseNumber} was issued to ${issuing.providerName}.`)
    setIssuing(null)
    reload()
  }

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

      {notice && (
        <p className="notice" role="status">
          {notice}
        </p>
      )}

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
              {/* The API reports who has no valid license; it also refuses to issue one to anyone else. */}
              {row.hasNoValidLicense && (
                <button
                  type="button"
                  className="row-action"
                  aria-label={`Issue License to ${row.providerName}`}
                  onClick={() => setIssuing(row)}
                >
                  Issue License
                </button>
              )}
              <LicenseTable licenses={row.licenses} emptyMessage={report.emptyLicenses} />
            </section>
          ))}
        </>
      )}

      {issuing && <IssueLicenseDialog provider={issuing} onIssued={onIssued} onCancel={() => setIssuing(null)} />}
    </>
  )
}
