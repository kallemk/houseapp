import type { ProjectDto, WorkType } from '../api/types'

/**
 * What work has actually cost, counted from the itemised cost rows on each project.
 *
 * **A cost belongs to the year of its own `dateIncurred`, never the project's completion date.** The
 * budget page applies the same rule server-side, so a job running over New Year splits across both
 * years the way the money actually left the account. That rule lives here and only here — the
 * overview summary and the spend matrix both read it, and a second copy is a copy that drifts.
 *
 * Only cost rows count. A project carrying nothing but an estimate contributes 0, which is
 * deliberate: an estimate is a plan, not a payment. (Money still *ahead* of you is what
 * `UpcomingExpenses` shows, from estimates.)
 */
export function spent(
  projects: ProjectDto[],
  includes: (dateIncurred: string) => boolean,
  workType?: WorkType,
): number {
  return projects
    .filter((p) => workType === undefined || p.workType === workType)
    .flatMap((p) => p.costs)
    .filter((c) => includes(c.dateIncurred))
    .reduce((sum, c) => sum + c.amount, 0)
}

/** Local-time YYYY-MM-DD. `toISOString()` would shift the date by the UTC offset. */
export function isoDate(date: Date): string {
  return [
    date.getFullYear(),
    String(date.getMonth() + 1).padStart(2, '0'),
    String(date.getDate()).padStart(2, '0'),
  ].join('-')
}

/** Cost rows dated within the given calendar year. */
export const inYear = (year: number) => (dateIncurred: string) => Number(dateIncurred.slice(0, 4)) === year

/** Cost rows dated after `cutoff` — used for the rolling twelve months. */
export const since = (cutoff: string) => (dateIncurred: string) => dateIncurred > cutoff

/** Every cost row, whenever it was incurred. */
export const anyDate = () => true

/** The cutoff for "the last twelve months", as an ISO date. */
export function twelveMonthsAgo(today: Date): string {
  const cutoff = new Date(today)
  cutoff.setFullYear(cutoff.getFullYear() - 1)
  return isoDate(cutoff)
}
