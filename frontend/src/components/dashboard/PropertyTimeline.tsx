import { ActionIcon, Anchor, Badge, Group, Menu, Stack, Text, Timeline, ThemeIcon, UnstyledButton } from '@mantine/core'
import { IconChartLine, IconChevronDown, IconChevronRight, IconHammer, IconPlus } from '@tabler/icons-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import type { ProjectDto, ValuationEntryDto } from '../../api/types'
import {
  currentYear,
  defaultDateForQuarter,
  defaultDateForYear,
  enumerateYears,
  quarterKeyFromDate,
  quarterLabel,
  quartersOfYear,
  yearFromDate,
} from '../../utils/quarters'
import type { QuickAddRequest } from './QuickAddModal'
import { formatCurrency } from '../../utils/currency'

interface TimelineEvent {
  id: string
  date: string
  icon: typeof IconChartLine
  color: string
  label: string
  to: string
}

/** Below this, folding a run into one row saves nothing and just adds something to click. */
const MIN_COLLAPSED_GAP = 3

interface PropertyTimelineProps {
  propertyId: string
  purchaseDate: string
  valuations: ValuationEntryDto[]
  projects: ProjectDto[]
  onQuickAdd: (request: QuickAddRequest) => void
}

/** A project sits where it happened, or where it's planned to — created-at is a fallback, not a date. */
function projectDate(project: ProjectDto): string | null {
  return project.completedDate ?? project.plannedStartDate ?? project.actualStartDate
}

function EventList({ events }: { events: TimelineEvent[] }) {
  if (events.length === 0) {
    return (
      <Text size="xs" c="dimmed">
        Ingen aktivitet
      </Text>
    )
  }

  return (
    <Stack gap={4}>
      {events.map((event) => (
        <Group key={event.id} gap="xs" wrap="nowrap" align="baseline">
          <ThemeIcon size={20} radius="xl" variant="light" color={event.color}>
            <event.icon size={12} />
          </ThemeIcon>
          {/* Fixed width so the labels line up into a column rather than ragging. */}
          <Text size="xs" c="dimmed" ff="monospace" w={82} style={{ flexShrink: 0 }}>
            {event.date}
          </Text>
          <Anchor component={Link} to={event.to} size="sm">
            {event.label}
          </Anchor>
        </Group>
      ))}
    </Stack>
  )
}

/** The +-menu, identical at year and quarter level — only the date it seeds differs. */
function QuickAddMenu({ defaultDate, onQuickAdd }: { defaultDate: string; onQuickAdd: (r: QuickAddRequest) => void }) {
  return (
    <Menu position="bottom-end" withArrow shadow="md">
      <Menu.Target>
        <ActionIcon size="sm" variant="light" radius="xl">
          <IconPlus size={14} />
        </ActionIcon>
      </Menu.Target>
      <Menu.Dropdown>
        <Menu.Item
          leftSection={<IconChartLine size={14} />}
          onClick={() => onQuickAdd({ type: 'valuation', defaultDate })}
        >
          Lägg till värdering
        </Menu.Item>
        <Menu.Item leftSection={<IconHammer size={14} />} onClick={() => onQuickAdd({ type: 'project', defaultDate })}>
          Lägg till projekt
        </Menu.Item>
      </Menu.Dropdown>
    </Menu>
  )
}

