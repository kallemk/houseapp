import { Anchor, Badge, Card, Group, Progress, Stack, Text, ThemeIcon, UnstyledButton } from '@mantine/core'
import { IconChevronDown, IconChevronRight, IconClockDollar } from '@tabler/icons-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { ProjectDto, ProjectStatus } from '../../api/types'
import { formatCurrency } from '../../utils/currency'
import { currentYear, quarterKeyFromDate, quarterLabel, quartersOfYear, yearFromDate } from '../../utils/quarters'
import { PROJECT_STATUS_COLORS, PROJECT_STATUS_LABELS } from '../../utils/labels'

/**
 * Work that still has to be paid for. Cancelled work never will be, and completed work already has
 * been — everything else is money ahead of you, including uppskjuten, which is easy to forget about
 * precisely because it's on hold.
 */
const OPEN_STATUSES: ProjectStatus[] = ['Planned', 'InProgress', 'OnHold']

/** Undated work still costs money, so it gets its own row rather than being silently dropped. */
const UNDATED = 'undated'

interface Bucket {
  key: string
  label: string
  amount: number
  projects: ProjectDto[]
}

/**
 * When a project's money is expected. Planned start is the intent; actual start covers work already
 * under way that was never given a planned date.
 */
function expenseDate(project: ProjectDto): string | null {
  return project.plannedStartDate ?? project.actualStartDate
}

export function UpcomingExpenses({ projects, propertyId }: { projects: ProjectDto[]; propertyId: string }) {
  const [expanded, setExpanded] = useState<string | null>(null)

  const open = projects.filter((p) => OPEN_STATUSES.includes(p.status))
  const total = open.reduce((sum, p) => sum + p.estimatedCost, 0)

  // Only years that actually have something in them. A continuous range would render a decade of
  // empty rows the moment someone plans a roof for 2036.
  const byYear = new Map<string, ProjectDto[]>()
  for (const project of open) {
    const date = expenseDate(project)
    const key = date === null ? UNDATED : yearFromDate(date)
    byYear.set(key, [...(byYear.get(key) ?? []), project])
  }

  const buckets: Bucket[] = [...byYear.entries()]
    .map(([key, list]) => ({
      key,
      label: key === UNDATED ? 'Utan datum' : key,
      amount: list.reduce((sum, p) => sum + p.estimatedCost, 0),
      projects: list,
    }))
    // Undated last; everything else chronologically. Years already past are kept rather than folded
    // into this one — open work that was planned for last year is worth seeing as exactly that.
    .sort((a, b) => (a.key === UNDATED ? 1 : b.key === UNDATED ? -1 : a.key.localeCompare(b.key)))

  const largest = Math.max(...buckets.map((b) => b.amount), 1)
  const thisYear = currentYear()

  if (open.length === 0) {
    return null
  }

  return (
    <Card withBorder padding="lg">
      <Group gap="sm" mb="md">
        <ThemeIcon variant="light" size={40} radius="md">
          <IconClockDollar size={20} />
        </ThemeIcon>
        <div>
          <Text size="sm" c="dimmed">
            Kommande utgifter
          </Text>
          <Text size="xl" fw={700}>
            {formatCurrency(total)}
          </Text>
          <Text size="xs" c="dimmed">
            Uppskattad kostnad för projekt som inte är slutförda. Klicka på ett år för att se kvartal.
          </Text>
        </div>
      </Group>

      <Stack gap="sm">
        {buckets.map((bucket) => {
          const isExpanded = expanded === bucket.key
          const isPast = bucket.key !== UNDATED && bucket.key < thisYear
          return (
            <div key={bucket.key}>
              <UnstyledButton
                w="100%"
                // Undated work has no quarters to break into.
                onClick={() => bucket.key !== UNDATED && setExpanded(isExpanded ? null : bucket.key)}
              >
                <Group justify="space-between" gap="xs" wrap="nowrap" mb={4}>
                  <Group gap={4} wrap="nowrap">
                    {bucket.key !== UNDATED &&
                      (isExpanded ? <IconChevronDown size={14} /> : <IconChevronRight size={14} />)}
                    <Text size="sm" fw={500}>
                      {bucket.label}
                    </Text>
                    {/* Planned for a year that has already been and gone — still unpaid, and worth
                        saying so rather than quietly counting it as "coming up". */}
                    {isPast && (
                      <Badge size="xs" variant="light" color="orange">
                        Försenat
                      </Badge>
                    )}
                  </Group>
                  <Text size="sm" fw={600} style={{ whiteSpace: 'nowrap' }}>
                    {formatCurrency(bucket.amount)}
                  </Text>
                </Group>
              </UnstyledButton>
              <Progress
                value={(bucket.amount / largest) * 100}
                color={isPast ? 'orange' : 'terracotta'}
                size="sm"
                radius="sm"
              />

              {isExpanded && (
                <Stack
                  gap={6}
                  mt="xs"
                  pl="xs"
                  style={{ borderLeft: '2px solid var(--mantine-color-gray-2)' }}
                >
                  {quartersOfYear(bucket.key).map((quarter) => {
                    const inQuarter = bucket.projects.filter((p) => {
                      const date = expenseDate(p)
                      return date !== null && quarterKeyFromDate(date) === quarter
                    })
                    const amount = inQuarter.reduce((sum, p) => sum + p.estimatedCost, 0)
                    return (
                      <div key={quarter}>
                        <Group justify="space-between" gap="xs" wrap="nowrap">
                          <Text size="xs" fw={600} c="dimmed">
                            {quarterLabel(quarter)}
                          </Text>
                          <Text size="xs" c={amount === 0 ? 'dimmed' : undefined} fw={amount === 0 ? 400 : 600}>
                            {formatCurrency(amount)}
                          </Text>
                        </Group>
                        {/* All four quarters are listed even when empty — the gaps are the point of
                            drilling in, since they show when in the year the money actually falls. */}
                        {inQuarter.map((project) => (
                          <Group key={project.id} justify="space-between" gap="xs" wrap="nowrap" pl="md">
                            <Group gap={6} wrap="nowrap" style={{ minWidth: 0 }}>
                              <Anchor
                                component={Link}
                                to={`/properties/${propertyId}/projects/${project.id}`}
                                size="xs"
                                truncate
                              >
                                {project.name}
                              </Anchor>
                              <Badge size="xs" variant="light" color={PROJECT_STATUS_COLORS[project.status]}>
                                {PROJECT_STATUS_LABELS[project.status]}
                              </Badge>
                            </Group>
                            <Text size="xs" c="dimmed" style={{ whiteSpace: 'nowrap' }}>
                              {formatCurrency(project.estimatedCost)}
                            </Text>
                          </Group>
                        ))}
                      </div>
                    )
                  })}
                </Stack>
              )}
            </div>
          )
        })}
      </Stack>

      {/* Said plainly: an estimate of 0 is a project nobody has priced, not a free one. */}
      {open.some((p) => p.estimatedCost === 0) && (
        <Text size="xs" c="dimmed" mt="md">
          Projekt utan uppskattad kostnad räknas som 0 kr — summan är alltså en nedre gräns.
        </Text>
      )}
    </Card>
  )
}
