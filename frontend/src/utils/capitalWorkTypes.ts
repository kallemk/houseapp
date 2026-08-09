import type { WorkType } from '../api/types'

// Which kinds of work the reader counts as money still sitting in the house, for the dashboard's
// "Mot insatt kapital". A view preference rather than a property of the house, so it lives in the
// browser and never reaches the API — the trade being that two members of the same household can
// see different figures, and the choice doesn't follow you to another device.

const storageKey = (propertyId: string) => `houseapp:capitalWorkTypes:${propertyId}`

/**
 * Renovation and Investment, matching what the figure counted before it was adjustable.
 *
 * Still an allowlist rather than an exclusion list: a work type added later has to be argued into
 * this default rather than landing in it. Maintenance is upkeep that's consumed rather than money
 * still in the building, and Purchase buys movable things that can leave with you — but both are
 * now the reader's call rather than the app's.
 */
export const DEFAULT_CAPITAL_WORK_TYPES: WorkType[] = ['Renovation', 'Investment']

const ALL: WorkType[] = ['Maintenance', 'Renovation', 'Investment', 'Purchase']

/**
 * **No stored value means "never chosen" and gives the default; a stored empty array means
 * "deliberately none" and must survive a reload.** Comparing purely against the purchase price is a
 * legitimate thing to want, and reading an empty selection as "unset" would quietly restore
 * Renovering + Nyinvestering on the next page load — the same "can't tell 'never ran' from
 * 'deliberately emptied'" mistake as `ComponentsCustomized` and `ProjectMigrator`.
 *
 * Anything malformed falls back to the default. This is storage the user can edit and that outlives
 * deploys, so a stale or hand-mangled value must not throw on the dashboard.
 */
export function getCapitalWorkTypes(propertyId: string): WorkType[] {
  const stored = localStorage.getItem(storageKey(propertyId))
  if (stored === null) {
    return DEFAULT_CAPITAL_WORK_TYPES
  }

  try {
    const parsed: unknown = JSON.parse(stored)
    if (!Array.isArray(parsed)) {
      return DEFAULT_CAPITAL_WORK_TYPES
    }
    // Keeps the app's own order rather than the stored one, so the generated caption reads the same
    // way however the boxes were ticked. Unknown values are dropped, not trusted.
    return ALL.filter((workType) => parsed.includes(workType))
  } catch {
    return DEFAULT_CAPITAL_WORK_TYPES
  }
}

export function setCapitalWorkTypes(propertyId: string, workTypes: WorkType[]): void {
  localStorage.setItem(storageKey(propertyId), JSON.stringify(workTypes))
}
