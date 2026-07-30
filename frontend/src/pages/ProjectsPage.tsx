import {
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
  TextInput,
  ThemeIcon,
  Title,
} from '@mantine/core'
import { IconHammer, IconPlus, IconSearch } from '@tabler/icons-react'
import { useState } from 'react'
import { Navigate, useNavigate, useParams } from 'react-router-dom'
import type { ProjectDto, ProjectStatus, WorkType } from '../api/types'
import { EmptyState } from '../components/common/EmptyState'
import { SortableTh } from '../components/common/SortableTh'
import { useTableSort } from '../hooks/useTableSort'
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
  const [year, setYear] = useState<string>(ALL)
  const [search, setSearch] = useState('')

  const componentsById = new Map((components ?? []).map((c) => [c.id, c]))

  // The same date the table shows and the list sorts by, so filtering by year can't disagree with
  // what's on screen. A project with neither date simply has no year to filter on.
  const projectYear = (p: ProjectDto) => (p.completedDate ?? p.plannedStartDate)?.slice(0, 4) ?? null
  const years = [...new Set((projects ?? []).map(projectYear).filter((y): y is string => y !== null))].sort(
    (a, b) => b.localeCompare(a),
  )

  const trimmedSearch = search.trim().toLowerCase()
  const filtered = (projects ?? []).filter(
    (p) =>
      (workType === ALL || p.workType === workType) &&
      (status === ALL || p.status === status) &&
      (componentId === ALL || p.componentId === componentId) &&
      (year === ALL || projectYear(p) === year) &&
      (trimmedSearch === '' || p.name.toLowerCase().includes(trimmedSearch)),
  )

  const { sorted, sortProps } = useTableSort(filtered, {
    name: (p) => p.name,
    workType: (p) => WORK_TYPE_LABELS[p.workType],
    component: (p) => componentsById.get(p.componentId)?.name,
    status: (p) => PROJECT_STATUS_LABELS[p.status],
    date: (p) => p.completedDate ?? p.plannedStartDate,
    cost: (p) => (p.actualCost > 0 ? p.actualCost : p.estimatedCost),
  })

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


  return (
    <Stack>
      <Group justify="space-between">
        <Group gap="sm">
          <ThemeIcon variant="light" size={36} radius="md">
            <IconHammer size={20} />
          </ThemeIcon>
          <Title order={2}>Projekt</Title>
        </Group>
        <Button
          leftSection={<IconPlus size={16} />}
          onClick={() => navigate(`/properties/${propertyId}/projects/new`)}
        >
          Nytt projekt
        </Button>
      </Group>
      <Text c="dimmed" size="sm">
        Underhåll, renoveringar och nyinvesteringar i bostaden.
      </Text>

      <Card withBorder padding="md">
        <Group>
          <TextInput
            label="Sök"
            placeholder="Namn"
            leftSection={<IconSearch size={16} />}
            value={search}
            onChange={(e) => setSearch(e.currentTarget.value)}
            w={220}
          />
          <Select
            label="År"
            value={year}
            onChange={(value) => setYear(value ?? ALL)}
            allowDeselect={false}
            w={120}
            data={[{ value: ALL, label: 'Alla' }, ...years.map((y) => ({ value: y, label: y }))]}
          />
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
              : 'Inga projekt matchar sökningen.'
          }
        />
      ) : (
        <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
          <Table.ScrollContainer minWidth={780}>
            <Table striped highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <SortableTh {...sortProps('name')}>Namn</SortableTh>
                  <SortableTh {...sortProps('workType')}>Typ</SortableTh>
                  <SortableTh {...sortProps('component')}>Komponent</SortableTh>
                  <SortableTh {...sortProps('status')}>Status</SortableTh>
                  <SortableTh {...sortProps('date')}>Datum</SortableTh>
                  <SortableTh {...sortProps('cost')}>Kostnad</SortableTh>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {sorted.map((project) => (
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
