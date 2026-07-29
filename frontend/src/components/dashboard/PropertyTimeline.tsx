import { ActionIcon, Anchor, Group, Menu, Stack, Text, Timeline, ThemeIcon } from '@mantine/core'
import { IconChartLine, IconFiles, IconHammer, IconPlus } from '@tabler/icons-react'
import { Link } from 'react-router-dom'
import { documentsApi } from '../../api/documents'
import type { DocumentDto, ProjectDto, ValuationEntryDto } from '../../api/types'
import {
  compareQuarterKeys,
  currentQuarterKey,
  defaultDateForQuarter,
  enumerateQuarters,
  quarterKeyFromDate,
  quarterLabel,
} from '../../utils/quarters'
import type { QuickAddRequest } from './QuickAddModal'
import { formatCurrency } from '../../utils/currency'

interface TimelineEvent {
  id: string
  date: string
  icon: typeof IconChartLine
  color: string
  label: string
  /** Navigate somewhere. Mutually exclusive with onClick. */
  to?: string
  /** Do something instead of navigating — documents download, matching the documents page. */
  onClick?: () => void
}

interface PropertyTimelineProps {
  propertyId: string
  purchaseDate: string
  valuations: ValuationEntryDto[]
  projects: ProjectDto[]
  documents: DocumentDto[]
  onQuickAdd: (request: QuickAddRequest) => void
}

/** A project sits where it happened, or where it's planned to — created-at is a fallback, not a date. */
function projectDate(project: ProjectDto): string | null {
  return project.completedDate ?? project.plannedStartDate ?? project.actualStartDate
}

export function PropertyTimeline({
  propertyId,
  purchaseDate,
  valuations,
  projects,
  documents,
  onQuickAdd,
}: PropertyTimelineProps) {
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
    ...documents.map((d) => ({
      id: `document-${d.id}`,
      date: d.date,
      icon: IconFiles,
      color: 'grape',
      label: d.title ?? d.fileName,
      // Downloads rather than navigates, so clicking a document name means the same thing here as
      // it does on the documents page.
      onClick: () => documentsApi.download(d.id, propertyId),
    })),
  ]

  const eventsByQuarter = new Map<string, TimelineEvent[]>()
  for (const event of events) {
    const key = quarterKeyFromDate(event.date)
    const list = eventsByQuarter.get(key) ?? []
    list.push(event)
    eventsByQuarter.set(key, list)
  }
  for (const list of eventsByQuarter.values()) {
    list.sort((a, b) => b.date.localeCompare(a.date))
  }

  const purchaseQuarter = quarterKeyFromDate(purchaseDate)
  const current = currentQuarterKey()
  const from = compareQuarterKeys(purchaseQuarter, current) <= 0 ? purchaseQuarter : current
  const quarters = enumerateQuarters(from, current).reverse() // newest first

  return (
    <Timeline bulletSize={16} lineWidth={2}>
      {quarters.map((quarter) => {
        const quarterEvents = eventsByQuarter.get(quarter) ?? []
        const hasEvents = quarterEvents.length > 0
        return (
          <Timeline.Item key={quarter}>
            <Group justify="space-between" wrap="nowrap" mb={4}>
              <Text fw={600} size="sm" c={hasEvents ? undefined : 'dimmed'}>
                {quarterLabel(quarter)}
              </Text>
              <Menu position="bottom-end" withArrow shadow="md">
                <Menu.Target>
                  <ActionIcon size="sm" variant="light" radius="xl">
                    <IconPlus size={14} />
                  </ActionIcon>
                </Menu.Target>
                <Menu.Dropdown>
                  <Menu.Item
                    leftSection={<IconChartLine size={14} />}
                    onClick={() => onQuickAdd({ type: 'valuation', defaultDate: defaultDateForQuarter(quarter) })}
                  >
                    Lägg till värdering
                  </Menu.Item>
                  <Menu.Item
                    leftSection={<IconHammer size={14} />}
                    onClick={() => onQuickAdd({ type: 'project', defaultDate: defaultDateForQuarter(quarter) })}
                  >
                    Lägg till projekt
                  </Menu.Item>
                  <Menu.Item
                    leftSection={<IconFiles size={14} />}
                    onClick={() => onQuickAdd({ type: 'document', defaultDate: defaultDateForQuarter(quarter) })}
                  >
                    Lägg till dokument
                  </Menu.Item>
                </Menu.Dropdown>
              </Menu>
            </Group>

            {hasEvents ? (
              <Stack gap={4}>
                {quarterEvents.map((event) => (
                  <Group key={event.id} gap="xs" wrap="nowrap">
                    <ThemeIcon size={20} radius="xl" variant="light" color={event.color}>
                      <event.icon size={12} />
                    </ThemeIcon>
                    {event.to && (
                      <Anchor component={Link} to={event.to} size="sm">
                        {event.label}
                      </Anchor>
                    )}
                    {event.onClick && (
                      <Anchor size="sm" onClick={event.onClick}>
                        {event.label}
                      </Anchor>
                    )}
                    {!event.to && !event.onClick && <Text size="sm">{event.label}</Text>}
                  </Group>
                ))}
              </Stack>
            ) : (
              <Text size="xs" c="dimmed">
                Ingen aktivitet
              </Text>
            )}
          </Timeline.Item>
        )
      })}
    </Timeline>
  )
}
