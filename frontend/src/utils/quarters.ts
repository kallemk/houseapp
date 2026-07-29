// Quarter keys are formatted "YYYY-Q#" (e.g. "2026-Q3"). Dates are parsed as UTC so a plain
// "YYYY-MM-DD" string always maps to the intended quarter regardless of the viewer's timezone.

export function quarterKeyFromDate(isoDate: string): string {
  const date = new Date(`${isoDate}T00:00:00Z`)
  const year = date.getUTCFullYear()
  const quarter = Math.floor(date.getUTCMonth() / 3) + 1
  return `${year}-Q${quarter}`
}

export function currentQuarterKey(): string {
  return quarterKeyFromDate(new Date().toISOString().slice(0, 10))
}

export function quarterLabel(key: string): string {
  const [year, q] = key.split('-Q')
  return `Kv ${q} ${year}`
}

/** First day of the quarter, as an ISO date string. */
export function quarterStartDate(key: string): string {
  const [yearStr, qStr] = key.split('-Q')
  const month = (Number(qStr) - 1) * 3
  return `${yearStr}-${String(month + 1).padStart(2, '0')}-01`
}

function parseQuarterKey(key: string): { year: number; quarter: number } {
  const [year, quarter] = key.split('-Q').map(Number)
  return { year, quarter }
}

export function compareQuarterKeys(a: string, b: string): number {
  const pa = parseQuarterKey(a)
  const pb = parseQuarterKey(b)
  return pa.year !== pb.year ? pa.year - pb.year : pa.quarter - pb.quarter
}

/** All quarter keys from `fromKey` to `toKey`, inclusive, ascending. */
export function enumerateQuarters(fromKey: string, toKey: string): string[] {
  const from = parseQuarterKey(fromKey)
  const to = parseQuarterKey(toKey)
  const result: string[] = []
  let { year, quarter } = from
  while (year < to.year || (year === to.year && quarter <= to.quarter)) {
    result.push(`${year}-Q${quarter}`)
    quarter += 1
    if (quarter > 4) {
      quarter = 1
      year += 1
    }
  }
  return result
}

/** Sensible default date for a new record in this quarter: today if it's the current quarter,
 * otherwise the first day of the quarter. */
export function defaultDateForQuarter(key: string): string {
  const current = currentQuarterKey()
  return key === current ? new Date().toISOString().slice(0, 10) : quarterStartDate(key)
}

// ---------------------------------------------------------------------------
// Year-level grouping. The timeline shows years by default and expands one year
// at a time into its quarters, so a house owned for decades doesn't render as a
// hundred mostly-empty rows.

export function yearFromQuarterKey(key: string): string {
  return key.split('-Q')[0]
}

export function yearFromDate(isoDate: string): string {
  return isoDate.slice(0, 4)
}

export function currentYear(): string {
  return String(new Date().getUTCFullYear())
}

/** All years from `fromYear` to `toYear`, inclusive, ascending. */
export function enumerateYears(fromYear: string, toYear: string): string[] {
  const from = Number(fromYear)
  const to = Number(toYear)
  const result: string[] = []
  for (let year = from; year <= to; year++) {
    result.push(String(year))
  }
  return result
}

/** The four quarter keys of a year, ascending. */
export function quartersOfYear(year: string): string[] {
  return [1, 2, 3, 4].map((q) => `${year}-Q${q}`)
}

/** Sensible default date for a new record in this year: today if it's the current year, else Jan 1. */
export function defaultDateForYear(year: string): string {
  return year === currentYear() ? new Date().toISOString().slice(0, 10) : `${year}-01-01`
}
