import { useState } from 'react'
import { getCounties, getStatuses, listProviders } from '../api/providers.js'
import DeleteProviderDialog from '../components/DeleteProviderDialog.jsx'
import IssueLicenseDialog from '../components/IssueLicenseDialog.jsx'
import { EmptyState, ErrorAlert, Loading, StatusBadge } from '../components/Feedback.jsx'
import ProviderDetail from '../components/ProviderDetail.jsx'
import ProviderForm from '../components/ProviderForm.jsx'
import { plural } from '../format.js'
import { useAsync, useDebouncedValue } from '../hooks/useAsync.js'

const PAGE_SIZE = 10
const NO_FILTERS = { search: '', status: '', countyId: '' }

/** Provider list with search, filters, paging, and the create, edit, delete, and license workflows. */
export default function ProvidersPage() {
  const [filters, setFilters] = useState(NO_FILTERS)
  const [page, setPage] = useState(1)
  const search = useDebouncedValue(filters.search, 300)

  const [selectedId, setSelectedId] = useState(null)
  const [editing, setEditing] = useState(undefined) // undefined: closed, null: adding, object: editing
  const [deleting, setDeleting] = useState(null)
  const [issuing, setIssuing] = useState(null)
  const [notice, setNotice] = useState('')

  const lookups = useAsync(async (signal) => {
    const [counties, statuses] = await Promise.all([getCounties(signal), getStatuses(signal)])
    return { counties, statuses: statuses.providerStatuses }
  }, [])

  const providers = useAsync(
    (signal) =>
      listProviders({ search, status: filters.status, countyId: filters.countyId, page, pageSize: PAGE_SIZE }, signal),
    [search, filters.status, filters.countyId, page],
  )

  const isFiltered = Boolean(search || filters.status || filters.countyId)

  function setFilter(name, value) {
    setFilters((current) => ({ ...current, [name]: value }))
    setPage(1)
  }

  function clearFilters() {
    setFilters(NO_FILTERS)
    setPage(1)
  }

  function onSaved(saved) {
    setNotice(`${saved.providerName} was ${editing ? 'updated' : 'added'}.`)
    setEditing(undefined)
    providers.reload()
  }

  function onIssued(license) {
    setNotice(`License ${license.licenseNumber} was issued to ${issuing.providerName}.`)
    setIssuing(null)
    providers.reload()
  }

  function onDeleted() {
    setNotice(`${deleting.providerName} was deleted. The record is kept for audit.`)
    setDeleting(null)
    // If that was the last row on this page, step back so the user is not left on an empty page.
    if (providers.data?.items.length === 1 && page > 1) {
      setPage(page - 1)
    } else {
      providers.reload()
    }
  }

  return (
    <>
      <div className="toolbar">
        <div className="filters">
          <label>
            Search by Name
            <input
              type="search"
              value={filters.search}
              placeholder="Provider Name"
              onChange={(event) => setFilter('search', event.target.value)}
            />
          </label>
          <label>
            Status
            <select value={filters.status} onChange={(event) => setFilter('status', event.target.value)}>
              <option value="">All statuses</option>
              {lookups.data?.statuses.map((status) => (
                <option key={status} value={status}>
                  {status}
                </option>
              ))}
            </select>
          </label>
          <label>
            County
            <select value={filters.countyId} onChange={(event) => setFilter('countyId', event.target.value)}>
              <option value="">All counties</option>
              {lookups.data?.counties.map((county) => (
                <option key={county.countyId} value={county.countyId}>
                  {county.name}
                </option>
              ))}
            </select>
          </label>
          {isFiltered && (
            <button type="button" onClick={clearFilters}>
              Clear Filters
            </button>
          )}
        </div>

        {/* Adding needs the county and status lists, so the button waits for them. */}
        <button type="button" className="primary" disabled={!lookups.data} onClick={() => setEditing(null)}>
          Add Provider
        </button>
      </div>

      {notice && (
        <p className="notice" role="status">
          {notice}
        </p>
      )}
      {lookups.error && <ErrorAlert error={lookups.error} onRetry={lookups.reload} />}
      {providers.error && <ErrorAlert error={providers.error} onRetry={providers.reload} />}
      {providers.loading && !providers.data && <Loading label="Loading providers…" />}

      {providers.data && providers.data.items.length === 0 && (
        <EmptyState>
          {isFiltered ? 'No providers match these filters.' : 'There are no providers yet. Add the first one.'}
        </EmptyState>
      )}

      {providers.data && providers.data.items.length > 0 && (
        <>
          <table className={providers.loading ? 'is-refreshing' : undefined} aria-busy={providers.loading}>
            <thead>
              <tr>
                <th scope="col">Provider</th>
                <th scope="col">County</th>
                <th scope="col">Status</th>
                <th scope="col">Licenses</th>
                <th scope="col">
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {providers.data.items.map((provider) => (
                <tr key={provider.providerId}>
                  <th scope="row">{provider.providerName}</th>
                  <td>{provider.county}</td>
                  <td>
                    <StatusBadge value={provider.status} />
                  </td>
                  <td>
                    <LicenseSummary provider={provider} />
                  </td>
                  <td className="actions">
                    <button
                      type="button"
                      aria-label={`View licenses for ${provider.providerName}`}
                      onClick={() => setSelectedId(provider.providerId)}
                    >
                      Licenses
                    </button>
                    {/* Offered when the API counts no valid license: none at all, expired, or suspended. */}
                    {provider.validLicenseCount === 0 && (
                      <button
                        type="button"
                        aria-label={`Issue License to ${provider.providerName}`}
                        onClick={() => setIssuing(provider)}
                      >
                        Issue License
                      </button>
                    )}
                    <button
                      type="button"
                      aria-label={`Edit ${provider.providerName}`}
                      disabled={!lookups.data}
                      onClick={() => setEditing(provider)}
                    >
                      Edit
                    </button>
                    <button
                      type="button"
                      className="danger-text"
                      aria-label={`Delete ${provider.providerName}`}
                      onClick={() => setDeleting(provider)}
                    >
                      Delete
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <nav className="pager" aria-label="Pages">
            <button type="button" disabled={page <= 1} onClick={() => setPage(page - 1)}>
              Previous
            </button>
            <span>
              Page {providers.data.page} of {providers.data.totalPages} · {plural(providers.data.totalCount, 'provider')}
            </span>
            <button type="button" disabled={page >= providers.data.totalPages} onClick={() => setPage(page + 1)}>
              Next
            </button>
          </nav>
        </>
      )}

      {selectedId !== null && <ProviderDetail providerId={selectedId} onClose={() => setSelectedId(null)} />}

      {editing !== undefined && (
        <ProviderForm
          provider={editing}
          counties={lookups.data.counties}
          statuses={lookups.data.statuses}
          onSaved={onSaved}
          onCancel={() => setEditing(undefined)}
        />
      )}

      {issuing && <IssueLicenseDialog provider={issuing} onIssued={onIssued} onCancel={() => setIssuing(null)} />}

      {deleting && <DeleteProviderDialog provider={deleting} onDeleted={onDeleted} onCancel={() => setDeleting(null)} />}
    </>
  )
}

/** Counts come from the API, which applies the expiration-date rule; nothing is computed here. */
function LicenseSummary({ provider }) {
  if (provider.licenseCount === 0) {
    return <span className="muted">None</span>
  }

  return (
    <>
      {provider.validLicenseCount} valid
      {provider.expiredLicenseCount > 0 && <span className="text-expired"> · {provider.expiredLicenseCount} expired</span>}
      <span className="muted"> of {provider.licenseCount}</span>
    </>
  )
}
