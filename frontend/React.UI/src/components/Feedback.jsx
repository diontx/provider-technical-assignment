// The loading, error, and empty states shared by every page.

export function Loading({ label = 'Loading…' }) {
  return (
    <p className="state state-loading" role="status">
      {label}
    </p>
  )
}

/** @param {{ error: Error, onRetry?: () => void }} props */
export function ErrorAlert({ error, onRetry }) {
  return (
    <div className="state state-error" role="alert">
      <p>{error.message}</p>
      {onRetry && (
        <button type="button" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  )
}

export function EmptyState({ children }) {
  return <p className="state state-empty">{children}</p>
}

/** A coloured label for a provider or license status. */
export function StatusBadge({ value }) {
  return <span className={`badge badge-${String(value).toLowerCase()}`}>{value}</span>
}
