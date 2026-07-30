import {
  ActionIcon,
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
} from '@mantine/core'
import { useForm } from '@mantine/form'
import { notifications } from '@mantine/notifications'
import { IconAdjustments, IconEdit, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { ApiError } from '../api/client'
import type { PropertyComponentDto } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { SortableTh } from '../components/common/SortableTh'
import { useTableSort } from '../hooks/useTableSort'
import {
  useCreatePropertyComponent,
  useDeletePropertyComponent,
  usePropertyComponents,
  useUpdatePropertyComponent,
} from '../hooks/usePropertyComponents'
import { formatInterval, splitMonths, toMonths, type IntervalUnit } from '../utils/interval'

const INTERVAL_UNIT_OPTIONS: { value: IntervalUnit; label: string }[] = [
  { value: 'years', label: 'år' },
  { value: 'months', label: 'månader' },
]

interface ComponentFormValues {
  name: string
  intervalValue: number | ''
  intervalUnit: IntervalUnit
}

function toFormValues(component: PropertyComponentDto): ComponentFormValues {
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

export function PropertyComponentsPage() {
  // Regular users get a read-only view: the list is worth seeing (it's the vocabulary the projects
  // page is built on), but renaming or deleting one changes what every existing project displays.
  // The API enforces this — GET is open, the mutations are admin-only.
  const { user } = useAuth()
  const isAdmin = user?.isAdmin ?? false
  const { data: components, isLoading } = usePropertyComponents()
  const createComponent = useCreatePropertyComponent()
  const updateComponent = useUpdatePropertyComponent()
  const deleteComponent = useDeletePropertyComponent()
  const [editing, setEditing] = useState<PropertyComponentDto | null>(null)
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)
  const { sorted, sortProps } = useTableSort(components ?? [], {
    name: (c) => c.name,
    // Sort by the number of months, not the rendered "Vart 8:e år" string.
    interval: (c) => c.recommendedIntervalMonths,
  })

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
            ? 'Komponenten används av minst ett projekt och kan inte tas bort.'
            : 'Något gick fel. Försök igen.'
        notifications.show({ color: 'red', message })
      },
    })
  }

  if (isLoading) {
    return (
      <Center py="xl">
        <Loader />
      </Center>
    )
  }

  const editingFormValues = editing ? toFormValues(editing) : undefined

  return (
    <Stack>
      <Group gap="sm">
        <ThemeIcon variant="light" size={36} radius="md">
          <IconAdjustments size={20} />
        </ThemeIcon>
        <Title order={2}>Komponenter</Title>
      </Group>
      <Text c="dimmed" size="sm">
        {isAdmin
          ? 'Hantera vilka delar av bostaden som projekt kan höra till, och hur ofta de brukar behöva ses över.'
          : 'Vilka delar av bostaden som projekt kan höra till, och hur ofta de brukar behöva ses över. Endast administratörer kan ändra dem.'}
      </Text>

      {isAdmin && (
        <Card withBorder padding="md">
          <ComponentForm submitLabel="Lägg till" onSubmit={handleCreate} submitting={createComponent.isPending} />
        </Card>
      )}

      <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
        <Table.ScrollContainer minWidth={520}>
          <Table striped highlightOnHover verticalSpacing="sm">
            <Table.Thead>
              <Table.Tr>
                <SortableTh {...sortProps('name')}>Namn</SortableTh>
                <SortableTh {...sortProps('interval')}>Rekommenderat intervall</SortableTh>
                {isAdmin && <Table.Th />}
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {sorted.map((component) => (
                <Table.Tr key={component.id}>
                  <Table.Td>{component.name}</Table.Td>
                  <Table.Td c="dimmed">{formatInterval(component.recommendedIntervalMonths)}</Table.Td>
                  {isAdmin && (
                    <Table.Td>
                      <ActionIcon variant="subtle" onClick={() => setEditing(component)} mr="xs">
                        <IconEdit size={16} />
                      </ActionIcon>
                      <ActionIcon color="red" variant="subtle" onClick={() => setPendingDeleteId(component.id)}>
                        <IconTrash size={16} />
                      </ActionIcon>
                    </Table.Td>
                  )}
                </Table.Tr>
              ))}
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
        message="Detta kan inte ångras."
        onCancel={() => setPendingDeleteId(null)}
        onConfirm={() => {
          if (pendingDeleteId) {
            handleDelete(pendingDeleteId)
          }
          setPendingDeleteId(null)
        }}
      />
    </Stack>
  )
}
