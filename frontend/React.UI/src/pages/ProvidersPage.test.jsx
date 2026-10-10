import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { counties, mockApi, page, providerListItem, statuses } from '../test/mockApi.js'
import ProvidersPage from './ProvidersPage.jsx'

const peachtree = providerListItem()
const savannah = providerListItem({
  providerId: 3,
  providerName: 'Savannah Coastal Kids Academy',
  county: 'Chatham',
  licenseCount: 1,
  validLicenseCount: 0,
  expiredLicenseCount: 1,
})

const savannahDetail = {
  ...savannah,
  createdBy: 'seed',
  modifiedBy: null,
  asOfDate: '2026-10-10',
  hasValidLicense: false,
  licenses: [
    {
      licenseId: 5,
      providerId: 3,
      licenseNumber: 'CCLC-10005',
      licenseStatus: 'Active',
      issueDate: '2023-04-01',
      expirationDate: '2025-03-31',
      isExpired: true,
      isCurrentlyValid: false,
      effectiveStatus: 'Expired',
    },
  ],
}

/**
 * A small in-memory stand-in for the API. `overrides` can replace the response for a request.
 */
function fakeApi(initialProviders, overrides = () => undefined) {
  let providers = [...initialProviders]

  return mockApi((request) => {
    const override = overrides(request)
    if (override) {
      return override
    }

    const url = new URL(request.url, 'http://localhost')
    if (url.pathname === '/api/lookups/counties') return { body: counties }
    if (url.pathname === '/api/lookups/statuses') return { body: statuses }
    if (url.pathname === '/api/providers' && request.method === 'GET') {
      const search = (url.searchParams.get('search') ?? '').toLowerCase()
      return { body: page(providers.filter((p) => p.providerName.toLowerCase().includes(search))) }
    }
    if (url.pathname === '/api/providers/3' && request.method === 'GET') return { body: savannahDetail }
    if (request.method === 'DELETE') {
      const id = Number(url.pathname.split('/').pop())
      providers = providers.filter((p) => p.providerId !== id)
      return { status: 204 }
    }
    return { status: 404, body: { title: 'Not found', status: 404, detail: `No fake for ${request.method} ${request.url}` } }
  })
}

describe('ProvidersPage states', () => {
  it('shows a loading state, then the providers returned by the API', async () => {
    fakeApi([peachtree, savannah])
    render(<ProvidersPage />)

    expect(screen.getByRole('status')).toHaveTextContent('Loading providers…')

    expect(await screen.findByRole('rowheader', { name: 'Peachtree Early Learning Center' })).toBeInTheDocument()
    expect(screen.getByRole('rowheader', { name: 'Savannah Coastal Kids Academy' })).toBeInTheDocument()
    expect(screen.queryByText('Loading providers…')).not.toBeInTheDocument()
    expect(screen.getByText(/2 providers/)).toBeInTheDocument()
  })

  it('shows the license counts the API computed, flagging expired ones', async () => {
    fakeApi([savannah])
    render(<ProvidersPage />)

    const row = (await screen.findByRole('rowheader', { name: savannah.providerName })).closest('tr')

    expect(row).toHaveTextContent('0 valid · 1 expired of 1')
  })

  it('shows an empty state when there are no providers', async () => {
    fakeApi([])
    render(<ProvidersPage />)

    expect(await screen.findByText('There are no providers yet. Add the first one.')).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('shows a different empty state when the filters match nothing', async () => {
    fakeApi([peachtree])
    render(<ProvidersPage />)
    const user = userEvent.setup()
    await screen.findByRole('rowheader', { name: peachtree.providerName })

    await user.type(screen.getByLabelText('Search by name'), 'zzz')

    expect(await screen.findByText('No providers match these filters.')).toBeInTheDocument()
  })

  it('shows the API error and recovers when the user retries', async () => {
    let failing = true
    fakeApi([peachtree], (request) =>
      failing && request.url.startsWith('/api/providers')
        ? { status: 500, body: { title: 'An error occurred while processing your request.', status: 500 } }
        : undefined,
    )
    render(<ProvidersPage />)
    const user = userEvent.setup()

    expect(await screen.findByRole('alert')).toHaveTextContent('The API could not complete the request (500).')

    failing = false
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('rowheader', { name: peachtree.providerName })).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})

describe('ProvidersPage filters', () => {
  it('sends the search text, status, and county to the API', async () => {
    const fetchMock = fakeApi([peachtree])
    render(<ProvidersPage />)
    const user = userEvent.setup()
    await screen.findByRole('rowheader', { name: peachtree.providerName })

    await user.selectOptions(screen.getByLabelText('Status'), 'Active')
    await user.selectOptions(screen.getByLabelText('County'), 'Fulton')
    await user.type(screen.getByLabelText('Search by name'), 'peach')

    await waitFor(() =>
      expect(fetchMock.calls('GET').map((request) => request.url)).toContain(
        '/api/providers?search=peach&status=Active&countyId=60&page=1&pageSize=10',
      ),
    )
  })
})

describe('ProvidersPage delete', () => {
  it('asks for confirmation and sends nothing if the user cancels', async () => {
    const fetchMock = fakeApi([peachtree])
    render(<ProvidersPage />)
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: `Delete ${peachtree.providerName}` }))

    const dialog = screen.getByRole('dialog', { name: 'Delete provider?' })
    expect(dialog).toHaveTextContent(peachtree.providerName)
    expect(dialog).toHaveTextContent('kept for audit')
    expect(fetchMock.calls('DELETE')).toHaveLength(0)

    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(fetchMock.calls('DELETE')).toHaveLength(0)
    expect(screen.getByRole('rowheader', { name: peachtree.providerName })).toBeInTheDocument()
  })

  it('deletes through the API after confirmation and removes the provider from the list', async () => {
    const fetchMock = fakeApi([peachtree, savannah])
    render(<ProvidersPage />)
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: `Delete ${peachtree.providerName}` }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Delete provider' }))

    await waitFor(() =>
      expect(screen.queryByRole('rowheader', { name: peachtree.providerName })).not.toBeInTheDocument(),
    )
    expect(fetchMock.calls('DELETE').map((request) => request.url)).toEqual(['/api/providers/1'])
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(screen.getByText(`${peachtree.providerName} was deleted. The record is kept for audit.`)).toBeInTheDocument()
    expect(screen.getByRole('rowheader', { name: savannah.providerName })).toBeInTheDocument()
  })

  it('keeps the dialog open and shows the error when the delete fails', async () => {
    fakeApi([peachtree], (request) =>
      request.method === 'DELETE'
        ? { status: 404, body: { title: 'Not found', status: 404, detail: 'Provider 1 was not found.' } }
        : undefined,
    )
    render(<ProvidersPage />)
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: `Delete ${peachtree.providerName}` }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Delete provider' }))

    expect(await within(screen.getByRole('dialog')).findByRole('alert')).toHaveTextContent('Provider 1 was not found.')
  })
})

