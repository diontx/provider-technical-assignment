import { describe, expect, it } from 'vitest'
import { addOneYear, todayIso, validateLicense } from './licenseValidation.js'

const today = '2026-10-10'
const options = { today }

describe('addOneYear', () => {
  it.each([
    ['2026-10-10', '2027-10-10'],
    ['2026-01-01', '2027-01-01'],
    ['2026-12-31', '2027-12-31'],
    ['2027-02-28', '2028-02-28'],
  ])('%s becomes %s', (issueDate, expected) => {
    expect(addOneYear(issueDate)).toBe(expected)
  })

  it('moves February 29 to February 28, as the API does', () => {
    expect(addOneYear('2028-02-29')).toBe('2029-02-28')
  })

  it.each(['', undefined, null, '2026-10', 'next week'])('returns an empty string for an incomplete date (%j)', (value) => {
    expect(addOneYear(value)).toBe('')
  })
})

describe('todayIso', () => {
  it('formats the local date with zero padding', () => {
    expect(todayIso(new Date(2026, 0, 5, 23, 30))).toBe('2026-01-05')
    expect(todayIso(new Date(2026, 11, 31, 0, 5))).toBe('2026-12-31')
  })
})

describe('validateLicense', () => {
  it('accepts the form defaults: issued today, expiring in one year', () => {
    expect(validateLicense({ issueDate: today, expirationDate: addOneYear(today) }, options)).toEqual({})
  })

  it('accepts a backdated license that is still valid today', () => {
    expect(validateLicense({ issueDate: '2025-10-10', expirationDate: today }, options)).toEqual({})
  })

  it('requires both dates', () => {
    expect(validateLicense({ issueDate: '', expirationDate: '' }, options)).toEqual({
      issueDate: 'Issue date is required.',
      expirationDate: 'Expiration date is required.',
    })
    expect(Object.keys(validateLicense({}, options))).toEqual(['issueDate', 'expirationDate'])
  })

  it('rejects an issue date in the future', () => {
    expect(validateLicense({ issueDate: '2026-10-11', expirationDate: '2027-10-11' }, options)).toEqual({
      issueDate: 'Issue date cannot be in the future.',
    })
  })

  it.each(['2026-10-09', '2026-10-10', '2027-04-10', '2027-10-09'])(
    'rejects an expiration date less than one year after the issue date (%s)',
    (expirationDate) => {
      expect(validateLicense({ issueDate: today, expirationDate }, options)).toEqual({
        expirationDate: 'Expiration date must be at least one year after the issue date.',
      })
    },
  )

  it.each(['2027-10-10', '2027-10-11', '2031-01-01'])('accepts a term of one year or longer (%s)', (expirationDate) => {
    expect(validateLicense({ issueDate: today, expirationDate }, options)).toEqual({})
  })

  it('measures the year from the issue date, not from today', () => {
    // Issued six months ago: one year from today is fine, one year from the issue date is the minimum.
    expect(validateLicense({ issueDate: '2026-04-10', expirationDate: '2027-04-09' }, options)).toEqual({
      expirationDate: 'Expiration date must be at least one year after the issue date.',
    })
    expect(validateLicense({ issueDate: '2026-04-10', expirationDate: '2027-04-10' }, options)).toEqual({})
  })

  it('applies the February 29 rule to the minimum term', () => {
    const leapDay = { today: '2028-02-29' }

    expect(validateLicense({ issueDate: '2028-02-29', expirationDate: '2029-02-28' }, leapDay)).toEqual({})
    expect(validateLicense({ issueDate: '2028-02-29', expirationDate: '2029-02-27' }, leapDay)).toEqual({
      expirationDate: 'Expiration date must be at least one year after the issue date.',
    })
  })

  it('rejects a license that would already be expired', () => {
    expect(validateLicense({ issueDate: '2024-01-01', expirationDate: '2026-10-09' }, options)).toEqual({
      expirationDate: 'Expiration date cannot be in the past.',
    })
  })

  it('still checks the expiration date against today when the issue date is missing', () => {
    expect(validateLicense({ issueDate: '', expirationDate: '2020-01-01' }, options)).toEqual({
      issueDate: 'Issue date is required.',
      expirationDate: 'Expiration date cannot be in the past.',
    })
  })
})
