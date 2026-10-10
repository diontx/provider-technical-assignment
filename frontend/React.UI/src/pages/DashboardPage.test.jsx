import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, providerListItem } from '../test/mockApi.js'
import DashboardPage from './DashboardPage.jsx'

const summary = {
  asOfDate: '2026-10-10',
  totalProviders: 11,
  totalLicenses: 11,
  activeProvidersWithoutValidLicense: 3,
  activeProvidersWithExpiredLicense: 0,
  providersByStatus: [
    { name: 'Active', count: 7, key: 'Active' },
    { name: 'Inactive', count: 2, key: 'Inactive' },
    { name: 'Pending', count: 0, key: 'Pending' },
  ],
  licensesByEffectiveStatus: [
    { name: 'Valid', count: 6, key: 'Valid' },
    { name: 'Expired', count: 4, key: 'Expired' },
    { name: 'Suspended', count: 1, key: 'Suspended' },
    { name: 'Other not valid', count: 0, key: 'OtherNotValid' },
  ],
  providersByCounty: [{ name: 'Fulton', count: 1, key: 'Fulton' }],
  licensesPerProvider: [
    { providerId: 1, providerName: 'Peachtree Early Learning Center', licenseCount: 2, validLicenseCount: 2 },
    { providerId: 3, providerName: 'Savannah Coastal Kids Academy', licenseCount: 1, validLicenseCount: 0 },
  ],
  expiringWindowDays: 90,
  expiringLicenses: [
    { providerId: 9, providerName: 'Gwinnett Growing Minds', licenseNumber: 'CCLC-10010', expirationDate: '2026-11-09', daysRemaining: 30 },
  ],
}

const expiredLicense = {
  licenseId: 5,
  providerId: 3,
  providerName: 'Savannah Coastal Kids Academy',
  county: 'Chatham',
  licenseNumber: 'CCLC-10005',
  licenseStatus: 'Active',
  issueDate: '2023-04-01',
  expirationDate: '2025-03-31',
  isExpired: true,
  isCurrentlyValid: false,
  effectiveStatus: 'Expired',
}

const providerDetail = {
  ...providerListItem(),
  createdBy: 'seed',
  modifiedBy: null,
  asOfDate: '2026-10-10',
  hasValidLicense: true,
  licenses: [{ ...expiredLicense, licenseId: 1, providerId: 1, licenseNumber: 'CCLC-10001', isExpired: false, isCurrentlyValid: true }],
}

function fakeDashboard(overrides = () => undefined) {
  return mockApi((request) => {
    const override = overrides(request)
    if (override) return override

    if (request.url === '/api/dashboard/summary') return { body: summary }
    if (request.url.startsWith('/api/dashboard/providers')) return { body: [providerListItem()] }
    if (request.url.startsWith('/api/dashboard/licenses')) return { body: [expiredLicense] }
    if (request.url === '/api/providers/1') return { body: providerDetail }
    return { status: 404, body: { title: 'Not found', status: 404, detail: `No fake for ${request.url}` } }
  })
}

/** Clicks a dashboard number and returns the record dialog it opens. */
async function open(user, buttonName, dialogName) {
  await user.click(await screen.findByRole('button', { name: buttonName }))
  return screen.findByRole('dialog', { name: dialogName })
}

const urls = (fetchMock) => fetchMock.calls('GET').map((request) => request.url)

