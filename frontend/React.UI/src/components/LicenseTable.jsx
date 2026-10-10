import { formatDate } from '../format.js'
import { EmptyState, StatusBadge } from './Feedback.jsx'

/**
 * Licenses with the stored status and the validity the API computed from the expiration date,
 * side by side. The UI does not decide validity; it displays `isExpired` and `isCurrentlyValid`.
 *
 * @param {{ licenses: object[], emptyMessage?: string }} props
 */
export default function LicenseTable({ licenses, emptyMessage = 'This provider has no licenses.' }) {
  if (licenses.length === 0) {
    return <EmptyState>{emptyMessage}</EmptyState>
  }

  return (
    <table className="licenses">
      <thead>
        <tr>
          <th scope="col">License number</th>
          <th scope="col">Stored status</th>
          <th scope="col">Expires</th>
          <th scope="col">Validity today</th>
        </tr>
      </thead>
      <tbody>
        {licenses.map((license) => (
          <tr key={license.licenseId} className={license.isExpired ? 'row-expired' : undefined}>
            <td>{license.licenseNumber}</td>
            <td>
              <StatusBadge value={license.licenseStatus} />
            </td>
            <td>{formatDate(license.expirationDate)}</td>
            <td>
              <LicenseValidity license={license} />
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function LicenseValidity({ license }) {
  if (license.isCurrentlyValid) {
    return <span className="validity validity-valid">Valid</span>
  }

  if (license.isExpired) {
    // Call out the case the stored status hides: it still says Active after the date passed.
    const drifted = license.licenseStatus !== 'Expired'
    return (
      <span className="validity validity-expired">
        Expired{drifted && ` (status still says ${license.licenseStatus})`}
      </span>
    )
  }

  return <span className="validity validity-invalid">Not valid ({license.licenseStatus})</span>
}
