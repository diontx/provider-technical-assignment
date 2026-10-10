import { describe, expect, it, vi } from 'vitest'
import { mockApi } from '../test/mockApi.js'
import { ApiError, request, toQuery } from './client.js'

/** Runs a request that is expected to fail and returns the error. */
async function failure(promise) {
  try {
    await promise
  } catch (error) {
    return error
  }
  throw new Error('Expected the request to fail.')
}

describe('request', () => {
  it('calls the API under /api and returns the parsed JSON', async () => {
    const fetchMock = mockApi(() => ({ body: { providerId: 7 } }))

    const result = await request('/providers/7')

    expect(result).toEqual({ providerId: 7 })
    expect(fetchMock).toHaveBeenCalledWith('/api/providers/7', expect.objectContaining({ method: 'GET' }))
  })

  it('sends a JSON body with the content type header', async () => {
    const fetchMock = mockApi(() => ({ status: 201, body: {} }))

    await request('/providers', { method: 'POST', body: { providerName: 'New' } })

    const [, init] = fetchMock.mock.calls[0]
    expect(init.method).toBe('POST')
    expect(init.body).toBe('{"providerName":"New"}')
    expect(init.headers['Content-Type']).toBe('application/json')
  })

  it('returns null for 204 No Content', async () => {
    mockApi(() => ({ status: 204 }))

    expect(await request('/providers/7', { method: 'DELETE' })).toBeNull()
  })

  it('turns a validation response into one message per field, keyed like the form fields', async () => {
    mockApi(() => ({
      status: 400,
      body: {
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: {
          providerName: ['Provider name is required.'],
          Status: ['Status is required.', 'Status must be one of: Active, Inactive, Pending.'],
        },
      },
    }))

    const error = await failure(request('/providers', { method: 'POST', body: {} }))

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(400)
    expect(error.message).toBe('Please correct the highlighted fields.')
    expect(error.fieldErrors).toEqual({
      providerName: 'Provider name is required.',
      status: 'Status is required. Status must be one of: Active, Inactive, Pending.',
    })
  })

  it.each([
    [404, 'Provider 11 was not found.'],
    [409, 'A provider with this name already exists in this county.'],
  ])('uses the detail from a %i problem response as the message', async (status, detail) => {
    mockApi(() => ({ status, body: { title: 'Problem', status, detail } }))

    const error = await failure(request('/providers/11'))

    expect(error.status).toBe(status)
    expect(error.message).toBe(detail)
    expect(error.fieldErrors).toEqual({})
  })

  it('does not show server internals for a 500', async () => {
    mockApi(() => ({ status: 500, body: { title: 'An error occurred while processing your request.', status: 500 } }))

    const error = await failure(request('/providers'))

    expect(error.message).toBe('The API could not complete the request (500). Check that it is running, then try again.')
  })

  it('copes with an error response that is not JSON', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('Bad Gateway', { status: 502 })))

    const error = await failure(request('/providers'))

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(502)
    expect(error.message).toContain('(502)')
  })

  it('reports a network failure as an ApiError the UI can show', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    const error = await failure(request('/providers'))

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(0)
    expect(error.message).toBe('Cannot reach the API. Check that it is running, then try again.')
  })

  it('lets an aborted request through unchanged, so callers can ignore it', async () => {
    const abort = new DOMException('The operation was aborted.', 'AbortError')
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(abort))

    expect(await failure(request('/providers'))).toBe(abort)
  })
})

describe('toQuery', () => {
  it('leaves out empty values and encodes the rest', () => {
    const query = toQuery({ search: 'kids & co', status: '', countyId: undefined, page: 2, pageSize: null })

    expect(query).toBe('?search=kids+%26+co&page=2')
  })

  it('returns an empty string when there is nothing to send', () => {
    expect(toQuery({ search: '', status: '' })).toBe('')
  })
})
