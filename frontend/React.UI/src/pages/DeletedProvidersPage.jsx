import { getDeletedProviders } from '../api/providers.js'
import { EmptyState, ErrorAlert, Loading, StatusBadge } from '../components/Feedback.jsx'
import { formatDateTime } from '../format.js'
import { useAsync } from '../hooks/useAsync.js'

/**
 * Audit view of soft-deleted providers, from the one API route that returns them.
 * Read-only: restoring a provider is out of scope.
 */
export default function DeletedProvidersPage() {
  const { data, error, loading, reload } = useAsync((signal) => getDeletedProviders(signal), [])

  return (
    <>
      <p className="hint">
        Deleted providers are hidden everywhere else in the application. Their records, and their licenses, remain in
        the database and are listed here for audit and troubleshooting.
      </p>

      {loading && <Loading label="Loading deleted providers…" />}
      {error && <ErrorAlert error={error} onRetry={reload} />}
      {data && data.length === 0 && <EmptyState>No provider has been deleted.</EmptyState>}

      {data && data.length > 0 && (
        <table>
          <thead>
            <tr>
              <th scope="col">Provider</th>
              <th scope="col">County</th>
              <th scope="col">Status when deleted</th>
              <th scope="col">Licenses kept</th>
              <th scope="col">Deleted</th>
              <th scope="col">Deleted by</th>
            </tr>
          </thead>
          <tbody>
            {data.map((provider) => (
              <tr key={provider.providerId}>
                <th scope="row">{provider.providerName}</th>
                <td>{provider.county}</td>
                <td>
                  <StatusBadge value={provider.status} />
                </td>
                <td>{provider.licenseCount}</td>
                <td>{formatDateTime(provider.deletedUtc)}</td>
                <td>{provider.deletedBy}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  )
}
