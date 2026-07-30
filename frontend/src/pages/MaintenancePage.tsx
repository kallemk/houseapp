import { Badge, Card, Center, Group, Loader, Stack, Table, Text, ThemeIcon, Title } from '@mantine/core'
import { IconCalendarClock } from '@tabler/icons-react'
import { Link, Navigate, useParams } from 'react-router-dom'
import type { MaintenanceScheduleItemDto } from '../api/types'
import { EmptyState } from '../components/common/EmptyState'
import { SortableTh } from '../components/common/SortableTh'
import { useTableSort } from '../hooks/useTableSort'
import { useMaintenanceSchedule } from '../hooks/useMaintenanceSchedule'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { formatInterval, formatMonthsSpan } from '../utils/interval'
import { MAINTENANCE_URGENCY_COLORS, MAINTENANCE_URGENCY_LABELS } from '../utils/labels'

function dueText(item: MaintenanceScheduleItemDto): string {
  if (item.nextDueDate === null || item.monthsUntilDue === null) {
    return '—'
  }
  if (item.monthsUntilDue < 0) {
    return `${item.nextDueDate} (${formatMonthsSpan(item.monthsUntilDue)} sedan)`
  }
  if (item.monthsUntilDue === 0) {
    return `${item.nextDueDate} (denna månad)`
  }
  return `${item.nextDueDate} (om ${formatMonthsSpan(item.monthsUntilDue)})`
}

export function MaintenancePage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const { property, isLoading: loadingProperty, notFound } = useSelectedProperty(propertyId)
  const { data: schedule, isLoading } = useMaintenanceSchedule(propertyId ?? '')
  const { sorted, sortProps } = useTableSort(schedule ?? [], {
    component: (i) => i.componentName,
    // By the underlying urgency order, not the Swedish label — "Försenat" before "Ok" matters more
    // than F before O.
    status: (i) => ['NotScheduled', 'Unknown', 'Ok', 'DueSoon', 'Overdue'].indexOf(i.urgency),
    interval: (i) => i.recommendedIntervalMonths,
    last: (i) => i.lastCompletedDate,
    next: (i) => i.nextDueDate,
  })

  if (loadingProperty || isLoading) {
    return (
      <Center py="xl">
        <Loader />
      </Center>
    )
  }

  if (notFound || !property) {
    return <Navigate to="/properties" replace />
  }

  return (
    <Stack>
      <Group gap="sm">
        <ThemeIcon variant="light" size={36} radius="md">
          <IconCalendarClock size={20} />
        </ThemeIcon>
        <Title order={2}>Underhållsplan</Title>
      </Group>
      <Text c="dimmed" size="sm">
        Räknas fram från komponenternas rekommenderade intervall och det senaste slutförda
        underhållsprojektet för varje del — inget som behöver hållas uppdaterat för hand. Finns inget
        loggat används bostadens byggår som utgångspunkt, tills du registrerar ett riktigt projekt.
      </Text>

      {!schedule || schedule.length === 0 ? (
        <EmptyState icon={IconCalendarClock} message="Inga komponenter att planera underhåll för." />
      ) : (
        <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
          <Table.ScrollContainer minWidth={780}>
            <Table striped highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <SortableTh {...sortProps('component')}>Komponent</SortableTh>
                  <SortableTh {...sortProps('status')}>Status</SortableTh>
                  <SortableTh {...sortProps('interval')}>Intervall</SortableTh>
                  <SortableTh {...sortProps('last')}>Senast utfört</SortableTh>
                  <SortableTh {...sortProps('next')}>Nästa gång</SortableTh>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {sorted.map((item) => (
                  <Table.Tr key={item.componentId}>
                    <Table.Td>
                      {item.componentName}
                      {item.hasUpcomingProject && (
                        <Badge ml="xs" size="sm" variant="light" color="blue">
                          Projekt planerat
                        </Badge>
                      )}
                    </Table.Td>
                    <Table.Td>
                      <Badge color={MAINTENANCE_URGENCY_COLORS[item.urgency]} variant="light">
                        {MAINTENANCE_URGENCY_LABELS[item.urgency]}
                      </Badge>
                    </Table.Td>
                    <Table.Td c="dimmed">{formatInterval(item.recommendedIntervalMonths)}</Table.Td>
                    <Table.Td c="dimmed">
                      {item.baseline === 'Project' && (
                        <>
                          {item.lastCompletedDate}
                          {' — '}
                          <Text
                            span
                            component={Link}
                            to={`/properties/${propertyId}/projects/${item.lastProjectId}`}
                            c="terracotta"
                          >
                            {item.lastProjectName}
                          </Text>
                        </>
                      )}
                      {/* Not a record of work done — the house's build year standing in until
                          something real is logged, so it mustn't read like a completed job. */}
                      {item.baseline === 'YearBuilt' && (
                        <Group gap={6} wrap="nowrap">
                          <Text span size="sm" fs="italic">
                            {item.lastCompletedDate}
                          </Text>
                          <Badge size="xs" variant="outline" color="gray">
                            Antaget byggår
                          </Badge>
                        </Group>
                      )}
                      {item.baseline === 'None' && '—'}
                    </Table.Td>
                    <Table.Td>{dueText(item)}</Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Card>
      )}
    </Stack>
  )
}
