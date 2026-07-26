import { ActionIcon, Group, Menu, Stack, Text, Timeline, ThemeIcon } from '@mantine/core'
import { IconChartLine, IconFiles, IconHammer, IconPlus } from '@tabler/icons-react'
import type { DocumentDto, RenovationEntryDto, ValuationEntryDto } from '../../api/types'
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
}

interface PropertyTimelineProps {
  purchaseDate: string
  valuations: ValuationEntryDto[]
  renovations: RenovationEntryDto[]
  documents: DocumentDto[]
  onQuickAdd: (request: QuickAddRequest) => void
}

export function PropertyTimeline({ purchaseDate, valuations, renovations, documents, onQuickAdd }: PropertyTimelineProps) {
  const events: TimelineEvent[] = [
    ...valuations.map((v) => ({
      id: `valuation-${v.id}`,
      date: v.date,
      icon: IconChartLine,
      color: 'terracotta',
      label: `Värdering: ${formatCurrency(v.value)}`,
    })),
    ...renovations.map((r) => ({
      id: `renovation-${r.id}`,
      date: r.date,
      icon: IconHammer,
      color: 'blue',
      label: `${r.title}: ${formatCurrency(r.amount)}`,
    })),
    ...documents.map((d) => ({
      id: `document-${d.id}`,
      date: d.date,
      icon: IconFiles,
      color: 'grape',
      label: d.fileName,
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
                    onClick={() => onQuickAdd({ type: 'renovation', defaultDate: defaultDateForQuarter(quarter) })}
                  >
                    Lägg till renovering
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
                    <Text size="sm">{event.label}</Text>
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
