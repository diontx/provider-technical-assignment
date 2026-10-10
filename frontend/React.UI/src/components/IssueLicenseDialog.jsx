import { useId, useState } from 'react'
import { issueLicense } from '../api/providers.js'
import { addOneYear, todayIso, validateLicense } from '../validation/licenseValidation.js'
import { hasErrors } from '../validation/providerValidation.js'
import Modal from './Modal.jsx'

/**
 * Issue a new license to a provider that has no valid one.
 * The license number is not entered: the API generates it when the license is saved.
 *
 * @param {object} props
 * @param {{ providerId: number, providerName: string, county: string }} props.provider
 * @param {(license: object) => void} props.onIssued
 * @param {() => void} props.onCancel
 */
export default function IssueLicenseDialog({ provider, onIssued, onCancel }) {
  const formId = useId()
  const [today] = useState(todayIso)

  const [values, setValues] = useState({ issueDate: today, expirationDate: addOneYear(today) })
  // Until the user picks an expiration date themselves, it follows the issue date.
  const [expirationEdited, setExpirationEdited] = useState(false)
  const [fieldErrors, setFieldErrors] = useState({})
  const [formError, setFormError] = useState('')
  const [saving, setSaving] = useState(false)

  /** Dates are checked as they are changed, so a problem shows at once rather than on submit. */
  function update(next) {
    setValues(next)
    setFieldErrors(validateLicense(next, { today }))
    setFormError('')
  }

  function onIssueDateChange(issueDate) {
    update({
      issueDate,
      expirationDate: expirationEdited ? values.expirationDate : addOneYear(issueDate),
    })
  }

  function onExpirationDateChange(expirationDate) {
    setExpirationEdited(true)
    update({ ...values, expirationDate })
  }

  async function onSubmit(event) {
    event.preventDefault()

    const errors = validateLicense(values, { today })
    setFieldErrors(errors)
    setFormError('')
    if (hasErrors(errors)) {
      return
    }

    setSaving(true)
    try {
      onIssued(await issueLicense(provider.providerId, values))
    } catch (error) {
      // The API applies the same date rules and decides "today" itself; show what it rejected.
      setFieldErrors(error.fieldErrors ?? {})
      setFormError(error.message)
      setSaving(false)
    }
  }

  function fieldProps(name) {
    return {
      id: `${formId}-${name}`,
      'aria-invalid': fieldErrors[name] ? true : undefined,
      'aria-describedby': fieldErrors[name] ? `${formId}-${name}-error` : `${formId}-${name}-hint`,
      disabled: saving,
    }
  }

  function fieldError(name) {
    return (
      fieldErrors[name] && (
        <p className="field-error" id={`${formId}-${name}-error`} role="alert">
          {fieldErrors[name]}
        </p>
      )
    )
  }

  return (
    <Modal title="Issue License" onCancel={onCancel} busy={saving}>
      <p>
        New license for <strong>{provider.providerName}</strong> ({provider.county} County). It is issued with status
        Active.
      </p>

      <form onSubmit={onSubmit} noValidate>
        {formError && (
          <p className="form-error" role="alert">
            {formError}
          </p>
        )}

        <div className="field">
          <label htmlFor={`${formId}-licenseNumber`}>License Number</label>
          <input id={`${formId}-licenseNumber`} type="text" value="Generated automatically when issued" readOnly disabled />
        </div>

        <div className="field">
          <label htmlFor={`${formId}-issueDate`}>Issue Date</label>
          <input
            {...fieldProps('issueDate')}
            type="date"
            max={today}
            value={values.issueDate}
            onChange={(event) => onIssueDateChange(event.target.value)}
          />
          {fieldError('issueDate')}
          <p className="hint" id={`${formId}-issueDate-hint`}>
            Today or earlier.
          </p>
        </div>

        <div className="field">
          <label htmlFor={`${formId}-expirationDate`}>Expires</label>
          <input
            {...fieldProps('expirationDate')}
            type="date"
            min={addOneYear(values.issueDate) || undefined}
            value={values.expirationDate}
            onChange={(event) => onExpirationDateChange(event.target.value)}
          />
          {fieldError('expirationDate')}
          <p className="hint" id={`${formId}-expirationDate-hint`}>
            At least one year after the issue date.
          </p>
        </div>

        <div className="modal-actions">
          <button type="button" onClick={onCancel} disabled={saving}>
            Cancel
          </button>
          {/* Disabled while a date is invalid, so a license with bad dates cannot be submitted. */}
          <button type="submit" className="primary" disabled={saving || hasErrors(fieldErrors)}>
            {saving ? 'Issuing…' : 'Issue License'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
