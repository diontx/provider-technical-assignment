import { useEffect, useId, useRef } from 'react'

/**
 * A modal dialog. Escape and the backdrop cancel it, unless `busy` (a save or delete is in flight).
 *
 * @param {object} props
 * @param {string} props.title
 * @param {() => void} props.onCancel
 * @param {boolean} [props.busy]
 * @param {boolean} [props.wide] Room for a table rather than a short form.
 * @param {boolean} [props.showClose] Adds a Close button beside the title, for read-only dialogs with no action buttons.
 * @param {React.ReactNode} props.children
 */
export default function Modal({ title, onCancel, busy = false, wide = false, showClose = false, children }) {
  const titleId = useId()
  const dialogRef = useRef(null)

  useEffect(() => {
    // Move focus into the dialog so keyboard and screen-reader users land in it.
    dialogRef.current?.focus()
  }, [])

  useEffect(() => {
    function onKeyDown(event) {
      if (event.key === 'Escape' && !busy) {
        onCancel()
      }
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [busy, onCancel])

  return (
    <div
      className="modal-backdrop"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !busy) {
          onCancel()
        }
      }}
    >
      <div
        className={`modal${wide ? ' modal-wide' : ''}`}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        ref={dialogRef}
      >
        <header className="modal-header">
          <h2 id={titleId}>{title}</h2>
          {showClose && (
            <button type="button" onClick={onCancel}>
              Close
            </button>
          )}
        </header>
        {children}
      </div>
    </div>
  )
}
