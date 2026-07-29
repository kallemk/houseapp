import { ActionIcon, Button, Card, Center, Group, Loader, Stack, Table, TextInput, ThemeIcon, Title } from '@mantine/core'
import { useForm } from '@mantine/form'
import { IconChartLine, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { Navigate, useParams } from 'react-router-dom'
import { EmptyState } from '../components/common/EmptyState'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { useCreateValuation, useDeleteValuation, useValuations } from '../hooks/useValuations'
import { formatCurrency } from '../utils/currency'

interface ValuationFormValues {
  date: string
  value: number
  source: string
  notes: string
}

export function ValuationsPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const { property, isLoading: loadingProperty, notFound } = useSelectedProperty(propertyId)
  const { data: valuations, isLoading } = useValuations(propertyId ?? '')
  const createValuation = useCreateValuation(propertyId ?? '')
  const deleteValuation = useDeleteValuation(propertyId ?? '')
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)

  const form = useForm<ValuationFormValues>({
    initialValues: { date: '', value: 0, source: '', notes: '' },
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

  function handleSubmit(values: ValuationFormValues) {
    createValuation.mutate(
      { date: values.date, value: Number(values.value), source: values.source || null, notes: values.notes || null },
      { onSuccess: () => form.reset() },
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
        <form onSubmit={form.onSubmit(handleSubmit)}>
          <Group align="flex-end">
            <TextInput label="Datum" type="date" required {...form.getInputProps('date')} />
            <TextInput label="Värde (kr)" type="number" required {...form.getInputProps('value')} />
            <TextInput label="Källa" placeholder="t.ex. värdering" {...form.getInputProps('source')} />
            <Button type="submit" loading={createValuation.isPending}>
              Lägg till
            </Button>
          </Group>
        </form>
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
                  <Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {valuations.map((v) => (
                  <Table.Tr key={v.id}>
                    <Table.Td>{v.date}</Table.Td>
                    <Table.Td fw={600}>{formatCurrency(v.value)}</Table.Td>
                    <Table.Td c="dimmed">{v.source ?? '—'}</Table.Td>
                    <Table.Td>
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
