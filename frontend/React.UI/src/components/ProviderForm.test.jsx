import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { counties, mockApi, providerListItem, statuses } from '../test/mockApi.js'
import ProviderForm from './ProviderForm.jsx'

function renderForm({ provider = null } = {}) {
  const onSaved = vi.fn()
  const onCancel = vi.fn()
  render(
    <ProviderForm
      provider={provider}
      counties={counties}
      statuses={statuses.providerStatuses}
      onSaved={onSaved}
      onCancel={onCancel}
    />,
  )
  return { onSaved, onCancel, user: userEvent.setup() }
}

const nameInput = () => screen.getByLabelText('Provider Name')
const countySelect = () => screen.getByLabelText('County')
const statusSelect = () => screen.getByLabelText('Status')

describe('ProviderForm validation', () => {
  it('shows a message beside every empty field and does not call the API', async () => {
    const fetchMock = mockApi(() => ({ status: 201, body: {} }))
    const { user, onSaved } = renderForm()

    await user.click(screen.getByRole('button', { name: 'Add Provider' }))

    expect(screen.getByText('Provider name is required.')).toBeInTheDocument()
    expect(screen.getByText('County is required.')).toBeInTheDocument()
    expect(screen.getByText('Status is required.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
    expect(onSaved).not.toHaveBeenCalled()
  })

  it('marks an invalid input and links it to its message for screen readers', async () => {
    mockApi(() => ({ status: 201, body: {} }))
    const { user } = renderForm()

    await user.click(screen.getByRole('button', { name: 'Add Provider' }))

    expect(nameInput()).toBeInvalid()
    expect(nameInput()).toHaveAccessibleDescription('Provider name is required.')
  })

  it('rejects a name that is only spaces', async () => {
    const fetchMock = mockApi(() => ({ status: 201, body: {} }))
    const { user } = renderForm()

    await user.type(nameInput(), '    ')
    await user.selectOptions(countySelect(), 'Fulton')
    await user.selectOptions(statusSelect(), 'Active')
    await user.click(screen.getByRole('button', { name: 'Add Provider' }))

    expect(screen.getByText('Provider name is required.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('rejects a name longer than 200 characters', async () => {
    const fetchMock = mockApi(() => ({ status: 201, body: {} }))
    const { user } = renderForm()

    await user.click(nameInput())
    await user.paste('x'.repeat(201))
    await user.selectOptions(countySelect(), 'Fulton')
    await user.selectOptions(statusSelect(), 'Active')
    await user.click(screen.getByRole('button', { name: 'Add Provider' }))

    expect(screen.getByText('Provider name must be 200 characters or fewer.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('clears a field message as soon as the user corrects that field', async () => {
    mockApi(() => ({ status: 201, body: {} }))
    const { user } = renderForm()
    await user.click(screen.getByRole('button', { name: 'Add Provider' }))

    await user.type(nameInput(), 'S')

    expect(screen.queryByText('Provider name is required.')).not.toBeInTheDocument()
    expect(screen.getByText('County is required.')).toBeInTheDocument()
  })
})

describe('ProviderForm saving', () => {
  it('creates a provider through POST /api/providers with a trimmed name', async () => {
    const saved = { providerId: 20, providerName: 'Sunrise Academy' }
    const fetchMock = mockApi(() => ({ status: 201, body: saved }))
    const { user, onSaved } = renderForm()

    await user.type(nameInput(), '  Sunrise Academy ')
    await user.selectOptions(countySelect(), 'DeKalb')
    await user.selectOptions(statusSelect(), 'Pending')
    await user.click(screen.getByRole('button', { name: 'Add Provider' }))

    expect(fetchMock.calls()).toEqual([
      {
        method: 'POST',
        url: '/api/providers',
        body: { providerName: 'Sunrise Academy', countyId: 44, status: 'Pending' },
      },
    ])
    expect(onSaved).toHaveBeenCalledWith(saved)
  })

  it('edits a provider through PUT, sending back the concurrency stamp it was loaded with', async () => {
    const provider = providerListItem({ providerId: 5, concurrencyStamp: 'stamp-5' })
    const fetchMock = mockApi(() => ({ body: { ...provider, status: 'Inactive' } }))
    const { user, onSaved } = renderForm({ provider })

    expect(nameInput()).toHaveValue('Peachtree Early Learning Center')
    expect(countySelect()).toHaveValue('60')
    expect(statusSelect()).toHaveValue('Active')

    await user.selectOptions(statusSelect(), 'Inactive')
    await user.click(screen.getByRole('button', { name: 'Save Changes' }))

    expect(fetchMock.calls()).toEqual([
      {
        method: 'PUT',
        url: '/api/providers/5',
        body: {
          providerName: 'Peachtree Early Learning Center',
          countyId: 60,
          status: 'Inactive',
          concurrencyStamp: 'stamp-5',
        },
      },
    ])
    expect(onSaved).toHaveBeenCalledTimes(1)
  })

  it('shows validation messages returned by the API beside their fields', async () => {
    mockApi(() => ({
      status: 400,
      body: { title: 'One or more validation errors occurred.', errors: { countyId: ['Select a valid Georgia county.'] } },
    }))
    const { user, onSaved } = renderForm({ provider: providerListItem() })

    await user.click(screen.getByRole('button', { name: 'Save Changes' }))

    expect(await screen.findByText('Select a valid Georgia county.')).toBeInTheDocument()
    expect(countySelect()).toBeInvalid()
    expect(screen.getByRole('alert')).toHaveTextContent('Please correct the highlighted fields.')
    expect(onSaved).not.toHaveBeenCalled()
  })

  it.each([
    'A provider with this name already exists in this county.',
    'This provider was changed by someone else after you opened it. Reload it and try again.',
  ])('shows a conflict from the API and keeps the form open: %s', async (detail) => {
    mockApi(() => ({ status: 409, body: { title: 'Conflict', status: 409, detail } }))
    const { user, onSaved } = renderForm({ provider: providerListItem() })

    await user.click(screen.getByRole('button', { name: 'Save Changes' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(detail)
    expect(onSaved).not.toHaveBeenCalled()
    // The form is usable again, so the user can correct the value and retry.
    expect(screen.getByRole('button', { name: 'Save Changes' })).toBeEnabled()
  })

  it('shows an error when the API cannot be reached', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))
    const { user } = renderForm({ provider: providerListItem() })

    await user.click(screen.getByRole('button', { name: 'Save Changes' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Cannot reach the API.')
  })

  it('cancels without saving', async () => {
    const fetchMock = mockApi(() => ({ body: {} }))
    const { user, onCancel } = renderForm()

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(onCancel).toHaveBeenCalledTimes(1)
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
