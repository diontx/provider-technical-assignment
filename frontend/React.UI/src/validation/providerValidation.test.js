import { describe, expect, it } from 'vitest'
import { NAME_MAX_LENGTH, hasErrors, toProviderInput, validateProvider } from './providerValidation.js'

const options = {
  counties: [{ countyId: 44 }, { countyId: 60 }],
  statuses: ['Active', 'Inactive', 'Pending'],
}

const valid = { providerName: 'Sunrise Academy', countyId: '60', status: 'Active' }

describe('validateProvider', () => {
  it('accepts a complete provider', () => {
    const errors = validateProvider(valid, options)

    expect(errors).toEqual({})
    expect(hasErrors(errors)).toBe(false)
  })

  it('reports every missing field at once', () => {
    const errors = validateProvider({ providerName: '', countyId: '', status: '' }, options)

    expect(errors).toEqual({
      providerName: 'Provider name is required.',
      countyId: 'County is required.',
      status: 'Status is required.',
    })
    expect(hasErrors(errors)).toBe(true)
  })

  it('treats undefined values as missing', () => {
    expect(Object.keys(validateProvider({}, options))).toEqual(['providerName', 'countyId', 'status'])
  })

  describe('provider name', () => {
    it.each(['', '   ', '\t\n'])('rejects a blank name (%j)', (providerName) => {
      expect(validateProvider({ ...valid, providerName }, options)).toEqual({
        providerName: 'Provider name is required.',
      })
    })

    it('accepts a name of exactly the maximum length', () => {
      const providerName = 'a'.repeat(NAME_MAX_LENGTH)

      expect(validateProvider({ ...valid, providerName }, options)).toEqual({})
    })

    it('rejects a name one character over the maximum', () => {
      const providerName = 'a'.repeat(NAME_MAX_LENGTH + 1)

      expect(validateProvider({ ...valid, providerName }, options)).toEqual({
        providerName: 'Provider name must be 200 characters or fewer.',
      })
    })

    it('measures the length after trimming, as the API does', () => {
      const providerName = `  ${'a'.repeat(NAME_MAX_LENGTH)}  `

      expect(validateProvider({ ...valid, providerName }, options)).toEqual({})
    })
  })

  describe('county', () => {
    it('accepts the county id as a number or as the string a <select> produces', () => {
      expect(validateProvider({ ...valid, countyId: 60 }, options)).toEqual({})
      expect(validateProvider({ ...valid, countyId: '60' }, options)).toEqual({})
    })

    it.each(['999', 0, 'Fulton'])('rejects a county that is not in the list from the API (%j)', (countyId) => {
      expect(validateProvider({ ...valid, countyId }, options)).toEqual({
        countyId: 'Select a valid Georgia county.',
      })
    })
  })

  describe('status', () => {
    it.each(['Active', 'Inactive', 'Pending'])('accepts %s', (status) => {
      expect(validateProvider({ ...valid, status }, options)).toEqual({})
    })

    it.each(['Deleted', 'Expired', 'active'])('rejects %s, which is not a provider status', (status) => {
      expect(validateProvider({ ...valid, status }, options)).toEqual({
        status: 'Status must be one of: Active, Inactive, Pending.',
      })
    })
  })
})

describe('toProviderInput', () => {
  it('builds the API request body: trimmed name and a numeric county id', () => {
    expect(toProviderInput({ providerName: '  Sunrise Academy ', countyId: '60', status: 'Pending' })).toEqual({
      providerName: 'Sunrise Academy',
      countyId: 60,
      status: 'Pending',
    })
  })
})
