import {
  ActionIcon,
  Button,
  Center,
  Group,
  Loader,
  Select,
  Stack,
  Table,
  TextInput,
  Title,
} from '@mantine/core'
import { useForm } from '@mantine/form'
import { IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { EmptyState } from '../components/common/EmptyState'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { usePrimaryProperty } from '../hooks/usePrimaryProperty'
import { useCreateRenovationEntry, useDeleteRenovationEntry, useRenovationEntries } from '../hooks/useRenovationEntries'
import type { RenovationCategory } from '../api/types'

const CATEGORY_OPTIONS: RenovationCategory[] = ['Renovation', 'Maintenance', 'Furniture', 'Other']

interface RenovationFormValues {
  date: string
  category: RenovationCategory
  title: string
  amount: number
  vendor: string
}

export function RenovationsPage() {
  const { property, isLoading: loadingProperty } = usePrimaryProperty()
  const propertyId = property?.id ?? ''
  const { data: entries, isLoading } = useRenovationEntries(propertyId)
  const createEntry = useCreateRenovationEntry(propertyId)
  const deleteEntry = useDeleteRenovationEntry(propertyId)
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)

  const form = useForm<RenovationFormValues>({
    initialValues: { date: '', category: 'Renovation', title: '', amount: 0, vendor: '' },
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

  function handleSubmit(values: RenovationFormValues) {
    createEntry.mutate(
      {
        date: values.date,
        category: values.category,
        title: values.title,
        amount: Number(values.amount),
        vendor: values.vendor || null,
      },
      { onSuccess: () => form.reset() },
    )
  }

  return (
    <Stack>
      <Title order={2}>Renovations &amp; investments</Title>

      <form onSubmit={form.onSubmit(handleSubmit)}>
        <Group align="flex-end">
          <TextInput label="Date" type="date" required {...form.getInputProps('date')} />
          <Select label="Category" data={CATEGORY_OPTIONS} allowDeselect={false} {...form.getInputProps('category')} />
          <TextInput label="Title" required {...form.getInputProps('title')} />
          <TextInput label="Amount" type="number" required {...form.getInputProps('amount')} />
          <TextInput label="Vendor" {...form.getInputProps('vendor')} />
          <Button type="submit" loading={createEntry.isPending}>
            Add
          </Button>
        </Group>
      </form>

      {!entries || entries.length === 0 ? (
        <EmptyState message="No renovation entries logged yet." />
      ) : (
        <Table striped highlightOnHover>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Date</Table.Th>
              <Table.Th>Category</Table.Th>
              <Table.Th>Title</Table.Th>
              <Table.Th>Amount</Table.Th>
              <Table.Th>Vendor</Table.Th>
              <Table.Th />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {entries.map((entry) => (
              <Table.Tr key={entry.id}>
                <Table.Td>{entry.date}</Table.Td>
                <Table.Td>{entry.category}</Table.Td>
                <Table.Td>{entry.title}</Table.Td>
                <Table.Td>{entry.amount.toLocaleString()}</Table.Td>
                <Table.Td>{entry.vendor ?? '—'}</Table.Td>
                <Table.Td>
                  <ActionIcon color="red" variant="subtle" onClick={() => setPendingDeleteId(entry.id)}>
                    <IconTrash size={16} />
                  </ActionIcon>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      )}

      <ConfirmDialog
        opened={pendingDeleteId !== null}
        title="Delete entry"
        message="This can't be undone."
        onCancel={() => setPendingDeleteId(null)}
        onConfirm={() => {
          if (pendingDeleteId) {
            deleteEntry.mutate(pendingDeleteId)
          }
          setPendingDeleteId(null)
        }}
      />
    </Stack>
  )
}
