import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mockApi } from '../test/mockApi.js'
import IssueLicenseDialog from './IssueLicenseDialog.jsx'

const provider = { providerId: 5, providerName: 'Athens Bright Beginnings', county: 'Clarke' }

const issued = {
  licenseId: 13,
  providerId: 5,
  licenseNumber: 'CCLC-10013',
  licenseStatus: 'Active',
  issueDate: '2026-10-10',
  expirationDate: '2027-10-10',
  isExpired: false,
  isCurrentlyValid: true,
  effectiveStatus: 'Active',
}

function renderDialog() {
  const onIssued = vi.fn()
  const onCancel = vi.fn()
  render(<IssueLicenseDialog provider={provider} onIssued={onIssued} onCancel={onCancel} />)
  return { onIssued, onCancel, user: userEvent.setup() }
}

const issueDate = () => screen.getByLabelText('Issue Date')
const expires = () => screen.getByLabelText('Expires')
const submit = () => screen.getByRole('button', { name: 'Issue License' })

beforeEach(() => {
  // Fix "today" only; timers stay real so user-event and async rendering work normally.
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(new Date(2026, 9, 10, 12, 0))
})

afterEach(() => {
  vi.useRealTimers()
})

describe('IssueLicenseDialog defaults', () => {
  it('names the provider and explains that the license number is generated', () => {
    mockApi(() => ({ status: 201, body: issued }))
    renderDialog()

    expect(screen.getByRole('dialog', { name: 'Issue License' })).toHaveTextContent('Athens Bright Beginnings')
    expect(screen.getByLabelText('License Number')).toHaveValue('Generated automatically when issued')
    expect(screen.getByLabelText('License Number')).toBeDisabled()
  })

  it('defaults the issue date to today and the expiration date to one year later', () => {
    mockApi(() => ({ status: 201, body: issued }))
    renderDialog()

    expect(issueDate()).toHaveValue('2026-10-10')
    expect(expires()).toHaveValue('2027-10-10')
  })

  it('moves the expiration date with the issue date until the user sets it', () => {
    mockApi(() => ({ status: 201, body: issued }))
    renderDialog()

    fireEvent.change(issueDate(), { target: { value: '2026-07-01' } })
    expect(expires()).toHaveValue('2027-07-01')

    fireEvent.change(expires(), { target: { value: '2028-01-31' } })
    fireEvent.change(issueDate(), { target: { value: '2026-08-15' } })

    // The user's own choice is not overwritten.
    expect(expires()).toHaveValue('2028-01-31')
  })
})

