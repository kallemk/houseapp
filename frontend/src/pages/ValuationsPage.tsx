import { ActionIcon, Button, Card, Center, Group, Loader, Stack, Table, TextInput, ThemeIcon, Title } from '@mantine/core'
import { useForm } from '@mantine/form'
import { IconChartLine, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { EmptyState } from '../components/common/EmptyState'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { usePrimaryProperty } from '../hooks/usePrimaryProperty'
import { useCreateValuation, useDeleteValuation, useValuations } from '../hooks/useValuations'

interface ValuationFormValues {
  date: string
  value: number
  source: string
  notes: string
}

export function ValuationsPage() {
  const { property, isLoading: loadingProperty } = usePrimaryProperty()
  const propertyId = property?.id ?? ''
  const { data: valuations, isLoading } = useValuations(propertyId)
  const createValuation = useCreateValuation(propertyId)
  const deleteValuation = useDeleteValuation(propertyId)
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

  if (!property) {
    return <EmptyState message="Add a property on the Dashboard first." />
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
        <Title order={2}>Valuations</Title>
      </Group>

      <Card withBorder padding="md">
        <form onSubmit={form.onSubmit(handleSubmit)}>
          <Group align="flex-end">
            <TextInput label="Date" type="date" required {...form.getInputProps('date')} />
            <TextInput label="Value" type="number" required {...form.getInputProps('value')} />
            <TextInput label="Source" placeholder="e.g. Appraisal" {...form.getInputProps('source')} />
            <Button type="submit" loading={createValuation.isPending}>
              Add
            </Button>
          </Group>
        </form>
      </Card>

      {!valuations || valuations.length === 0 ? (
        <EmptyState icon={IconChartLine} message="No valuations logged yet." />
      ) : (
        <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
          <Table striped highlightOnHover verticalSpacing="sm">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Date</Table.Th>
                <Table.Th>Value</Table.Th>
                <Table.Th>Source</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {valuations.map((v) => (
                <Table.Tr key={v.id}>
                  <Table.Td>{v.date}</Table.Td>
                  <Table.Td fw={600}>{v.value.toLocaleString()}</Table.Td>
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
        </Card>
      )}

      <ConfirmDialog
        opened={pendingDeleteId !== null}
        title="Delete valuation"
        message="This can't be undone."
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
