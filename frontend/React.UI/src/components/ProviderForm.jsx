import { useId, useState } from 'react'
import { createProvider, updateProvider } from '../api/providers.js'
import { hasErrors, toProviderInput, validateProvider } from '../validation/providerValidation.js'
import Modal from './Modal.jsx'

/**
 * Create or edit a provider.
 *
 * @param {object} props
 * @param {object | null} props.provider The provider to edit, or null to create one.
 * @param {{ countyId: number, name: string }[]} props.counties
 * @param {string[]} props.statuses
 * @param {(saved: object) => void} props.onSaved
 * @param {() => void} props.onCancel
 */
export default function ProviderForm({ provider, counties, statuses, onSaved, onCancel }) {
  const isEdit = provider !== null
  const formId = useId()

  const [values, setValues] = useState({
    providerName: provider?.providerName ?? '',
    countyId: provider?.countyId ?? '',
    status: provider?.status ?? '',
  })
  const [fieldErrors, setFieldErrors] = useState({})
  const [formError, setFormError] = useState('')
  const [saving, setSaving] = useState(false)

  function setValue(name, value) {
    setValues((current) => ({ ...current, [name]: value }))
    // Clear the message for a field as soon as the user starts correcting it.
    setFieldErrors(({ [name]: _cleared, ...rest }) => rest)
  }

  async function onSubmit(event) {
    event.preventDefault()

    const errors = validateProvider(values, { counties, statuses })
    setFieldErrors(errors)
    setFormError('')
    if (hasErrors(errors)) {
      return
    }

    setSaving(true)
    try {
      const input = toProviderInput(values)
      const saved = isEdit
        ? await updateProvider(provider.providerId, { ...input, concurrencyStamp: provider.concurrencyStamp })
        : await createProvider(input)
      onSaved(saved)
    } catch (error) {
      // The API is the authority: show whatever it rejected, beside the field where it can.
      setFieldErrors(error.fieldErrors ?? {})
      setFormError(error.message)
      setSaving(false)
    }
  }

  /** Props that tie an input to its error message for assistive technology. */
  function fieldProps(name) {
    return {
      id: `${formId}-${name}`,
      'aria-invalid': fieldErrors[name] ? true : undefined,
      'aria-describedby': fieldErrors[name] ? `${formId}-${name}-error` : undefined,
      disabled: saving,
    }
  }

  function fieldError(name) {
    return (
      fieldErrors[name] && (
        <p className="field-error" id={`${formId}-${name}-error`}>
          {fieldErrors[name]}
        </p>
      )
    )
  }

  return (
    <Modal title={isEdit ? 'Edit Provider' : 'Add Provider'} onCancel={onCancel} busy={saving}>
      {/* noValidate: the messages come from validateProvider and the API, not the browser. */}
      <form onSubmit={onSubmit} noValidate>
        {formError && (
          <p className="form-error" role="alert">
            {formError}
          </p>
        )}

        <div className="field">
          <label htmlFor={`${formId}-providerName`}>Provider Name</label>
          <input
            {...fieldProps('providerName')}
            type="text"
            value={values.providerName}
            autoComplete="off"
            onChange={(event) => setValue('providerName', event.target.value)}
          />
          {fieldError('providerName')}
        </div>

        <div className="field">
          <label htmlFor={`${formId}-countyId`}>County</label>
          <select
            {...fieldProps('countyId')}
            value={values.countyId}
            onChange={(event) => setValue('countyId', event.target.value)}
          >
            <option value="">Select a county</option>
            {counties.map((county) => (
              <option key={county.countyId} value={county.countyId}>
                {county.name}
              </option>
            ))}
          </select>
          {fieldError('countyId')}
        </div>

        <div className="field">
          <label htmlFor={`${formId}-status`}>Status</label>
          <select {...fieldProps('status')} value={values.status} onChange={(event) => setValue('status', event.target.value)}>
            <option value="">Select a status</option>
            {statuses.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
          {fieldError('status')}
        </div>

        <div className="modal-actions">
          <button type="button" onClick={onCancel} disabled={saving}>
            Cancel
          </button>
          <button type="submit" className="primary" disabled={saving}>
            {saving ? 'Saving…' : isEdit ? 'Save Changes' : 'Add Provider'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
