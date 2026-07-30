import {
  ActionIcon,
  Button,
  Card,
  Center,
  Checkbox,
  Divider,
  Grid,
  Group,
  Loader,
  NumberInput,
  Select,
  Stack,
  Switch,
  Table,
  Text,
  Textarea,
  TextInput,
  Tooltip,
  ThemeIcon,
  Title,
} from '@mantine/core'
import { useForm } from '@mantine/form'
import { notifications } from '@mantine/notifications'
import { IconArrowLeft, IconHammer, IconInfoCircle, IconPlus, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { Navigate, useNavigate, useParams } from 'react-router-dom'
import type { SaveProjectInput } from '../api/projects'
import type { CostType, ProjectDto, ProjectPriority, ProjectStatus, WorkType } from '../api/types'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { ProjectDocuments } from '../components/projects/ProjectDocuments'
import { useCreateProject, useDeleteProject, useProject, useUpdateProject } from '../hooks/useProjects'
import { usePropertyComponents } from '../hooks/usePropertyComponents'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { formatCurrency } from '../utils/currency'
import {
  COST_TYPE_OPTIONS,
  PRIORITY_OPTIONS,
  PROJECT_STATUS_OPTIONS,
  WORK_TYPE_OPTIONS,
} from '../utils/labels'

interface CostFormValues {
  type: CostType
  description: string
  amount: number | string
  dateIncurred: string
  isBudgeted: boolean
}

interface MilestoneFormValues {
  description: string
  plannedDate: string
  completedDate: string
}

interface ProjectFormValues {
  name: string
  description: string
  notes: string
  workType: WorkType
  componentId: string
  status: ProjectStatus
  priority: ProjectPriority
  isUrgent: boolean
  countsTowardMaintenanceSchedule: boolean
  plannedStartDate: string
  actualStartDate: string
  completedDate: string
  estimatedDurationDays: number | string
  estimatedCost: number | string
  estimatedValueIncrease: number | string
  expectedLifespanYears: number | string
  energyEfficiencyGainPercent: number | string
  hasContractor: boolean
  contractorName: string
  contractorPhone: string
  contractorEmail: string
  contractorWebsite: string
  contractorQuotedPrice: number | string
  contractorQuotedDate: string
  contractorNotes: string
  costs: CostFormValues[]
  milestones: MilestoneFormValues[]
}

const EMPTY_FORM: ProjectFormValues = {
  name: '',
  description: '',
  notes: '',
  workType: 'Maintenance',
  componentId: '',
  status: 'Planned',
  priority: 'Medium',
  isUrgent: false,
  countsTowardMaintenanceSchedule: true,
  plannedStartDate: '',
  actualStartDate: '',
  completedDate: '',
  estimatedDurationDays: '',
  estimatedCost: '',
  estimatedValueIncrease: '',
  expectedLifespanYears: '',
  energyEfficiencyGainPercent: '',
  hasContractor: false,
  contractorName: '',
  contractorPhone: '',
  contractorEmail: '',
  contractorWebsite: '',
  contractorQuotedPrice: '',
  contractorQuotedDate: '',
  contractorNotes: '',
  costs: [],
  milestones: [],
}

const text = (value: string) => (value.trim() === '' ? null : value.trim())
const num = (value: number | string) => (value === '' || value === null ? null : Number(value))

/**
 * A cost is nearly always incurred around the work, not on the day you happen to type it in, so the
 * project's own dates are a far better guess than today. Most specific first: the job finished, else
 * it started, else it's planned to start. Today is the last resort for a project with no dates yet.
 */
function defaultCostDate(values: ProjectFormValues): string {
  return (
    values.completedDate ||
    values.actualStartDate ||
    values.plannedStartDate ||
    new Date().toISOString().slice(0, 10)
  )
}

function toFormValues(project: ProjectDto): ProjectFormValues {
  return {
    name: project.name,
    description: project.description ?? '',
    notes: project.notes ?? '',
    workType: project.workType,
    componentId: project.componentId,
    status: project.status,
    priority: project.priority,
    isUrgent: project.isUrgent,
    countsTowardMaintenanceSchedule: !project.excludeFromMaintenanceSchedule,
    plannedStartDate: project.plannedStartDate ?? '',
    actualStartDate: project.actualStartDate ?? '',
    completedDate: project.completedDate ?? '',
    estimatedDurationDays: project.estimatedDurationDays ?? '',
    estimatedCost: project.estimatedCost,
    estimatedValueIncrease: project.estimatedValueIncrease ?? '',
    expectedLifespanYears: project.expectedLifespanYears ?? '',
    energyEfficiencyGainPercent: project.energyEfficiencyGainPercent ?? '',
    hasContractor: project.contractor !== null,
    contractorName: project.contractor?.name ?? '',
    contractorPhone: project.contractor?.phone ?? '',
    contractorEmail: project.contractor?.email ?? '',
    contractorWebsite: project.contractor?.website ?? '',
    contractorQuotedPrice: project.contractor?.quotedPrice ?? '',
    contractorQuotedDate: project.contractor?.quotedDate ?? '',
    contractorNotes: project.contractor?.notes ?? '',
    costs: project.costs.map((c) => ({
      type: c.type,
      description: c.description ?? '',
      amount: c.amount,
      dateIncurred: c.dateIncurred,
      isBudgeted: c.isBudgeted,
    })),
    milestones: project.milestones.map((m) => ({
      description: m.description,
      plannedDate: m.plannedDate ?? '',
      completedDate: m.completedDate ?? '',
    })),
  }
}

function toInput(values: ProjectFormValues): SaveProjectInput {
  return {
    name: values.name.trim(),
    description: text(values.description),
    notes: text(values.notes),
    workType: values.workType,
    componentId: values.componentId,
    status: values.status,
    priority: values.priority,
    isUrgent: values.isUrgent,
    excludeFromMaintenanceSchedule: !values.countsTowardMaintenanceSchedule,
    plannedStartDate: text(values.plannedStartDate),
    actualStartDate: text(values.actualStartDate),
    completedDate: text(values.completedDate),
    estimatedDurationDays: num(values.estimatedDurationDays),
    estimatedCost: Number(values.estimatedCost) || 0,
    estimatedValueIncrease: num(values.estimatedValueIncrease),
    expectedLifespanYears: num(values.expectedLifespanYears),
    energyEfficiencyGainPercent: num(values.energyEfficiencyGainPercent),
    contractor: values.hasContractor
      ? {
          name: values.contractorName.trim(),
          phone: text(values.contractorPhone),
          email: text(values.contractorEmail),
          website: text(values.contractorWebsite),
          quotedPrice: num(values.contractorQuotedPrice),
          quotedDate: text(values.contractorQuotedDate),
          notes: text(values.contractorNotes),
        }
      : null,
    costs: values.costs.map((c) => ({
      type: c.type,
      description: text(c.description),
      amount: Number(c.amount) || 0,
      dateIncurred: c.dateIncurred,
      isBudgeted: c.isBudgeted,
    })),
    milestones: values.milestones
      // A row with no description is an empty row the user added and didn't fill in.
      .filter((m) => m.description.trim() !== '')
      .map((m) => ({
        description: m.description.trim(),
        plannedDate: text(m.plannedDate),
        completedDate: text(m.completedDate),
      })),
  }
}

export function ProjectDetailPage() {
  const { propertyId, projectId } = useParams<{ propertyId: string; projectId: string }>()
  const navigate = useNavigate()
  const isNew = projectId === 'new'

  const { property, isLoading: loadingProperty, notFound } = useSelectedProperty(propertyId)
  const { data: components, isLoading: loadingComponents } = usePropertyComponents()
  const { data: project, isLoading: loadingProject } = useProject(propertyId ?? '', isNew ? '' : (projectId ?? ''))
  const createProject = useCreateProject(propertyId ?? '')
  const updateProject = useUpdateProject(propertyId ?? '', projectId ?? '')
  const deleteProject = useDeleteProject(propertyId ?? '')
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [initialised, setInitialised] = useState(false)

  const form = useForm<ProjectFormValues>({
    initialValues: EMPTY_FORM,
    validate: {
      name: (value) => (value.trim() ? null : 'Namn krävs'),
      componentId: (value) => (value ? null : 'Välj en komponent'),
    },
  })

  // Populate once the project arrives. Guarded so it doesn't stomp on edits in progress when the
  // query refetches in the background.
  if (!isNew && project && !initialised) {
    form.setValues(toFormValues(project))
    form.resetDirty(toFormValues(project))
    setInitialised(true)
  }

  const projectsPath = `/properties/${propertyId}/projects`

  if (loadingProperty || loadingComponents || (!isNew && loadingProject)) {
    return (
      <Center py="xl">
        <Loader />
      </Center>
    )
  }

  if (notFound || !property) {
    return <Navigate to="/properties" replace />
  }

  if (!isNew && !project) {
    return <Navigate to={projectsPath} replace />
  }

  function handleSubmit(values: ProjectFormValues) {
    const input = toInput(values)
    const onError = () => notifications.show({ color: 'red', message: 'Kunde inte spara projektet. Försök igen.' })

    if (isNew) {
      createProject.mutate(input, {
        onSuccess: (created) => navigate(`${projectsPath}/${created.id}`, { replace: true }),
        onError,
      })
    } else {
      updateProject.mutate(input, {
        onSuccess: () => notifications.show({ color: 'green', message: 'Projektet sparades.' }),
        onError,
      })
    }
  }

  const componentOptions = (components ?? []).map((c) => ({ value: c.id, label: c.name }))
  const actualCost = form.values.costs.reduce((sum, c) => sum + (Number(c.amount) || 0), 0)
  const saving = createProject.isPending || updateProject.isPending

  return (
    <Stack>
      <Group justify="space-between">
        <Group gap="sm">
          <ThemeIcon variant="light" size={36} radius="md">
            <IconHammer size={20} />
          </ThemeIcon>
          <Title order={2}>{isNew ? 'Nytt projekt' : (project?.name ?? '')}</Title>
        </Group>
        <Button variant="subtle" leftSection={<IconArrowLeft size={16} />} onClick={() => navigate(projectsPath)}>
          Tillbaka
        </Button>
      </Group>

      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Stack>
          <Card withBorder padding="lg">
            <Stack>
              <Title order={5}>Grunduppgifter</Title>
              <TextInput label="Namn" required {...form.getInputProps('name')} />
              <Textarea label="Beskrivning" autosize minRows={2} {...form.getInputProps('description')} />
              <Grid>
                <Grid.Col span={{ base: 12, sm: 4 }}>
                  <Select
                    label="Typ av arbete"
                    data={WORK_TYPE_OPTIONS}
                    allowDeselect={false}
                    {...form.getInputProps('workType')}
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 12, sm: 4 }}>
                  <Select
                    label="Komponent"
                    placeholder="Välj komponent"
                    data={componentOptions}
                    required
                    {...form.getInputProps('componentId')}
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 12, sm: 4 }}>
                  <Select
                    label="Status"
                    data={PROJECT_STATUS_OPTIONS}
                    allowDeselect={false}
                    {...form.getInputProps('status')}
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 12, sm: 4 }}>
                  <Select
                    label="Prioritet"
                    data={PRIORITY_OPTIONS}
                    allowDeselect={false}
                    {...form.getInputProps('priority')}
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 12, sm: 8 }}>
                  <Switch
                    mt="xl"
                    label="Brådskande"
                    {...form.getInputProps('isUrgent', { type: 'checkbox' })}
                  />
                </Grid.Col>
                {/* Only shown where it does something: investments never reset the clock anyway. */}
                {form.values.workType !== 'Investment' && (
                  <Grid.Col span={12}>
                    <Checkbox
                      label="Räknas mot underhållsplanen"
                      description="Avmarkera för mindre jobb som inte förlänger komponentens livslängd, t.ex. en lagning."
                      {...form.getInputProps('countsTowardMaintenanceSchedule', { type: 'checkbox' })}
                    />
                  </Grid.Col>
                )}
              </Grid>
            </Stack>
          </Card>

          <Card withBorder padding="lg">
            <Stack>
              <Title order={5}>Tidsplan</Title>
              <Grid>
                <Grid.Col span={{ base: 12, sm: 3 }}>
                  <TextInput label="Planerad start" type="date" {...form.getInputProps('plannedStartDate')} />
                </Grid.Col>
                <Grid.Col span={{ base: 12, sm: 3 }}>
                  <TextInput label="Faktisk start" type="date" {...form.getInputProps('actualStartDate')} />
                </Grid.Col>
                <Grid.Col span={{ base: 12, sm: 3 }}>
                  <TextInput label="Slutfört" type="date" {...form.getInputProps('completedDate')} />
                </Grid.Col>
                <Grid.Col span={{ base: 12, sm: 3 }}>
                  <NumberInput label="Uppskattad tid (dagar)" min={0} {...form.getInputProps('estimatedDurationDays')} />
                </Grid.Col>
              </Grid>
            </Stack>
          </Card>

          <Card withBorder padding="lg">
            <Stack>
              <Group justify="space-between">
                <Title order={5}>Ekonomi</Title>
                <Text size="sm" c="dimmed">
                  Faktisk kostnad: <Text span fw={700}>{formatCurrency(actualCost)}</Text>
                </Text>
              </Group>
              <NumberInput
                label="Uppskattad kostnad (kr)"
                min={0}
                w={240}
                {...form.getInputProps('estimatedCost')}
              />

              <Divider label="Kostnadsposter" labelPosition="left" />
              <Text size="xs" c="dimmed">
                Den faktiska kostnaden är summan av posterna nedan — lägg till en post av typen Övrigt om du
                inte vill specificera.
              </Text>

              {form.values.costs.length > 0 && (
                <Table.ScrollContainer minWidth={720}>
                  <Table verticalSpacing="xs">
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th w={150}>Typ</Table.Th>
                        <Table.Th>Beskrivning</Table.Th>
                        <Table.Th w={140}>Belopp (kr)</Table.Th>
                        <Table.Th w={160}>Datum</Table.Th>
                        <Table.Th w={120}>
                        <Group gap={4} wrap="nowrap">
                          Budgeterad
                          <Tooltip
                            multiline
                            w={260}
                            label="Kryssa i om kostnaden var planerad i årets budget. Just nu är det bara en notering — budgetsidan räknar med alla kostnader oavsett."
                          >
                            <IconInfoCircle size={14} style={{ opacity: 0.6 }} />
                          </Tooltip>
                        </Group>
                      </Table.Th>
                        <Table.Th w={50} />
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {form.values.costs.map((_, index) => (
                        // Index keys are safe here: rows are only ever appended or removed, never reordered.
                        <Table.Tr key={index}>
                          <Table.Td>
                            <Select
                              data={COST_TYPE_OPTIONS}
                              allowDeselect={false}
                              {...form.getInputProps(`costs.${index}.type`)}
                            />
                          </Table.Td>
                          <Table.Td>
                            <TextInput {...form.getInputProps(`costs.${index}.description`)} />
                          </Table.Td>
                          <Table.Td>
                            <NumberInput min={0} {...form.getInputProps(`costs.${index}.amount`)} />
                          </Table.Td>
                          <Table.Td>
                            <TextInput type="date" {...form.getInputProps(`costs.${index}.dateIncurred`)} />
                          </Table.Td>
                          <Table.Td>
                            <Checkbox
                              {...form.getInputProps(`costs.${index}.isBudgeted`, { type: 'checkbox' })}
                            />
                          </Table.Td>
                          <Table.Td>
                            <ActionIcon
                              color="red"
                              variant="subtle"
                              onClick={() => form.removeListItem('costs', index)}
                            >
                              <IconTrash size={16} />
                            </ActionIcon>
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </Table.ScrollContainer>
              )}

              <Group>
                <Button
                  variant="light"
                  leftSection={<IconPlus size={16} />}
                  onClick={() =>
                    form.insertListItem('costs', {
                      // Övrigt by default: most costs get typed in as a lump sum, and picking a
                      // specific type is the exception rather than the rule.
                      type: 'Other',
                      description: '',
                      amount: '',
                      dateIncurred: defaultCostDate(form.values),
                      isBudgeted: false,
                    })
                  }
                >
                  Lägg till kostnadspost
                </Button>
              </Group>
            </Stack>
          </Card>

          {/* Only once the project exists — attachments are saved against its id, not with the form. */}
          {!isNew && projectId && <ProjectDocuments propertyId={propertyId ?? ''} projectId={projectId} />}

          <Card withBorder padding="lg">
            <Stack>
              <Title order={5}>Milstolpar</Title>
              <Text size="xs" c="dimmed">
                Endast tidsplan — kostnader hör hemma bland kostnadsposterna ovan.
              </Text>

              {form.values.milestones.length > 0 && (
                <Table.ScrollContainer minWidth={620}>
                  <Table verticalSpacing="xs">
                    <Table.Thead>
                      <Table.Tr>
                        <Table.Th>Beskrivning</Table.Th>
                        <Table.Th w={170}>Planerat</Table.Th>
                        <Table.Th w={170}>Klart</Table.Th>
                        <Table.Th w={50} />
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {form.values.milestones.map((_, index) => (
                        <Table.Tr key={index}>
                          <Table.Td>
                            <TextInput {...form.getInputProps(`milestones.${index}.description`)} />
                          </Table.Td>
                          <Table.Td>
                            <TextInput type="date" {...form.getInputProps(`milestones.${index}.plannedDate`)} />
                          </Table.Td>
                          <Table.Td>
                            <TextInput type="date" {...form.getInputProps(`milestones.${index}.completedDate`)} />
                          </Table.Td>
                          <Table.Td>
                            <ActionIcon
                              color="red"
                              variant="subtle"
                              onClick={() => form.removeListItem('milestones', index)}
                            >
                              <IconTrash size={16} />
                            </ActionIcon>
                          </Table.Td>
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                </Table.ScrollContainer>
              )}

              <Group>
                <Button
                  variant="light"
                  leftSection={<IconPlus size={16} />}
                  onClick={() =>
                    form.insertListItem('milestones', { description: '', plannedDate: '', completedDate: '' })
                  }
                >
                  Lägg till milstolpe
                </Button>
              </Group>
            </Stack>
          </Card>

          <Card withBorder padding="lg">
            <Stack>
              <Group justify="space-between">
                <Title order={5}>Entreprenör</Title>
                <Switch
                  label="Anlitad"
                  {...form.getInputProps('hasContractor', { type: 'checkbox' })}
                />
              </Group>
              {form.values.hasContractor && (
                <Grid>
                  <Grid.Col span={{ base: 12, sm: 6 }}>
                    <TextInput label="Namn" {...form.getInputProps('contractorName')} />
                  </Grid.Col>
                  <Grid.Col span={{ base: 12, sm: 6 }}>
                    <TextInput label="Telefon" {...form.getInputProps('contractorPhone')} />
                  </Grid.Col>
                  <Grid.Col span={{ base: 12, sm: 6 }}>
                    <TextInput label="E-post" type="email" {...form.getInputProps('contractorEmail')} />
                  </Grid.Col>
                  <Grid.Col span={{ base: 12, sm: 6 }}>
                    <TextInput label="Webbplats" {...form.getInputProps('contractorWebsite')} />
                  </Grid.Col>
                  <Grid.Col span={{ base: 12, sm: 6 }}>
                    <NumberInput label="Offererat pris (kr)" min={0} {...form.getInputProps('contractorQuotedPrice')} />
                  </Grid.Col>
                  <Grid.Col span={{ base: 12, sm: 6 }}>
                    <TextInput label="Offertdatum" type="date" {...form.getInputProps('contractorQuotedDate')} />
                  </Grid.Col>
                  <Grid.Col span={12}>
                    <Textarea label="Anteckningar" autosize minRows={2} {...form.getInputProps('contractorNotes')} />
                  </Grid.Col>
                </Grid>
              )}
            </Stack>
          </Card>

          <Card withBorder padding="lg">
            <Stack>
              <Title order={5}>Påverkan</Title>
              <Grid>
                <Grid.Col span={{ base: 12, sm: 4 }}>
                  <NumberInput
                    label="Uppskattad värdeökning (kr)"
                    min={0}
                    {...form.getInputProps('estimatedValueIncrease')}
                  />
                </Grid.Col>
                <Grid.Col span={{ base: 12, sm: 4 }}>
                  <NumberInput label="Förväntad livslängd (år)" min={0} {...form.getInputProps('expectedLifespanYears')} />
                </Grid.Col>
                <Grid.Col span={{ base: 12, sm: 4 }}>
                  <NumberInput
                    label="Energibesparing (%)"
                    min={0}
                    max={100}
                    {...form.getInputProps('energyEfficiencyGainPercent')}
                  />
                </Grid.Col>
              </Grid>
              <Textarea label="Anteckningar" autosize minRows={2} {...form.getInputProps('notes')} />
            </Stack>
          </Card>

          <Group justify="space-between">
            <Button type="submit" loading={saving}>
              {isNew ? 'Skapa projekt' : 'Spara'}
            </Button>
            {!isNew && (
              <Button color="red" variant="light" onClick={() => setConfirmingDelete(true)}>
                Ta bort projekt
              </Button>
            )}
          </Group>
        </Stack>
      </form>

      <ConfirmDialog
        opened={confirmingDelete}
        title="Ta bort projekt"
        message="Projektet tas bort med sina kostnadsposter och entreprenörsuppgifter. Detta kan inte ångras."
        onCancel={() => setConfirmingDelete(false)}
        onConfirm={() => {
          setConfirmingDelete(false)
          if (projectId) {
            deleteProject.mutate(projectId, { onSuccess: () => navigate(projectsPath) })
          }
        }}
      />
    </Stack>
  )
}
