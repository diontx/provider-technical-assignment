import { EmptyState } from './Feedback.jsx'

// Two small charts drawn with CSS and SVG, so a handful of counts needs no charting library.
// Every value is a button: `onSelect(item)` opens the records behind it. A zero is not clickable.

/**
 * Horizontal bar chart.
 * @param {object} props
 * @param {string} props.title
 * @param {{ key: string | number, name: string, count: number }[]} props.items
 * @param {(item: object) => void} props.onSelect
 * @param {string} [props.unit] Singular noun for the accessible label, for example "provider".
 */
export function BarChart({ title, items, onSelect, unit = 'record' }) {
  const max = Math.max(1, ...items.map((item) => item.count))

  return (
    <section className="panel chart">
      <h2>{title}</h2>
      {items.length === 0 ? (
        <EmptyState>No data.</EmptyState>
      ) : (
        <ul>
          {items.map((item) => (
            <li key={item.key}>
              <button
                type="button"
                className="chart-row"
                disabled={item.count === 0}
                aria-label={describe(item, unit)}
                onClick={() => onSelect(item)}
              >
                <span className="chart-label">{item.name}</span>
                <span className="chart-track">
                  <span className="chart-bar" style={{ width: `${(item.count / max) * 100}%` }} />
                </span>
                <span className="chart-value">{item.count}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

const RADIUS = 50
const CENTER = 60

/**
 * Pie chart with a legend. The legend buttons are the accessible controls; the slices
 * are a pointer shortcut to the same action.
 *
 * @param {object} props
 * @param {string} props.title
 * @param {{ key: string, name: string, count: number, color: string }[]} props.items
 * @param {(item: object) => void} props.onSelect
 * @param {string} [props.unit]
 */
export function PieChart({ title, items, onSelect, unit = 'record' }) {
  const total = items.reduce((sum, item) => sum + item.count, 0)
  const slices = toSlices(items, total)

  return (
    <section className="panel chart">
      <h2>{title}</h2>
      {total === 0 ? (
        <EmptyState>No data.</EmptyState>
      ) : (
        <div className="pie">
          <svg viewBox="0 0 120 120" aria-hidden="true">
            {slices.map((slice) =>
              slice.full ? (
                <circle key={slice.item.key} cx={CENTER} cy={CENTER} r={RADIUS} fill={slice.item.color} onClick={() => onSelect(slice.item)} />
              ) : (
                <path key={slice.item.key} d={slice.path} fill={slice.item.color} onClick={() => onSelect(slice.item)} />
              ),
            )}
          </svg>
          <ul>
            {items.map((item) => (
              <li key={item.key}>
                <button
                  type="button"
                  className="chart-row pie-legend"
                  disabled={item.count === 0}
                  aria-label={describe(item, unit)}
                  onClick={() => onSelect(item)}
                >
                  <span className="pie-swatch" style={{ background: item.color }} />
                  <span className="chart-label">{item.name}</span>
                  <span className="chart-value">{item.count}</span>
                  <span className="muted">{Math.round((item.count / total) * 100)}%</span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  )
}

function describe(item, unit) {
  return `${item.name}: ${item.count} ${unit}${item.count === 1 ? '' : 's'}. View records`
}

/** Turns counts into SVG arcs, starting at 12 o'clock and going clockwise. */
function toSlices(items, total) {
  let angle = -Math.PI / 2

  return items
    .filter((item) => item.count > 0)
    .map((item) => {
      const sweep = (item.count / total) * 2 * Math.PI
      const start = angle
      angle += sweep

      // A single slice covering everything cannot be drawn as an arc (its start and end coincide).
      if (item.count === total) {
        return { item, full: true }
      }

      const [x1, y1] = point(start)
      const [x2, y2] = point(angle)
      const largeArc = sweep > Math.PI ? 1 : 0
      return {
        item,
        full: false,
        path: `M ${CENTER} ${CENTER} L ${x1} ${y1} A ${RADIUS} ${RADIUS} 0 ${largeArc} 1 ${x2} ${y2} Z`,
      }
    })
}

function point(angle) {
  return [(CENTER + RADIUS * Math.cos(angle)).toFixed(3), (CENTER + RADIUS * Math.sin(angle)).toFixed(3)]
}
