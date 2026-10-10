import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import App from '../App.jsx'
import { mockApi } from '../test/mockApi.js'
import ReportsPage from './ReportsPage.jsx'

const license = (overrides) => ({
  licenseId: 1,
  providerId: 1,
  licenseNumber: 'CCLC-10001',
  licenseStatus: 'Active',
  issueDate: null,
  expirationDate: '2031-06-30',
  isExpired: false,
  isCurrentlyValid: true,
  effectiveStatus: 'Active',
  ...overrides,
})

const activeReport = {
  asOfDate: '2026-10-10',
  items: [
    { providerId: 1, providerName: 'Peachtree Early Learning Center', county: 'Fulton', status: 'Active', hasNoValidLicense: false, licenses: [license()] },
    { providerId: 5, providerName: 'Athens Bright Beginnings', county: 'Clarke', status: 'Active', hasNoValidLicense: true, licenses: [] },
  ],
}

const expiredReport = {
  asOfDate: '2026-10-10',
  items: [
    {
      providerId: 3,
      providerName: 'Savannah Coastal Kids Academy',
      county: 'Chatham',
      status: 'Active',
      hasNoValidLicense: true,
      licenses: [
        license({ licenseId: 5, licenseNumber: 'CCLC-10005', expirationDate: '2025-03-31', isExpired: true, isCurrentlyValid: false, effectiveStatus: 'Expired' }),
      ],
    },
  ],
}

function fakeReports(overrides = {}) {
  return mockApi((request) => {
    if (request.url === '/api/reports/active-providers-active-licenses') return overrides.active ?? { body: activeReport }
    if (request.url === '/api/reports/active-providers-expired-licenses') return overrides.expired ?? { body: expiredReport }
    return { status: 404, body: { title: 'Not found', status: 404 } }
  })
}

describe('ReportsPage', () => {
  it('shows active providers with their valid licenses, including a provider that has none', async () => {
    fakeReports()
    render(<ReportsPage />)

    expect(await screen.findByRole('heading', { name: /Peachtree Early Learning Center/ })).toBeInTheDocument()
    expect(screen.getByText('CCLC-10001')).toBeInTheDocument()
    expect(screen.getByText(/2 providers · evaluated as of Oct 10, 2026/)).toBeInTheDocument()

    const athens = screen.getByRole('heading', { name: /Athens Bright Beginnings/ }).closest('section')
    expect(within(athens).getByText('No valid license')).toBeInTheDocument()
    expect(within(athens).getByText('No valid license.')).toBeInTheDocument()
  })

  it('switches to the expired-license scenario and loads it from its own endpoint', async () => {
    const fetchMock = fakeReports()
    render(<ReportsPage />)
    const user = userEvent.setup()
    await screen.findByRole('heading', { name: /Peachtree/ })

    await user.click(screen.getByRole('button', { name: 'Active Providers with Expired Licenses' }))

    expect(await screen.findByRole('heading', { name: /Savannah Coastal Kids Academy/ })).toBeInTheDocument()
    expect(screen.getByText('Expired (status still says Active)')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /Peachtree/ })).not.toBeInTheDocument()
    expect(fetchMock.calls().map((request) => request.url)).toEqual([
      '/api/reports/active-providers-active-licenses',
      '/api/reports/active-providers-expired-licenses',
    ])
  })

  it('shows an empty state when no provider matches the scenario', async () => {
    fakeReports({ expired: { body: { asOfDate: '2026-10-10', items: [] } } })
    render(<ReportsPage />)
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'Active Providers with Expired Licenses' }))

    expect(await screen.findByText('No active provider has an expired license.')).toBeInTheDocument()
  })

  it('shows the error state when the report cannot be loaded', async () => {
    fakeReports({ active: { status: 500, body: { title: 'Error', status: 500 } } })
    render(<ReportsPage />)

    expect(await screen.findByRole('alert')).toHaveTextContent('The API could not complete the request (500).')
    expect(screen.getByRole('button', { name: 'Try Again' })).toBeInTheDocument()
  })
})

describe('ReportsPage issue license', () => {
  it('offers the button only on providers the API flags as having no valid license', async () => {
    fakeReports()
    render(<ReportsPage />)

    expect(await screen.findByRole('button', { name: 'Issue License to Athens Bright Beginnings' })).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Issue License to Peachtree Early Learning Center' }),
    ).not.toBeInTheDocument()
  })

  it('issues a license from the expired-license report and reloads it', async () => {
    const fetchMock = mockApi((request) => {
      if (request.method === 'POST') {
        return { status: 201, body: license({ licenseId: 13, providerId: 3, licenseNumber: 'CCLC-10013' }) }
      }
      if (request.url === '/api/reports/active-providers-active-licenses') return { body: activeReport }
      return { body: expiredReport }
    })
    render(<ReportsPage />)
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'Active Providers with Expired Licenses' }))
    await user.click(await screen.findByRole('button', { name: 'Issue License to Savannah Coastal Kids Academy' }))
    await user.click(within(screen.getByRole('dialog', { name: 'Issue License' })).getByRole('button', { name: 'Issue License' }))

    expect(await screen.findByText('License CCLC-10013 was issued to Savannah Coastal Kids Academy.')).toBeInTheDocument()
    expect(fetchMock.calls('POST').map((request) => request.url)).toEqual(['/api/providers/3/licenses'])
    // One load when the tab opened, one more after the license was issued.
    await waitFor(() =>
      expect(
        fetchMock.calls('GET').filter((request) => request.url === '/api/reports/active-providers-expired-licenses'),
      ).toHaveLength(2),
    )
  })
})

describe('ReportsPage text', () => {
  it('uses the agreed page title, report name, and description', async () => {
    mockApi((request) => {
      if (request.url.startsWith('/api/reports')) return { body: activeReport }
      if (request.url === '/api/lookups/counties') return { body: [] }
      if (request.url === '/api/lookups/statuses') return { body: { providerStatuses: [], licenseStatuses: [] } }
      return { body: { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 } }
    })
    render(<App />)
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'License Reports' }))

    expect(screen.getByRole('heading', { level: 1, name: 'License Reports' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Active Providers and License Status' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByText('All active providers and their current license status.')).toBeInTheDocument()
  })
})

describe('App navigation', () => {
  it('makes the license reports, dashboard, and audit views reachable from the main menu', async () => {
    mockApi((request) => {
      if (request.url.startsWith('/api/providers')) return { body: { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 } }
      if (request.url === '/api/lookups/counties') return { body: [] }
      if (request.url === '/api/lookups/statuses') return { body: { providerStatuses: [], licenseStatuses: [] } }
      if (request.url === '/api/reports/active-providers-active-licenses') return { body: activeReport }
      if (request.url === '/api/audit/providers/deleted') {
        return {
          body: [
            { providerId: 12, providerName: 'Albany Tiny Tots', county: 'Dougherty', status: 'Inactive', createdUtc: '2024-09-03T12:35:00Z', createdBy: 'seed', deletedUtc: '2025-01-30T16:10:00Z', deletedBy: 'seed', licenseCount: 0 },
          ],
        }
      }
      return { status: 404, body: { title: 'Not found', status: 404 } }
    })
    render(<App />)
    const user = userEvent.setup()

    expect(screen.getByRole('heading', { level: 1, name: 'Providers' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'License Reports' }))
    expect(await screen.findByRole('heading', { name: /Peachtree Early Learning Center/ })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Deleted (Audit)' }))
    expect(await screen.findByRole('rowheader', { name: 'Albany Tiny Tots' })).toBeInTheDocument()
  })
})
