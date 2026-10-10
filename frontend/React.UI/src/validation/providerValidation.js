// Client-side checks for the provider form. They exist to give immediate feedback and to
// avoid a round trip for obvious mistakes. The API repeats every rule and is the authority:
// anything it rejects is shown through the same per-field messages.

export const NAME_MAX_LENGTH = 200

/**
 * @param {{ providerName?: string, countyId?: string | number, status?: string }} values Raw form values.
 * @param {{ counties: { countyId: number }[], statuses: string[] }} options Allowed values, loaded from the API.
 * @returns {Record<string, string>} Message per invalid field. Empty when the form is valid.
 */
export function validateProvider(values, { counties, statuses }) {
  const errors = {}

  const name = (values.providerName ?? '').trim()
  if (!name) {
    errors.providerName = 'Provider name is required.'
  } else if (name.length > NAME_MAX_LENGTH) {
    errors.providerName = `Provider name must be ${NAME_MAX_LENGTH} characters or fewer.`
  }

  if (values.countyId === undefined || values.countyId === null || values.countyId === '') {
    errors.countyId = 'County is required.'
  } else if (!counties.some((county) => county.countyId === Number(values.countyId))) {
    errors.countyId = 'Select a valid Georgia county.'
  }

  if (!values.status) {
    errors.status = 'Status is required.'
  } else if (!statuses.includes(values.status)) {
    errors.status = `Status must be one of: ${statuses.join(', ')}.`
  }

  return errors
}

/** @param {Record<string, string>} errors */
export function hasErrors(errors) {
  return Object.keys(errors).length > 0
}

/**
 * The request body for the API, built from validated form values.
 * @param {{ providerName: string, countyId: string | number, status: string }} values
 */
export function toProviderInput(values) {
  return {
    providerName: values.providerName.trim(),
    countyId: Number(values.countyId),
    status: values.status,
  }
}
