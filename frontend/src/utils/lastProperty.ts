// Remembers the last-viewed property across sessions so "/" can jump straight back into it
// instead of always landing on the picker. Purely a UX shortcut — the destination route still
// validates membership via useSelectedProperty, so a stale/invalid id here just bounces back
// to the picker rather than causing any real problem.
const STORAGE_KEY = 'houseapp:lastPropertyId'

export function getLastPropertyId(): string | null {
  return localStorage.getItem(STORAGE_KEY)
}

export function setLastPropertyId(id: string): void {
  localStorage.setItem(STORAGE_KEY, id)
}
