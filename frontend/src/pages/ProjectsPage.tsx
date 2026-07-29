import {
  Anchor,
  Badge,
  Button,
  Card,
  Center,
  Group,
  Loader,
  Select,
  Stack,
  Table,
  Text,
  ThemeIcon,
  Title,
} from '@mantine/core'
import { IconHammer, IconPlus, IconSettings } from '@tabler/icons-react'
import { useState } from 'react'
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom'
import type { ProjectStatus, WorkType } from '../api/types'
import { EmptyState } from '../components/common/EmptyState'
import { useProjects } from '../hooks/useProjects'
import { usePropertyComponents } from '../hooks/usePropertyComponents'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { formatCurrency } from '../utils/currency'
import {
  PROJECT_STATUS_COLORS,
  PROJECT_STATUS_LABELS,
  PROJECT_STATUS_OPTIONS,
  WORK_TYPE_COLORS,
  WORK_TYPE_LABELS,
  WORK_TYPE_OPTIONS,
} from '../utils/labels'

const ALL = 'all'

export function ProjectsPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const navigate = useNavigate()
  const { property, isLoading: loadingProperty, notFound } = useSelectedProperty(propertyId)
  const { data: projects, isLoading } = useProjects(propertyId ?? '')
  const { data: components, isLoading: loadingComponents } = usePropertyComponents()
  const [workType, setWorkType] = useState<string>(ALL)
  const [status, setStatus] = useState<string>(ALL)
  const [componentId, setComponentId] = useState<string>(ALL)

  if (loadingProperty || isLoading || loadingComponents) {
    return (
      <Center py="xl">
        <Loader />
      </Center>
    )
  }

  if (notFound || !property) {
    return <Navigate to="/properties" replace />
  }

  const componentsById = new Map((components ?? []).map((c) => [c.id, c]))
  const filtered = (projects ?? []).filter(
    (p) =>
      (workType === ALL || p.workType === workType) &&
      (status === ALL || p.status === status) &&
      (componentId === ALL || p.componentId === componentId),
  )

  return (
    <Stack>
      <Group justify="space-between">
        <Group gap="sm">
          <ThemeIcon variant="light" size={36} radius="md">
            <IconHammer size={20} />
          </ThemeIcon>
          <Title order={2}>Projekt</Title>
        </Group>
        <Group gap="md">
          <Anchor component={Link} to={`/properties/${propertyId}/components`} size="sm">
            <Group gap={4}>
              <IconSettings size={14} />
              Hantera komponenter
            </Group>
          </Anchor>
          <Button
            leftSection={<IconPlus size={16} />}
            onClick={() => navigate(`/properties/${propertyId}/projects/new`)}
          >
            Nytt projekt
          </Button>
        </Group>
      </Group>
      <Text c="dimmed" size="sm">
        Underhåll, renoveringar och nyinvesteringar i bostaden.
      </Text>

      <Card withBorder padding="md">
        <Group>
          <Select
            label="Typ av arbete"
            value={workType}
            onChange={(value) => setWorkType(value ?? ALL)}
            allowDeselect={false}
            w={170}
            data={[{ value: ALL, label: 'Alla' }, ...WORK_TYPE_OPTIONS]}
          />
          <Select
            label="Status"
            value={status}
            onChange={(value) => setStatus(value ?? ALL)}
            allowDeselect={false}
            w={170}
            data={[{ value: ALL, label: 'Alla' }, ...PROJECT_STATUS_OPTIONS]}
          />
          <Select
            label="Komponent"
            value={componentId}
            onChange={(value) => setComponentId(value ?? ALL)}
            allowDeselect={false}
            w={170}
            data={[
              { value: ALL, label: 'Alla' },
              ...(components ?? []).map((c) => ({ value: c.id, label: c.name })),
            ]}
          />
        </Group>
      </Card>

      {filtered.length === 0 ? (
        <EmptyState
          icon={IconHammer}
          message={
            (projects ?? []).length === 0
              ? 'Inga projekt registrerade ännu.'
              : 'Inga projekt matchar filtret.'
          }
        />
      ) : (
        <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
          <Table.ScrollContainer minWidth={780}>
            <Table striped highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Namn</Table.Th>
                  <Table.Th>Typ</Table.Th>
                  <Table.Th>Komponent</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th>Datum</Table.Th>
                  <Table.Th>Kostnad</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {filtered.map((project) => (
                  <Table.Tr
                    key={project.id}
                    style={{ cursor: 'pointer' }}
                    onClick={() => navigate(`/properties/${propertyId}/projects/${project.id}`)}
                  >
                    <Table.Td>
                      {project.name}
                      {project.isUrgent && (
                        <Badge ml="xs" size="sm" color="red" variant="light">
                          Brådskande
                        </Badge>
                      )}
                    </Table.Td>
                    <Table.Td>
                      <Badge color={WORK_TYPE_COLORS[project.workType as WorkType]} variant="light">
                        {WORK_TYPE_LABELS[project.workType as WorkType]}
                      </Badge>
                    </Table.Td>
                    <Table.Td c="dimmed">{componentsById.get(project.componentId)?.name ?? 'Okänd'}</Table.Td>
                    <Table.Td>
                      <Badge color={PROJECT_STATUS_COLORS[project.status as ProjectStatus]} variant="light">
                        {PROJECT_STATUS_LABELS[project.status as ProjectStatus]}
                      </Badge>
                    </Table.Td>
                    <Table.Td c="dimmed">
                      {project.completedDate ?? project.plannedStartDate ?? '—'}
                    </Table.Td>
                    <Table.Td fw={600}>
                      {/* Actual once there's anything itemised, otherwise what it's expected to cost. */}
                      {project.actualCost > 0
                        ? formatCurrency(project.actualCost)
                        : `≈ ${formatCurrency(project.estimatedCost)}`}
                    </Table.Td>
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
