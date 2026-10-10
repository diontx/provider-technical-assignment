import { getProvider } from '../api/providers.js'
import { formatDate, formatDateTime } from '../format.js'
import { useAsync } from '../hooks/useAsync.js'
import { ErrorAlert, Loading, StatusBadge } from './Feedback.jsx'
import LicenseTable from './LicenseTable.jsx'
import Modal from './Modal.jsx'

/**
 * The selected provider and its licenses, loaded from GET /api/providers/{id} and shown in a modal.
 * @param {{ providerId: number, onClose: () => void }} props
 */
export default function ProviderDetail({ providerId, onClose }) {
  const { data: provider, error, loading, reload } = useAsync((signal) => getProvider(providerId, signal), [providerId])

  return (
    <Modal title={provider ? provider.providerName : 'Provider Licenses'} onCancel={onClose} wide showClose>
      {loading && !provider && <Loading label="Loading licenses…" />}
      {error && <ErrorAlert error={error} onRetry={reload} />}

      {provider && (
        <>
          <dl className="facts">
            <div>
              <dt>County</dt>
              <dd>{provider.county}</dd>
            </div>
            <div>
              <dt>Provider Status</dt>
              <dd>
                <StatusBadge value={provider.status} />
              </dd>
            </div>
            <div>
              <dt>Created</dt>
              <dd>{formatDateTime(provider.createdUtc)}</dd>
            </div>
            <div>
              <dt>Last Changed</dt>
              <dd>{provider.modifiedUtc ? formatDateTime(provider.modifiedUtc) : 'Never'}</dd>
            </div>
          </dl>

          {provider.status === 'Active' && !provider.hasValidLicense && (
            <p className="warning" role="note">
              This provider is Active but has no currently valid license.
            </p>
          )}

          <h3>Licenses</h3>
          <p className="hint">Validity is evaluated as of {formatDate(provider.asOfDate)} (US Eastern).</p>
          <LicenseTable licenses={provider.licenses} />
        </>
      )}
    </Modal>
  )
}
