import {
  ActionIcon,
  Button,
  Card,
  Center,
  Group,
  Loader,
  Modal,
  Stack,
  Table,
  Textarea,
  TextInput,
  ThemeIcon,
  Title,
} from '@mantine/core'
import { useForm } from '@mantine/form'
import { notifications } from '@mantine/notifications'
import { IconChartLine, IconEdit, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { Navigate, useParams } from 'react-router-dom'
import type { ValuationEntryDto } from '../api/types'
import { EmptyState } from '../components/common/EmptyState'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import {
  useCreateValuation,
  useDeleteValuation,
  useUpdateValuation,
  useValuations,
} from '../hooks/useValuations'
import { formatCurrency } from '../utils/currency'

interface ValuationFormValues {
  date: string
  value: number | string
  source: string
  notes: string
}

const EMPTY_FORM: ValuationFormValues = { date: '', value: '', source: '', notes: '' }

function ValuationForm({
  initial,
  submitLabel,
  onSubmit,
  submitting,
  /** Inline on the add card, stacked in the edit modal. */
  layout = 'row',
}: {
  initial?: ValuationFormValues
  submitLabel: string
  onSubmit: (values: ValuationFormValues) => void
  submitting: boolean
  layout?: 'row' | 'stack'
}) {
  const form = useForm<ValuationFormValues>({ initialValues: initial ?? EMPTY_FORM })
  const Wrapper = layout === 'row' ? Group : Stack

  return (
    <form onSubmit={form.onSubmit(onSubmit)}>
      <Wrapper align={layout === 'row' ? 'flex-end' : undefined}>
        <TextInput label="Datum" type="date" required {...form.getInputProps('date')} />
        <TextInput label="Värde (kr)" type="number" required {...form.getInputProps('value')} />
        <TextInput label="Källa" placeholder="t.ex. mäklare" {...form.getInputProps('source')} />
        {layout === 'stack' && <Textarea label="Anteckning" autosize minRows={2} {...form.getInputProps('notes')} />}
        <Group justify={layout === 'row' ? undefined : 'flex-end'}>
          <Button type="submit" loading={submitting}>
            {submitLabel}
          </Button>
        </Group>
      </Wrapper>
    </form>
  )
}

export function ValuationsPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const { property, isLoading: loadingProperty, notFound } = useSelectedProperty(propertyId)
  const { data: valuations, isLoading } = useValuations(propertyId ?? '')
  const createValuation = useCreateValuation(propertyId ?? '')
  const updateValuation = useUpdateValuation(propertyId ?? '')
  const deleteValuation = useDeleteValuation(propertyId ?? '')
  const [editing, setEditing] = useState<ValuationEntryDto | null>(null)
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)

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

  function toInput(values: ValuationFormValues) {
    return {
      date: values.date,
      value: Number(values.value) || 0,
      source: values.source.trim() || null,
      notes: values.notes.trim() || null,
    }
  }

  function handleCreate(values: ValuationFormValues) {
    createValuation.mutate(toInput(values))
  }

  function handleUpdate(values: ValuationFormValues) {
    if (!editing) return
    updateValuation.mutate(
      { id: editing.id, input: toInput(values) },
      {
        onSuccess: () => setEditing(null),
        onError: () => notifications.show({ color: 'red', message: 'Kunde inte spara värderingen. Försök igen.' }),
      },
    )
  }

  return (
    <Stack>
      <Group gap="sm">
        <ThemeIcon variant="light" size={36} radius="md">
          <IconChartLine size={20} />
        </ThemeIcon>
        <Title order={2}>Värderingar</Title>
      </Group>

      <Card withBorder padding="md">
        {/* key resets the inline form after each successful add. */}
        <ValuationForm
          key={valuations?.length ?? 0}
          submitLabel="Lägg till"
          onSubmit={handleCreate}
          submitting={createValuation.isPending}
        />
      </Card>

      {!valuations || valuations.length === 0 ? (
        <EmptyState icon={IconChartLine} message="Inga värderingar registrerade ännu." />
      ) : (
        <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
          <Table.ScrollContainer minWidth={600}>
            <Table striped highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Datum</Table.Th>
                  <Table.Th>Värde</Table.Th>
                  <Table.Th>Källa</Table.Th>
                  <Table.Th>Anteckning</Table.Th>
                  <Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {valuations.map((v) => (
                  <Table.Tr key={v.id}>
                    <Table.Td>{v.date}</Table.Td>
                    <Table.Td fw={600}>{formatCurrency(v.value)}</Table.Td>
                    <Table.Td c="dimmed">{v.source ?? '—'}</Table.Td>
                    <Table.Td c="dimmed">{v.notes ?? '—'}</Table.Td>
                    <Table.Td>
                      <ActionIcon variant="subtle" onClick={() => setEditing(v)} mr="xs">
                        <IconEdit size={16} />
                      </ActionIcon>
                      <ActionIcon color="red" variant="subtle" onClick={() => setPendingDeleteId(v.id)}>
                        <IconTrash size={16} />
                      </ActionIcon>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </Card>
      )}

      <Modal opened={editing !== null} onClose={() => setEditing(null)} title="Redigera värdering" centered>
        {editing && (
          <ValuationForm
            key={editing.id}
            layout="stack"
            initial={{
              date: editing.date,
              value: editing.value,
              source: editing.source ?? '',
              notes: editing.notes ?? '',
            }}
            submitLabel="Spara"
            onSubmit={handleUpdate}
            submitting={updateValuation.isPending}
          />
        )}
      </Modal>

      <ConfirmDialog
        opened={pendingDeleteId !== null}
        title="Ta bort värdering"
        message="Detta kan inte ångras."
        onCancel={() => setPendingDeleteId(null)}
        onConfirm={() => {
          if (pendingDeleteId) {
            deleteValuation.mutate(pendingDeleteId)
          }
          setPendingDeleteId(null)
        }}
      />
    </Stack>
  )
}
