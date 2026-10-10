const dateFormat = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeZone: 'UTC' })
const dateTimeFormat = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' })

/**
 * Formats a calendar date from the API ("2030-08-31"). It is read as UTC and printed as UTC,
 * so the day never shifts with the viewer's time zone.
 * @param {string | null | undefined} isoDate
 */
export function formatDate(isoDate) {
  return isoDate ? dateFormat.format(new Date(`${isoDate}T00:00:00Z`)) : ''
}

/**
 * Formats an audit timestamp from the API (UTC, "2024-01-15T14:00:00Z") in the viewer's local time.
 * @param {string | null | undefined} isoDateTime
 */
export function formatDateTime(isoDateTime) {
  return isoDateTime ? dateTimeFormat.format(new Date(isoDateTime)) : ''
}

/** @param {number} count @param {string} singular */
export function plural(count, singular) {
  return `${count} ${singular}${count === 1 ? '' : 's'}`
}
