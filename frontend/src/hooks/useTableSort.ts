import { useState } from 'react'

type Accessor<T> = (row: T) => string | number | null | undefined
export type SortAccessors<T> = Record<string, Accessor<T>>

/**
 * Click-to-sort for the list tables. Starts **unsorted**, deliberately: every list already arrives
 * in a considered order (projects by date, the maintenance schedule by urgency, documents newest
 * first), and defaulting to a column would silently throw that away. Sorting only kicks in once you
 * ask for it.
 *
 * First click sorts ascending, second descending, third clears it and restores the original order.
 */
export function useTableSort<T>(rows: T[], accessors: SortAccessors<T>) {
  const [sortKey, setSortKey] = useState<string | null>(null)
  const [descending, setDescending] = useState(false)

  function toggle(key: string) {
    if (sortKey !== key) {
      setSortKey(key)
      setDescending(false)
    } else if (!descending) {
      setDescending(true)
    } else {
      setSortKey(null)
      setDescending(false)
    }
  }

  const sorted = sortKey === null ? rows : [...rows].sort(compareBy(accessors[sortKey], descending))

  /** Spread onto SortableTh: `<SortableTh {...sortProps('name')}>Namn</SortableTh>` */
  function sortProps(key: string) {
    return {
      sorted: sortKey === key,
      descending: sortKey === key && descending,
      onSort: () => toggle(key),
    }
  }

  return { sorted, sortProps }
}

function compareBy<T>(accessor: Accessor<T>, descending: boolean) {
  return (a: T, b: T) => {
    const left = accessor(a)
    const right = accessor(b)

    // Blanks sink to the bottom either way — a project with no date is least useful at the top
    // whichever direction you're sorting.
    const leftEmpty = left === null || left === undefined || left === ''
    const rightEmpty = right === null || right === undefined || right === ''
    if (leftEmpty || rightEmpty) {
      return leftEmpty && rightEmpty ? 0 : leftEmpty ? 1 : -1
    }

    const result =
      typeof left === 'number' && typeof right === 'number'
        ? left - right
        : // 'sv' so å/ä/ö sort after z rather than next to a.
          String(left).localeCompare(String(right), 'sv')

    return descending ? -result : result
  }
}
