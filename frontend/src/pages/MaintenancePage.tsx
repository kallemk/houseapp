import { Badge, Card, Center, Group, Loader, Stack, Table, Text, ThemeIcon, Title } from '@mantine/core'
import { IconCalendarClock } from '@tabler/icons-react'
import { Link, Navigate, useParams } from 'react-router-dom'
import type { MaintenanceScheduleItemDto } from '../api/types'
import { EmptyState } from '../components/common/EmptyState'
import { useMaintenanceSchedule } from '../hooks/useMaintenanceSchedule'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { formatInterval } from '../utils/interval'
import { MAINTENANCE_URGENCY_COLORS, MAINTENANCE_URGENCY_LABELS } from '../utils/labels'

function dueText(item: MaintenanceScheduleItemDto): string {
  if (item.nextDueDate === null || item.monthsUntilDue === null) {
    return '—'
  }
  if (item.monthsUntilDue < 0) {
    const overdueBy = Math.abs(item.monthsUntilDue)
    return `${item.nextDueDate} (${overdueBy} mån sedan)`
  }
  if (item.monthsUntilDue === 0) {
    return `${item.nextDueDate} (denna månad)`
  }
  return `${item.nextDueDate} (om ${item.monthsUntilDue} mån)`
}

export function MaintenancePage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const { property, isLoading: loadingProperty, notFound } = useSelectedProperty(propertyId)
  const { data: schedule, isLoading } = useMaintenanceSchedule(propertyId ?? '')

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
        underhållsprojektet för varje del — inget som behöver hållas uppdaterat för hand.
      </Text>

      {!schedule || schedule.length === 0 ? (
        <EmptyState icon={IconCalendarClock} message="Inga komponenter att planera underhåll för." />
      ) : (
        <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
          <Table striped highlightOnHover verticalSpacing="sm">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Komponent</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Intervall</Table.Th>
                <Table.Th>Senast utfört</Table.Th>
                <Table.Th>Nästa gång</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {schedule.map((item) => (
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
                    {item.lastCompletedDate ? (
                      <>
                        {item.lastCompletedDate}
                        {item.lastProjectId && (
                          <>
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
                      </>
                    ) : (
                      '—'
                    )}
                  </Table.Td>
                  <Table.Td>{dueText(item)}</Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Card>
      )}
    </Stack>
  )
}
