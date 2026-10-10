const API_BASE = '/api'

/**
 * A failed API call, already turned into something the UI can show.
 * `fieldErrors` maps a form field name to its message, taken from the API's validation response.
 */
export class ApiError extends Error {
  /**
   * @param {string} message
   * @param {{ status?: number, fieldErrors?: Record<string, string> }} [details]
   */
  constructor(message, { status = 0, fieldErrors = {} } = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

/**
 * Calls the C# API and returns the parsed JSON body (or null for 204 No Content).
 * Every failure is thrown as an ApiError, except an aborted request.
 *
 * @param {string} path Path under /api, starting with a slash.
 * @param {{ method?: string, body?: unknown, signal?: AbortSignal }} [options]
 */
export async function request(path, { method = 'GET', body, signal } = {}) {
  const headers = { Accept: 'application/json' }
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }

  let response
  try {
    response = await fetch(`${API_BASE}${path}`, {
      method,
      headers,
      signal,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch (error) {
    if (error?.name === 'AbortError') {
      throw error
    }
    throw new ApiError('Cannot reach the API. Check that it is running, then try again.')
  }

  if (response.ok) {
    return response.status === 204 ? null : response.json()
  }

  throw await toApiError(response)
}

/**
 * Builds a query string, leaving out empty values.
 * @param {Record<string, string | number | null | undefined>} params
 */
export function toQuery(params) {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') {
      query.set(key, String(value))
    }
  }
  const text = query.toString()
  return text ? `?${text}` : ''
}

/** Reads an RFC 7807 problem response from the API. */
async function toApiError(response) {
  let problem = null
  try {
    problem = await response.json()
  } catch {
    // Not JSON: for example the dev proxy answering because the API is down.
  }

  const fieldErrors = toFieldErrors(problem?.errors)
  const hasFieldErrors = Object.keys(fieldErrors).length > 0

  let message
  if (problem?.detail) {
    message = problem.detail
  } else if (hasFieldErrors) {
    message = 'Please correct the highlighted fields.'
  } else if (response.status >= 500 || !problem) {
    message = `The API could not complete the request (${response.status}). Check that it is running, then try again.`
  } else {
    message = problem.title ?? `The request failed (${response.status}).`
  }

  return new ApiError(message, { status: response.status, fieldErrors })
}

/** `{ ProviderName: ['a', 'b'] }` becomes `{ providerName: 'a b' }`. */
function toFieldErrors(errors) {
  const result = {}
  for (const [key, messages] of Object.entries(errors ?? {})) {
    const name = key.replace(/^\$\./, '')
    if (name) {
      result[name[0].toLowerCase() + name.slice(1)] = [].concat(messages).join(' ')
    }
  }
  return result
}
