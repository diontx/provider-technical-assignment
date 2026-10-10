import { useCallback, useEffect, useState } from 'react'

/**
 * Loads data when the component mounts and whenever `deps` change.
 * A newer load cancels the previous one, so a slow early response can never overwrite a later one.
 *
 * @template T
 * @param {(signal: AbortSignal) => Promise<T>} load
 * @param {unknown[]} deps
 * @returns {{ data: T | null, error: Error | null, loading: boolean, reload: () => void }}
 */
export function useAsync(load, deps) {
  const [state, setState] = useState({ data: null, error: null, loading: true })
  const [version, setVersion] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    // Keep showing the previous data while the next page or filter loads.
    setState((previous) => ({ ...previous, error: null, loading: true }))

    load(controller.signal).then(
      (data) => {
        if (!controller.signal.aborted) {
          setState({ data, error: null, loading: false })
        }
      },
      (error) => {
        if (!controller.signal.aborted) {
          setState({ data: null, error, loading: false })
        }
      },
    )

    return () => controller.abort()
    // `load` is a new function on every render, so the caller lists what it depends on instead.
  }, [...deps, version])

  const reload = useCallback(() => setVersion((current) => current + 1), [])

  return { ...state, reload }
}

/**
 * Returns `value` after it has stopped changing for `delayMs`. Used so that typing in the
 * search box sends one request, not one per keystroke.
 */
export function useDebouncedValue(value, delayMs) {
  const [debounced, setDebounced] = useState(value)

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs)
    return () => clearTimeout(timer)
  }, [value, delayMs])

  return debounced
}
