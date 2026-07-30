import {
  ActionIcon,
  Alert,
  Badge,
  Button,
  Card,
  Center,
  Group,
  Loader,
  Modal,
  NumberInput,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
  ThemeIcon,
  Title,
  Tooltip,
} from '@mantine/core'
import { useForm } from '@mantine/form'
import { notifications } from '@mantine/notifications'
import { IconAdjustments, IconEdit, IconRefresh, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { Navigate, useParams } from 'react-router-dom'
import { ApiError } from '../api/client'
import type { ComponentOrigin, PropertyLocalComponentDto } from '../api/types'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { SortableTh } from '../components/common/SortableTh'
import { useTableSort } from '../hooks/useTableSort'
import {
  useCreateLocalComponent,
  useDeleteLocalComponent,
  usePropertyComponentSet,
  useSyncComponentsFromCentral,
  useUpdateLocalComponent,
} from '../hooks/usePropertyComponents'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { formatInterval, splitMonths, toMonths, type IntervalUnit } from '../utils/interval'

const INTERVAL_UNIT_OPTIONS: { value: IntervalUnit; label: string }[] = [
  { value: 'years', label: 'år' },
  { value: 'months', label: 'månader' },
]

const ORIGIN_LABELS: Record<ComponentOrigin, string> = {
  Central: 'Central',
  Modified: 'Ändrad',
  Local: 'Egen',
}

const ORIGIN_COLORS: Record<ComponentOrigin, string> = {
  Central: 'gray',
  Modified: 'orange',
  Local: 'blue',
}

interface ComponentFormValues {
  name: string
  intervalValue: number | ''
  intervalUnit: IntervalUnit
}

function toFormValues(component: PropertyLocalComponentDto): ComponentFormValues {
  const { value, unit } = component.recommendedIntervalMonths
    ? splitMonths(component.recommendedIntervalMonths)
    : { value: '' as const, unit: 'years' as IntervalUnit }
  return { name: component.name, intervalValue: value, intervalUnit: unit }
}

function ComponentForm({
  initial,
  submitLabel,
  onSubmit,
  submitting,
}: {
  initial?: ComponentFormValues
  submitLabel: string
  onSubmit: (values: ComponentFormValues) => void
  submitting: boolean
}) {
  const form = useForm<ComponentFormValues>({
    initialValues: initial ?? { name: '', intervalValue: '', intervalUnit: 'years' },
  })

  return (
    <form onSubmit={form.onSubmit(onSubmit)}>
      <Group align="flex-end">
        <TextInput label="Namn" required {...form.getInputProps('name')} />
        <NumberInput
          label="Intervall"
          placeholder="valfritt"
          min={1}
          w={110}
          {...form.getInputProps('intervalValue')}
        />
        <Select
          label="Enhet"
          data={INTERVAL_UNIT_OPTIONS}
          allowDeselect={false}
          w={110}
          {...form.getInputProps('intervalUnit')}
        />
        <Button type="submit" loading={submitting}>
          {submitLabel}
        </Button>
      </Group>
    </form>
  )
}

/** What a Modified row differs from, so the badge isn't just an assertion. */
function centralHint(component: PropertyLocalComponentDto): string | null {
  if (component.origin !== 'Modified') {
    return null
  }
  return `Centralt: ${component.centralName} — ${formatInterval(component.centralIntervalMonths)}`
}

export function PropertyLocalComponentsPage() {
  const { propertyId = '' } = useParams<{ propertyId: string }>()
  const { property, isLoading: loadingProperty, notFound } = useSelectedProperty(propertyId)
  const { data: set, isLoading } = usePropertyComponentSet(propertyId)
  const createComponent = useCreateLocalComponent(propertyId)
  const updateComponent = useUpdateLocalComponent(propertyId)
  const deleteComponent = useDeleteLocalComponent(propertyId)
  const syncFromCentral = useSyncComponentsFromCentral(propertyId)
  const [editing, setEditing] = useState<PropertyLocalComponentDto | null>(null)
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)
  const [confirmSync, setConfirmSync] = useState(false)
  const { sorted, sortProps } = useTableSort(set?.components ?? [], {
    name: (c) => c.name,
    // By the number of months, not the rendered "Vart 8:e år" string.
    interval: (c) => c.recommendedIntervalMonths,
    origin: (c) => c.origin,
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

  function toRequest(values: ComponentFormValues) {
    return {
      name: values.name,
      recommendedIntervalMonths:
        values.intervalValue === '' ? null : toMonths(Number(values.intervalValue), values.intervalUnit),
    }
  }

  function handleCreate(values: ComponentFormValues) {
    createComponent.mutate(toRequest(values))
  }

  function handleUpdate(values: ComponentFormValues) {
    if (!editing) return
    updateComponent.mutate({ id: editing.id, input: toRequest(values) }, { onSuccess: () => setEditing(null) })
  }

  function handleDelete(id: string) {
    deleteComponent.mutate(id, {
      onError: (error) => {
        const message =
          error instanceof ApiError && error.status === 409
            ? 'Komponenten används av minst ett projekt i den här bostaden och kan inte tas bort.'
            : 'Något gick fel. Försök igen.'
        notifications.show({ color: 'red', message })
      },
    })
  }

  const customized = set?.customized ?? false
  const availableFromCentral = set?.availableFromCentralCount ?? 0
  const editingFormValues = editing ? toFormValues(editing) : undefined

  return (
    <Stack>
      <Group justify="space-between">
        <Group gap="sm">
          <ThemeIcon variant="light" size={36} radius="md">
            <IconAdjustments size={20} />
          </ThemeIcon>
          <Title order={2}>Komponenter</Title>
        </Group>
        {/* Hidden until the property has its own list: syncing to central when you already *are*
            central does nothing except stop you tracking future central changes. */}
        {customized && (
          <Button
            variant="light"
            leftSection={<IconRefresh size={16} />}
            loading={syncFromCentral.isPending}
            onClick={() => setConfirmSync(true)}
          >
            Hämta från centrala registret
          </Button>
        )}
      </Group>
      <Text c="dimmed" size="sm">
        Delarna av {property.nickname} som projekt kan höra till, och hur ofta de brukar behöva ses
        över. Underhållsplanen räknas fram från den här listan — ändra ett intervall här om det inte
        stämmer för just den här bostaden.
      </Text>

      {!customized && (
        <Alert variant="light" color="gray">
          Bostaden följer det centrala registret. Så fort du ändrar, lägger till eller tar bort något
          här får den en egen lista, som du sedan uppdaterar när du vill.
        </Alert>
      )}

      {customized && availableFromCentral > 0 && (
        <Alert variant="light" color="blue">
          {availableFromCentral === 1
            ? 'Det finns 1 komponent i det centrala registret som inte används här.'
            : `Det finns ${availableFromCentral} komponenter i det centrala registret som inte används här.`}{' '}
          Hämta från centrala registret för att lägga till dem.
        </Alert>
      )}

      <Card withBorder padding="md">
        <ComponentForm submitLabel="Lägg till" onSubmit={handleCreate} submitting={createComponent.isPending} />
      </Card>

      <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
        <Table.ScrollContainer minWidth={640}>
          <Table striped highlightOnHover verticalSpacing="sm">
            <Table.Thead>
              <Table.Tr>
                <SortableTh {...sortProps('name')}>Namn</SortableTh>
                <SortableTh {...sortProps('interval')}>Rekommenderat intervall</SortableTh>
                <SortableTh {...sortProps('origin')}>Ursprung</SortableTh>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {sorted.map((component) => {
                const hint = centralHint(component)
                return (
                  <Table.Tr key={component.id}>
                    <Table.Td>{component.name}</Table.Td>
                    <Table.Td c="dimmed">{formatInterval(component.recommendedIntervalMonths)}</Table.Td>
                    <Table.Td>
                      <Tooltip label={hint} disabled={hint === null} withArrow>
                        <Badge variant="light" color={ORIGIN_COLORS[component.origin]}>
                          {ORIGIN_LABELS[component.origin]}
                        </Badge>
                      </Tooltip>
                    </Table.Td>
                    <Table.Td>
                      <ActionIcon variant="subtle" onClick={() => setEditing(component)} mr="xs">
                        <IconEdit size={16} />
                      </ActionIcon>
                      <ActionIcon color="red" variant="subtle" onClick={() => setPendingDeleteId(component.id)}>
                        <IconTrash size={16} />
                      </ActionIcon>
                    </Table.Td>
                  </Table.Tr>
                )
              })}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>
      </Card>

      <Modal opened={editing !== null} onClose={() => setEditing(null)} title="Redigera komponent" centered>
        {editingFormValues && (
          <ComponentForm
            key={editing?.id}
            initial={editingFormValues}
            submitLabel="Spara"
            onSubmit={handleUpdate}
            submitting={updateComponent.isPending}
          />
        )}
      </Modal>

      <ConfirmDialog
        opened={pendingDeleteId !== null}
        title="Ta bort komponent"
        message="Komponenten tas bort för den här bostaden. Det centrala registret påverkas inte."
        onCancel={() => setPendingDeleteId(null)}
        onConfirm={() => {
          if (pendingDeleteId) {
            handleDelete(pendingDeleteId)
          }
          setPendingDeleteId(null)
        }}
      />

      <ConfirmDialog
        opened={confirmSync}
        confirmLabel="Hämta"
        title="Hämta från centrala registret"
        message="Komponenter som finns både här och centralt skrivs över med de centrala namnen och intervallen, och komponenter som saknas läggs till. Egna komponenter påverkas inte."
        onCancel={() => setConfirmSync(false)}
        onConfirm={() => {
          setConfirmSync(false)
          syncFromCentral.mutate(undefined, {
            onError: () => notifications.show({ color: 'red', message: 'Kunde inte hämta. Försök igen.' }),
          })
        }}
      />
    </Stack>
  )
}