describe('DashboardPage content', () => {
  it('shows the summary cards and all four charts from the API', async () => {
    fakeDashboard()
    render(<DashboardPage />)

    expect(await screen.findByRole('button', { name: 'Providers: 11. View records' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Licenses: 11. View records' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Providers by Status' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Expired vs. Valid Licenses' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Licenses per Provider (Most)' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Providers by County (Largest)' })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Valid Licenses Expiring in the Next 90 Days' })).toBeInTheDocument()
  })

  it('draws one pie slice per non-empty license bucket and lists each with its share', async () => {
    fakeDashboard()
    const { container } = render(<DashboardPage />)
    await screen.findByRole('heading', { name: 'Expired vs. Valid Licenses' })

    expect(container.querySelectorAll('.pie svg path')).toHaveLength(3)
    expect(screen.getByRole('button', { name: 'Valid: 6 licenses. View records' })).toHaveTextContent('55%')
    expect(screen.getByRole('button', { name: 'Expired: 4 licenses. View records' })).toHaveTextContent('36%')
    // The empty catch-all bucket is left out of the chart altogether.
    expect(screen.queryByText('Other not valid')).not.toBeInTheDocument()
  })

  it('draws a full circle when every license is in one bucket', async () => {
    fakeDashboard((request) =>
      request.url === '/api/dashboard/summary'
        ? { body: { ...summary, licensesByEffectiveStatus: [{ name: 'Valid', count: 5, key: 'Valid' }, { name: 'Expired', count: 0, key: 'Expired' }] } }
        : undefined,
    )
    const { container } = render(<DashboardPage />)
    await screen.findByRole('heading', { name: 'Expired vs. Valid Licenses' })

    expect(container.querySelectorAll('.pie svg circle')).toHaveLength(1)
    expect(container.querySelectorAll('.pie svg path')).toHaveLength(0)
  })

  it('does not make a zero clickable, because there are no records behind it', async () => {
    fakeDashboard()
    render(<DashboardPage />)

    expect(await screen.findByRole('button', { name: 'Pending: 0 providers. View records' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Active providers with an expired license: 0. View records' })).toBeDisabled()
  })

  it('shows the error state when the summary cannot be loaded', async () => {
    fakeDashboard((request) =>
      request.url === '/api/dashboard/summary' ? { status: 500, body: { title: 'Error', status: 500 } } : undefined,
    )
    render(<DashboardPage />)

    expect(await screen.findByRole('alert')).toHaveTextContent('The API could not complete the request (500).')
  })
})

describe('DashboardPage record lists', () => {
  it.each([
    ['Providers: 11. View records', 'All Providers', '/api/dashboard/providers'],
    [
      'Active providers with no valid license: 3. View records',
      'Active Providers with No Valid License',
      '/api/dashboard/providers?licenseGap=NoValidLicense',
    ],
    ['Active: 7 providers. View records', 'Active Providers', '/api/dashboard/providers?status=Active'],
    ['Fulton: 1 provider. View records', 'Providers in Fulton County', '/api/dashboard/providers?county=Fulton'],
  ])('%s opens the providers from %3$s', async (buttonName, dialogName, url) => {
    const fetchMock = fakeDashboard()
    render(<DashboardPage />)
    const user = userEvent.setup()

    const dialog = await open(user, buttonName, dialogName)

    expect(await within(dialog).findByRole('rowheader', { name: 'Peachtree Early Learning Center' })).toBeInTheDocument()
    expect(within(dialog).getByText('1 record · read-only')).toBeInTheDocument()
    expect(urls(fetchMock)).toContain(url)
  })

  it.each([
    ['Licenses: 11. View records', 'All Licenses', '/api/dashboard/licenses'],
    ['Expired: 4 licenses. View records', 'Expired Licenses', '/api/dashboard/licenses?validity=Expired'],
    ['Suspended: 1 license. View records', 'Suspended Licenses', '/api/dashboard/licenses?validity=Suspended'],
  ])('%s opens the licenses from %3$s', async (buttonName, dialogName, url) => {
    const fetchMock = fakeDashboard()
    render(<DashboardPage />)
    const user = userEvent.setup()

    const dialog = await open(user, buttonName, dialogName)

    // Each license names its provider and shows the validity the API computed.
    expect(await within(dialog).findByRole('rowheader', { name: 'Savannah Coastal Kids Academy' })).toBeInTheDocument()
    expect(within(dialog).getByText('CCLC-10005')).toBeInTheDocument()
    expect(within(dialog).getByText('Expired (status still says Active)')).toBeInTheDocument()
    expect(urls(fetchMock)).toContain(url)
  })

  it('opens the same list from a pie slice as from its legend entry', async () => {
    const fetchMock = fakeDashboard()
    const { container } = render(<DashboardPage />)
    const user = userEvent.setup()
    await screen.findByRole('heading', { name: 'Expired vs. Valid Licenses' })

    await user.click(container.querySelectorAll('.pie svg path')[1])

    expect(await screen.findByRole('dialog', { name: 'Expired Licenses' })).toBeInTheDocument()
    expect(urls(fetchMock)).toContain('/api/dashboard/licenses?validity=Expired')
  })

  it('is read-only: the record list offers Close and nothing that changes data', async () => {
    const fetchMock = fakeDashboard()
    render(<DashboardPage />)
    const user = userEvent.setup()

    const dialog = await open(user, 'Active: 7 providers. View records', 'Active Providers')
    await within(dialog).findByRole('rowheader', { name: 'Peachtree Early Learning Center' })

    expect(within(dialog).getAllByRole('button').map((button) => button.textContent)).toEqual(['Close'])
    expect(fetchMock.calls().every((request) => request.method === 'GET')).toBe(true)

    await user.click(within(dialog).getByRole('button', { name: 'Close' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('shows an empty state and an error state inside the dialog', async () => {
    let fail = false
    fakeDashboard((request) => {
      if (!request.url.startsWith('/api/dashboard/providers')) return undefined
      return fail ? { status: 500, body: { title: 'Error', status: 500 } } : { body: [] }
    })
    render(<DashboardPage />)
    const user = userEvent.setup()

    const empty = await open(user, 'Providers: 11. View records', 'All Providers')
    expect(await within(empty).findByText('No providers.')).toBeInTheDocument()
    await user.click(within(empty).getByRole('button', { name: 'Close' }))

    fail = true
    const failed = await open(user, 'Active: 7 providers. View records', 'Active Providers')
    expect(await within(failed).findByRole('alert')).toHaveTextContent('The API could not complete the request (500).')
  })
})

describe('DashboardPage provider drill-down', () => {
  it('opens a provider and its licenses from the licenses-per-provider chart', async () => {
    const fetchMock = fakeDashboard()
    render(<DashboardPage />)
    const user = userEvent.setup()

    const dialog = await open(
      user,
      'Peachtree Early Learning Center: 2 licenses. View records',
      'Peachtree Early Learning Center',
    )

    expect(await within(dialog).findByText('CCLC-10001')).toBeInTheDocument()
    expect(urls(fetchMock)).toContain('/api/providers/1')
  })

  it('opens the provider from the expiring-licenses table', async () => {
    const fetchMock = fakeDashboard((request) =>
      request.url === '/api/providers/9' ? { body: { ...providerDetail, providerId: 9, providerName: 'Gwinnett Growing Minds' } } : undefined,
    )
    render(<DashboardPage />)
    const user = userEvent.setup()

    await open(user, 'View licenses for Gwinnett Growing Minds', 'Gwinnett Growing Minds')

    expect(urls(fetchMock)).toContain('/api/providers/9')
  })
})
