// RenovationType.recommendedIntervalMonths is stored as a single int (months) on the backend for
// simplicity, but admins think in whichever unit is natural for the type ("every 6 months" vs
// "every 20 years") — these helpers convert for the form and for display.
export type IntervalUnit = 'months' | 'years'

export function toMonths(value: number, unit: IntervalUnit): number {
  return unit === 'years' ? value * 12 : value
}

export function splitMonths(months: number): { value: number; unit: IntervalUnit } {
  if (months > 0 && months % 12 === 0) {
    return { value: months / 12, unit: 'years' }
  }
  return { value: months, unit: 'months' }
}

/**
 * A span of time as "2 år 3 mån" rather than "27 mån" — months alone stop being readable past a
 * year. Takes a positive count; the caller decides whether it reads as "om …" or "… sedan".
 */
export function formatMonthsSpan(months: number): string {
  const total = Math.abs(months)
  if (total === 0) {
    return '0 mån'
  }

  const years = Math.floor(total / 12)
  const remainingMonths = total % 12

  if (years === 0) {
    return `${remainingMonths} mån`
  }
  if (remainingMonths === 0) {
    return `${years} år`
  }
  return `${years} år ${remainingMonths} mån`
}

export function formatInterval(months: number | null): string {
  if (months === null || months <= 0) {
    return 'Inget intervall'
  }
  const { value, unit } = splitMonths(months)
  if (value === 1) {
    return unit === 'years' ? 'Varje år' : 'Varje månad'
  }
  return unit === 'years' ? `Vart ${value}:e år` : `Var ${value}:e månad`
}