describe('IssueLicenseDialog validation', () => {
  it.each([
    ['2026-10-11', '2027-10-11', 'Issue date cannot be in the future.'],
    ['2026-10-10', '2026-10-10', 'Expiration date must be at least one year after the issue date.'],
    ['2026-10-10', '2027-10-09', 'Expiration date must be at least one year after the issue date.'],
    ['2025-01-01', '2026-10-09', 'Expiration date cannot be in the past.'],
    ['2026-10-10', '', 'Expiration date is required.'],
    ['', '2027-10-10', 'Issue date is required.'],
  ])('issue %j, expires %j: %s', (issue, expiration, message) => {
    const fetchMock = mockApi(() => ({ status: 201, body: issued }))
    renderDialog()

    fireEvent.change(expires(), { target: { value: expiration } })
    fireEvent.change(issueDate(), { target: { value: issue } })

    // Shown as soon as the date is changed, without waiting for the user to submit.
    expect(screen.getByText(message)).toBeInTheDocument()
    expect(submit()).toBeDisabled()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('does not allow an expiration date less than one year after the issue date', async () => {
    const fetchMock = mockApi(() => ({ status: 201, body: issued }))
    const { user, onIssued } = renderDialog()

    fireEvent.change(expires(), { target: { value: '2027-10-09' } })
    await user.click(submit())
    fireEvent.submit(submit().closest('form'))

    expect(fetchMock).not.toHaveBeenCalled()
    expect(onIssued).not.toHaveBeenCalled()
    expect(expires()).toHaveAttribute('min', '2027-10-10')
  })

  it('accepts an expiration date exactly one year after the issue date, or later', () => {
    mockApi(() => ({ status: 201, body: issued }))
    renderDialog()

    fireEvent.change(expires(), { target: { value: '2027-10-10' } })
    expect(submit()).toBeEnabled()

    fireEvent.change(expires(), { target: { value: '2029-06-30' } })
    expect(submit()).toBeEnabled()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('flags a chosen expiration date that becomes too early when the issue date is moved later', () => {
    mockApi(() => ({ status: 201, body: issued }))
    renderDialog()

    // Issue date moved back a year, with an expiration date that suits it.
    fireEvent.change(issueDate(), { target: { value: '2025-10-10' } })
    fireEvent.change(expires(), { target: { value: '2026-12-31' } })
    expect(submit()).toBeEnabled()

    fireEvent.change(issueDate(), { target: { value: '2026-10-10' } })

    expect(screen.getByText('Expiration date must be at least one year after the issue date.')).toBeInTheDocument()
    expect(submit()).toBeDisabled()
  })

  it('clears the message and re-enables the button once the date is corrected', () => {
    mockApi(() => ({ status: 201, body: issued }))
    renderDialog()

    fireEvent.change(expires(), { target: { value: '2026-12-01' } })
    expect(submit()).toBeDisabled()

    fireEvent.change(expires(), { target: { value: '2027-12-01' } })

    expect(screen.queryByText('Expiration date must be at least one year after the issue date.')).not.toBeInTheDocument()
    expect(submit()).toBeEnabled()
  })

  it('marks the invalid date and links it to its message', () => {
    mockApi(() => ({ status: 201, body: issued }))
    renderDialog()

    fireEvent.change(expires(), { target: { value: '2026-10-01' } })

    expect(expires()).toBeInvalid()
    expect(expires()).toHaveAccessibleDescription('Expiration date must be at least one year after the issue date.')
    expect(issueDate()).toBeValid()
  })
})

describe('IssueLicenseDialog saving', () => {
  it('posts only the dates and passes the issued license, with its generated number, to the caller', async () => {
    const fetchMock = mockApi(() => ({ status: 201, body: issued }))
    const { user, onIssued } = renderDialog()

    await user.click(submit())

    expect(fetchMock.calls()).toEqual([
      {
        method: 'POST',
        url: '/api/providers/5/licenses',
        body: { issueDate: '2026-10-10', expirationDate: '2027-10-10' },
      },
    ])
    expect(onIssued).toHaveBeenCalledWith(issued)
  })

  it('shows a date rejected by the API beside its field', async () => {
    mockApi(() => ({
      status: 400,
      body: { title: 'One or more validation errors occurred.', errors: { issueDate: ['Issue date cannot be in the future.'] } },
    }))
    const { user, onIssued } = renderDialog()

    await user.click(submit())

    expect(await screen.findByText('Issue date cannot be in the future.')).toBeInTheDocument()
    expect(issueDate()).toBeInvalid()
    expect(onIssued).not.toHaveBeenCalled()
  })

  it('shows the conflict when the provider already holds a valid license', async () => {
    const detail = 'This provider already holds a valid license (CCLC-10004, expires 2030-08-31).'
    mockApi(() => ({ status: 409, body: { title: 'Conflict', status: 409, detail } }))
    const { user, onIssued } = renderDialog()

    await user.click(submit())

    expect(await screen.findByRole('alert')).toHaveTextContent(detail)
    expect(onIssued).not.toHaveBeenCalled()
    expect(submit()).toBeEnabled()
  })

  it('cancels without issuing', async () => {
    const fetchMock = mockApi(() => ({ status: 201, body: issued }))
    const { user, onCancel } = renderDialog()

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(onCancel).toHaveBeenCalledTimes(1)
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
