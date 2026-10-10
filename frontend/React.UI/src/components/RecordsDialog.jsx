import { plural } from '../format.js'
import { useAsync } from '../hooks/useAsync.js'
import { EmptyState, ErrorAlert, Loading, StatusBadge } from './Feedback.jsx'
import LicenseTable from './LicenseTable.jsx'
import Modal from './Modal.jsx'

/**
 * Read-only list of the records behind a dashboard number.
 * The API selects the records; this dialog only displays what `load` returns.
 *
 * @param {object} props
 * @param {string} props.title
 * @param {'providers' | 'licenses'} props.kind
 * @param {(signal: AbortSignal) => Promise<object[]>} props.load
 * @param {() => void} props.onClose
 */
export default function RecordsDialog({ title, kind, load, onClose }) {
  const { data: records, error, loading, reload } = useAsync(load, [])

  return (
    <Modal title={title} onCancel={onClose} wide showClose>
      {loading && <Loading label="Loading records…" />}
      {error && <ErrorAlert error={error} onRetry={reload} />}

      {records && !loading && (
        <>
          <p className="hint">{plural(records.length, 'record')} · read-only</p>
          {kind === 'licenses' ? (
            <LicenseTable licenses={records} showProvider emptyMessage="No licenses." />
          ) : (
            <ProviderRecords providers={records} />
          )}
        </>
      )}
    </Modal>
  )
}

function ProviderRecords({ providers }) {
  if (providers.length === 0) {
    return <EmptyState>No providers.</EmptyState>
  }

  return (
    <table>
      <thead>
        <tr>
          <th scope="col">Provider</th>
          <th scope="col">County</th>
          <th scope="col">Status</th>
          <th scope="col">Licenses</th>
        </tr>
      </thead>
      <tbody>
        {providers.map((provider) => (
          <tr key={provider.providerId}>
            <th scope="row">{provider.providerName}</th>
            <td>{provider.county}</td>
            <td>
              <StatusBadge value={provider.status} />
            </td>
            <td>
              {provider.licenseCount === 0 ? (
                <span className="muted">None</span>
              ) : (
                <>
                  {provider.validLicenseCount} valid
                  {provider.expiredLicenseCount > 0 && (
                    <span className="text-expired"> · {provider.expiredLicenseCount} expired</span>
                  )}
                  <span className="muted"> of {provider.licenseCount}</span>
                </>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
