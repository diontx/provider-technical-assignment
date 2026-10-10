import { vi } from 'vitest'

/**
 * Replaces fetch for a test. The handler receives the request and returns
 * `{ status, body }` (status defaults to 200), or throws to simulate a network failure.
 *
 * @param {(request: { method: string, url: string, body: any }) => { status?: number, body?: any } | Promise<any>} handler
 * @returns the mock, with `calls(method)` to read back the requests that were sent
 */
export function mockApi(handler) {
  const requests = []

  const fetchMock = vi.fn(async (url, init = {}) => {
    const request = {
      method: init.method ?? 'GET',
      url: String(url),
      body: init.body ? JSON.parse(init.body) : undefined,
    }
    requests.push(request)

    const { status = 200, body } = (await handler(request)) ?? {}
    const isProblem = status >= 400
    return new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'Content-Type': isProblem ? 'application/problem+json' : 'application/json' },
    })
  })

  vi.stubGlobal('fetch', fetchMock)

  fetchMock.calls = (method) => requests.filter((request) => !method || request.method === method)
  return fetchMock
}

export const counties = [
  { countyId: 44, name: 'DeKalb' },
  { countyId: 60, name: 'Fulton' },
]

export const statuses = {
  providerStatuses: ['Active', 'Inactive', 'Pending'],
  licenseStatuses: ['Active', 'Expired', 'Suspended'],
}

export function providerListItem(overrides = {}) {
  return {
    providerId: 1,
    providerName: 'Peachtree Early Learning Center',
    countyId: 60,
    county: 'Fulton',
    status: 'Active',
    createdUtc: '2024-01-15T14:00:00Z',
    modifiedUtc: null,
    licenseCount: 2,
    validLicenseCount: 2,
    expiredLicenseCount: 0,
    concurrencyStamp: 'stamp-1',
    ...overrides,
  }
}

export function page(items) {
  return { items, page: 1, pageSize: 10, totalCount: items.length, totalPages: items.length > 0 ? 1 : 0 }
}
