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

// Called when the remembered property is deleted. Not strictly required — "/" re-validates and
// falls back to the picker anyway — but it avoids a pointless redirect bounce on next load.
export function clearLastPropertyId(id: string): void {
  if (localStorage.getItem(STORAGE_KEY) === id) {
    localStorage.removeItem(STORAGE_KEY)
  }
}
