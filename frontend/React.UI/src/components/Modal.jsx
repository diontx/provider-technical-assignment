import { useEffect, useId, useRef } from 'react'

/**
 * A modal dialog. Escape and the backdrop cancel it, unless `busy` (a save or delete is in flight).
 * @param {{ title: string, onCancel: () => void, busy?: boolean, children: React.ReactNode }} props
 */
export default function Modal({ title, onCancel, busy = false, children }) {
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
      <div className="modal" role="dialog" aria-modal="true" aria-labelledby={titleId} tabIndex={-1} ref={dialogRef}>
        <h2 id={titleId}>{title}</h2>
        {children}
      </div>
    </div>
  )
}
