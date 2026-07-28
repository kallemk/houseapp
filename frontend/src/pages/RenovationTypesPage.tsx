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
import type { RenovationTypeDto } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import {
  useCreateRenovationType,
  useDeleteRenovationType,
  useRenovationTypes,
  useUpdateRenovationType,
} from '../hooks/useRenovationTypes'
import { formatInterval, splitMonths, toMonths, type IntervalUnit } from '../utils/interval'

const INTERVAL_UNIT_OPTIONS: { value: IntervalUnit; label: string }[] = [
  { value: 'years', label: 'år' },
  { value: 'months', label: 'månader' },
]

interface TypeFormValues {
  name: string
  intervalValue: number | ''
  intervalUnit: IntervalUnit
}

function toFormValues(type: RenovationTypeDto): TypeFormValues {
  const { value, unit } = type.recommendedIntervalMonths
    ? splitMonths(type.recommendedIntervalMonths)
    : { value: '' as const, unit: 'years' as IntervalUnit }
  return { name: type.name, intervalValue: value, intervalUnit: unit }
}

function TypeForm({
  initial,
  submitLabel,
  onSubmit,
  submitting,
}: {
  initial?: TypeFormValues
  submitLabel: string
  onSubmit: (values: TypeFormValues) => void
  submitting: boolean
}) {
  const form = useForm<TypeFormValues>({
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

export function RenovationTypesPage() {
  // Regular users get a read-only view: types are worth seeing (they're the vocabulary the whole
  // renovations page is built on), but renaming or deleting one changes what every existing entry
  // displays for everyone. The API enforces this — GET is open, the mutations are admin-only.
  const { user } = useAuth()
  const isAdmin = user?.isAdmin ?? false
  const { data: types, isLoading } = useRenovationTypes()
  const createType = useCreateRenovationType()
  const updateType = useUpdateRenovationType()
  const deleteType = useDeleteRenovationType()
  const [editing, setEditing] = useState<RenovationTypeDto | null>(null)
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)

  function toRequest(values: TypeFormValues) {
    return {
      name: values.name,
      recommendedIntervalMonths: values.intervalValue === '' ? null : toMonths(Number(values.intervalValue), values.intervalUnit),
    }
  }

  function handleCreate(values: TypeFormValues) {
    createType.mutate(toRequest(values))
  }

  function handleUpdate(values: TypeFormValues) {
    if (!editing) return
    updateType.mutate({ id: editing.id, input: toRequest(values) }, { onSuccess: () => setEditing(null) })
  }

  function handleDelete(id: string) {
    deleteType.mutate(id, {
      onError: (error) => {
        const message =
          error instanceof ApiError && error.status === 409
            ? 'Typen används av minst en post och kan inte tas bort.'
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
        <Title order={2}>Renoveringstyper</Title>
      </Group>
      <Text c="dimmed" size="sm">
        {isAdmin
          ? 'Hantera vilka typer av renoveringar/underhåll som är relevanta, och hur ofta de brukar behövas.'
          : 'Vilka typer av renoveringar/underhåll som är relevanta, och hur ofta de brukar behövas. Endast administratörer kan ändra dem.'}
      </Text>

      {isAdmin && (
        <Card withBorder padding="md">
          <TypeForm submitLabel="Lägg till" onSubmit={handleCreate} submitting={createType.isPending} />
        </Card>
      )}

      <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
        <Table striped highlightOnHover verticalSpacing="sm">
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Namn</Table.Th>
              <Table.Th>Rekommenderat intervall</Table.Th>
              {isAdmin && <Table.Th />}
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {types?.map((type) => (
              <Table.Tr key={type.id}>
                <Table.Td>{type.name}</Table.Td>
                <Table.Td c="dimmed">{formatInterval(type.recommendedIntervalMonths)}</Table.Td>
                {isAdmin && (
                  <Table.Td>
                    <ActionIcon variant="subtle" onClick={() => setEditing(type)} mr="xs">
                      <IconEdit size={16} />
                    </ActionIcon>
                    <ActionIcon color="red" variant="subtle" onClick={() => setPendingDeleteId(type.id)}>
                      <IconTrash size={16} />
                    </ActionIcon>
                  </Table.Td>
                )}
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      </Card>

      <Modal opened={editing !== null} onClose={() => setEditing(null)} title="Redigera typ" centered>
        {editingFormValues && (
          <TypeForm
            key={editing?.id}
            initial={editingFormValues}
            submitLabel="Spara"
            onSubmit={handleUpdate}
            submitting={updateType.isPending}
          />
        )}
      </Modal>

      <ConfirmDialog
        opened={pendingDeleteId !== null}
        title="Ta bort typ"
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