describe('ProvidersPage licenses', () => {
  it('loads the selected provider from the API and shows stored status beside computed validity', async () => {
    const fetchMock = fakeApi([savannah])
    render(<ProvidersPage />)
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: `View licenses for ${savannah.providerName}` }))

    const panel = await screen.findByRole('region', { name: 'Provider licenses' })
    expect(await within(panel).findByText('CCLC-10005')).toBeInTheDocument()
    expect(fetchMock.calls('GET').map((request) => request.url)).toContain('/api/providers/3')

    // Stored status says Active, the date has passed: the API's verdict is what is displayed.
    expect(within(panel).getByText('Expired (status still says Active)')).toBeInTheDocument()
    expect(within(panel).getByRole('note')).toHaveTextContent('Active but has no currently valid license')
    expect(within(panel).getByText('Mar 31, 2025')).toBeInTheDocument()
  })
})

describe('ProvidersPage create and edit', () => {
  it('opens the form with the county and status lists loaded from the API', async () => {
    fakeApi([peachtree])
    render(<ProvidersPage />)
    const user = userEvent.setup()
    await screen.findByRole('rowheader', { name: peachtree.providerName })

    await user.click(screen.getByRole('button', { name: 'Add provider' }))

    const dialog = screen.getByRole('dialog', { name: 'Add provider' })
    expect(within(within(dialog).getByLabelText('County')).getByRole('option', { name: 'DeKalb' })).toBeInTheDocument()
    expect(within(within(dialog).getByLabelText('Status')).getByRole('option', { name: 'Pending' })).toBeInTheDocument()
  })

  it('reloads the list and confirms after an edit is saved', async () => {
    const renamed = { ...peachtree, providerName: 'Peachtree Renamed' }
    let saved = false
    const fetchMock = fakeApi([peachtree], (request) => {
      if (request.method === 'PUT') {
        saved = true
        return { body: renamed }
      }
      if (saved && request.method === 'GET' && request.url.startsWith('/api/providers?')) {
        return { body: page([renamed]) }
      }
      return undefined
    })
    render(<ProvidersPage />)
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: `Edit ${peachtree.providerName}` }))
    const dialog = screen.getByRole('dialog', { name: 'Edit provider' })
    await user.clear(within(dialog).getByLabelText('Provider name'))
    await user.type(within(dialog).getByLabelText('Provider name'), 'Peachtree Renamed')
    await user.click(within(dialog).getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByRole('rowheader', { name: 'Peachtree Renamed' })).toBeInTheDocument()
    expect(screen.getByText('Peachtree Renamed was updated.')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(fetchMock.calls('PUT')[0].body.concurrencyStamp).toBe('stamp-1')
  })
})
