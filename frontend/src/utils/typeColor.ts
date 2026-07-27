// Renovation types are admin-managed data now, not a fixed enum, so badge colors can't be a
// hardcoded per-value map anymore — this deterministically picks a color from a fixed palette
// based on the type's id, so a given type always gets the same color across reloads.
const PALETTE = ['terracotta', 'blue', 'grape', 'green', 'orange', 'cyan', 'pink', 'teal']

export function colorForId(id: string): string {
  let hash = 0
  for (let i = 0; i < id.length; i++) {
    hash = (hash * 31 + id.charCodeAt(i)) | 0
  }
  return PALETTE[Math.abs(hash) % PALETTE.length]
}