export function PropertyTimeline({
  propertyId,
  purchaseDate,
  valuations,
  projects,
  onQuickAdd,
}: PropertyTimelineProps) {
  // Years default to collapsed: a house owned for 20 years is 80 quarter rows, nearly all empty.
  // Expanding one year at a time is the only place quarter granularity actually earns its space.
  const [expandedYears, setExpandedYears] = useState<Set<string>>(new Set())
  const [expandedGaps, setExpandedGaps] = useState<Set<string>>(new Set())

  function toggle(setter: typeof setExpandedYears, key: string) {
    setter((previous) => {
      const next = new Set(previous)
      if (!next.delete(key)) {
        next.add(key)
      }
      return next
    })
  }

  const toggleYear = (year: string) => toggle(setExpandedYears, year)
  const toggleGap = (key: string) => toggle(setExpandedGaps, key)

  const events: TimelineEvent[] = [
    ...valuations.map((v) => ({
      id: `valuation-${v.id}`,
      date: v.date,
      icon: IconChartLine,
      color: 'terracotta',
      label: `Värdering: ${formatCurrency(v.value)}`,
      // There's no per-valuation page, so this goes to the list where it can be edited.
      to: `/properties/${propertyId}/valuations`,
    })),
    ...projects.flatMap((p) => {
      const date = projectDate(p)
      if (!date) {
        return []
      }
      const amount = p.actualCost > 0 ? p.actualCost : p.estimatedCost
      return [
        {
          id: `project-${p.id}`,
          date,
          icon: IconHammer,
          color: 'blue',
          label: `${p.name}: ${formatCurrency(amount)}`,
          to: `/properties/${propertyId}/projects/${p.id}`,
        },
      ]
    }),
  ]

  const byQuarter = new Map<string, TimelineEvent[]>()
  const byYear = new Map<string, TimelineEvent[]>()
  for (const event of events) {
    const quarter = quarterKeyFromDate(event.date)
    byQuarter.set(quarter, [...(byQuarter.get(quarter) ?? []), event])
    const year = yearFromDate(event.date)
    byYear.set(year, [...(byYear.get(year) ?? []), event])
  }
  for (const list of [...byQuarter.values(), ...byYear.values()]) {
    list.sort((a, b) => b.date.localeCompare(a.date))
  }

  const purchaseYear = yearFromDate(purchaseDate)
  const thisYear = currentYear()
  const from = purchaseYear <= thisYear ? purchaseYear : thisYear
  const years = enumerateYears(from, thisYear).reverse() // newest first

  // A house owned since 2000 is 27 rows, and on the demo property 20 of them said nothing but
  // "Ingen aktivitet" — enough to bury the years that matter, and on a phone enough to make the
  // dashboard mostly scrolling. Consecutive empty years collapse into one row you can open, so the
  // quick-add for a quiet year is still reachable; short runs are left alone because folding one or
  // two rows into one saves nothing and just adds a thing to click.
  const rows: ({ kind: 'year'; year: string } | { kind: 'gap'; years: string[] })[] = []
  for (const year of years) {
    const isEmpty = (byYear.get(year) ?? []).length === 0
    const previous = rows.at(-1)
    if (isEmpty && previous?.kind === 'gap') {
      previous.years.push(year)
    } else {
      rows.push(isEmpty ? { kind: 'gap', years: [year] } : { kind: 'year', year })
    }
  }

  function renderYear(year: string) {
    const yearEvents = byYear.get(year) ?? []
    const expanded = expandedYears.has(year)
    return (
      <Timeline.Item key={year}>
            <Group justify="space-between" wrap="nowrap" mb={4}>
              <UnstyledButton onClick={() => toggleYear(year)}>
                <Group gap={6} wrap="nowrap">
                  {expanded ? <IconChevronDown size={14} /> : <IconChevronRight size={14} />}
                  <Text fw={600} size="sm" c={yearEvents.length > 0 ? undefined : 'dimmed'}>
                    {year}
                  </Text>
                  {/* Not `circle` — that pins a one-character width and turned "12" into "1..". */}
                  {yearEvents.length > 0 && (
                    <Badge size="xs" variant="light">
                      {yearEvents.length}
                    </Badge>
                  )}
                </Group>
              </UnstyledButton>
              <QuickAddMenu defaultDate={defaultDateForYear(year)} onQuickAdd={onQuickAdd} />
            </Group>

            {expanded ? (
              <Stack gap="xs" mt="xs" pl="xs" style={{ borderLeft: '2px solid var(--mantine-color-gray-2)' }}>
                {quartersOfYear(year).map((quarter) => (
                  <div key={quarter}>
                    <Group justify="space-between" wrap="nowrap" mb={2}>
                      <Text size="xs" fw={600} c="dimmed">
                        {quarterLabel(quarter)}
                      </Text>
                      <QuickAddMenu defaultDate={defaultDateForQuarter(quarter)} onQuickAdd={onQuickAdd} />
                    </Group>
                    <EventList events={byQuarter.get(quarter) ?? []} />
                  </div>
                ))}
              </Stack>
        ) : (
          <EventList events={yearEvents} />
        )}
      </Timeline.Item>
    )
  }

  return (
    <Timeline bulletSize={16} lineWidth={2}>
      {rows.flatMap((row) => {
        if (row.kind === 'year') {
          return renderYear(row.year)
        }

        const key = `gap-${row.years[0]}`
        if (row.years.length < MIN_COLLAPSED_GAP || expandedGaps.has(key)) {
          return row.years.map(renderYear)
        }

        const newest = row.years[0]
        const oldest = row.years.at(-1)!
        return (
          <Timeline.Item key={key}>
            <UnstyledButton onClick={() => toggleGap(key)}>
              <Group gap={6} wrap="nowrap">
                <IconChevronRight size={14} />
                <Text size="sm" c="dimmed">
                  {oldest}–{newest} · {row.years.length} år utan aktivitet
                </Text>
              </Group>
            </UnstyledButton>
          </Timeline.Item>
        )
      })}
    </Timeline>
  )
}
