import { useState } from 'react'
import { deleteProvider } from '../api/providers.js'
import Modal from './Modal.jsx'

/**
 * The confirmation step before a provider is deleted. Nothing is sent until the user confirms.
 *
 * @param {object} props
 * @param {{ providerId: number, providerName: string, county: string }} props.provider
 * @param {() => void} props.onDeleted
 * @param {() => void} props.onCancel
 */
export default function DeleteProviderDialog({ provider, onDeleted, onCancel }) {
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState('')

  async function onConfirm() {
    setDeleting(true)
    setError('')
    try {
      await deleteProvider(provider.providerId)
      onDeleted()
    } catch (apiError) {
      setError(apiError.message)
      setDeleting(false)
    }
  }

  return (
    <Modal title="Delete Provider?" onCancel={onCancel} busy={deleting}>
      <p>
        <strong>{provider.providerName}</strong> ({provider.county} County) will be removed from the provider list
        and from all reports, together with its licenses.
      </p>
      <p className="hint">
        The record is not erased. It is kept for audit and remains visible under “Deleted (Audit)”.
      </p>

      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}

      <div className="modal-actions">
        <button type="button" onClick={onCancel} disabled={deleting}>
          Cancel
        </button>
        <button type="button" className="danger" onClick={onConfirm} disabled={deleting}>
          {deleting ? 'Deleting…' : 'Delete Provider'}
        </button>
      </div>
    </Modal>
  )
}
