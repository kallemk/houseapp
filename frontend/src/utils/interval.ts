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
