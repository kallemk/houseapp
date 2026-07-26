// No currency is configured anywhere in the data model — the app is Swedish-only, so amounts
// are always Swedish kronor and labeled explicitly as such.
export function formatCurrency(value: number): string {
  return `${new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 0 }).format(value)} kr`
}
