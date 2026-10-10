// Client-side checks for the issue-license form. As with the provider form, these give
// immediate feedback only; the API applies the same rules and is the authority.
//
// Dates are the "yyyy-MM-dd" strings an <input type="date"> produces, which is also the
// format the API uses. That format sorts as text, so dates are compared as strings.

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/

/**
 * Today's date on the user's machine, as "yyyy-MM-dd".
 * The API decides "today" in US Eastern time, so near midnight it may disagree and reject the date.
 * @param {Date} [now]
 */
export function todayIso(now = new Date()) {
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${now.getFullYear()}-${month}-${day}`
}

/**
 * The default expiration date: one year after the issue date. February 29 becomes
 * February 28, matching how the API calculates its own default.
 * @param {string} isoDate
 * @returns {string} "yyyy-MM-dd", or "" when the input is not a complete date.
 */
export function addOneYear(isoDate) {
  if (!ISO_DATE.test(isoDate ?? '')) {
    return ''
  }

  const [year, month, day] = isoDate.split('-').map(Number)
  const result = new Date(Date.UTC(year + 1, month - 1, day))
  if (result.getUTCMonth() !== month - 1) {
    // The day does not exist next year (February 29): step back to the last day of the month.
    result.setUTCDate(0)
  }
  return result.toISOString().slice(0, 10)
}

/**
 * @param {{ issueDate?: string, expirationDate?: string }} values Raw form values.
 * @param {{ today: string }} options
 * @returns {Record<string, string>} Message per invalid field. Empty when the form is valid.
 */
export function validateLicense(values, { today }) {
  const errors = {}
  const issueDate = values.issueDate ?? ''
  const expirationDate = values.expirationDate ?? ''

  if (!ISO_DATE.test(issueDate)) {
    errors.issueDate = 'Issue date is required.'
  } else if (issueDate > today) {
    errors.issueDate = 'Issue date cannot be in the future.'
  }

  if (!ISO_DATE.test(expirationDate)) {
    errors.expirationDate = 'Expiration date is required.'
  } else if (ISO_DATE.test(issueDate) && expirationDate < addOneYear(issueDate)) {
    // The minimum term is one year, which is also the default the form fills in.
    errors.expirationDate = 'Expiration date must be at least one year after the issue date.'
  } else if (expirationDate < today) {
    errors.expirationDate = 'Expiration date cannot be in the past.'
  }

  return errors
}
