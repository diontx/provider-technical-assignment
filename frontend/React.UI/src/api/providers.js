import { request, toQuery } from './client.js'

// One function per API endpoint. Components call these and never build URLs themselves.

/**
 * @typedef {{ search?: string, status?: string, countyId?: string | number, page?: number, pageSize?: number }} ProviderFilters
 * @typedef {{ providerName: string, countyId: number, status: string }} ProviderInput
 */

/** @param {ProviderFilters} filters @param {AbortSignal} [signal] */
export const listProviders = (filters, signal) => request(`/providers${toQuery(filters)}`, { signal })

/** Provider with its licenses. @param {number} providerId @param {AbortSignal} [signal] */
export const getProvider = (providerId, signal) => request(`/providers/${providerId}`, { signal })

/** @param {ProviderInput} provider */
export const createProvider = (provider) => request('/providers', { method: 'POST', body: provider })

/**
 * @param {number} providerId
 * @param {ProviderInput & { concurrencyStamp: string }} provider The stamp must be the one loaded with the provider.
 */
export const updateProvider = (providerId, provider) =>
  request(`/providers/${providerId}`, { method: 'PUT', body: provider })

/** Soft delete: the API keeps the record for audit. @param {number} providerId */
export const deleteProvider = (providerId) => request(`/providers/${providerId}`, { method: 'DELETE' })

/**
 * Issues a new Active license. The API generates the license number and returns the license.
 * @param {number} providerId
 * @param {{ issueDate: string, expirationDate: string }} license Dates as "yyyy-MM-dd".
 */
export const issueLicense = (providerId, license) =>
  request(`/providers/${providerId}/licenses`, { method: 'POST', body: license })

export const getCounties = (signal) => request('/lookups/counties', { signal })

export const getStatuses = (signal) => request('/lookups/statuses', { signal })

export const getActiveProvidersWithActiveLicenses = (signal) =>
  request('/reports/active-providers-active-licenses', { signal })

export const getActiveProvidersWithExpiredLicenses = (signal) =>
  request('/reports/active-providers-expired-licenses', { signal })

export const getDeletedProviders = (signal) => request('/audit/providers/deleted', { signal })

export const getDashboardSummary = (signal) => request('/dashboard/summary', { signal })

/**
 * The providers behind a dashboard number.
 * @param {{ status?: string, county?: string, licenseGap?: 'NoValidLicense' | 'ExpiredLicense' }} filters
 * @param {AbortSignal} [signal]
 */
export const getDashboardProviders = (filters, signal) => request(`/dashboard/providers${toQuery(filters)}`, { signal })

/**
 * The licenses behind a dashboard number.
 * @param {string} [validity] A bucket key from the summary; omit for every license.
 * @param {AbortSignal} [signal]
 */
export const getDashboardLicenses = (validity, signal) => request(`/dashboard/licenses${toQuery({ validity })}`, { signal })
