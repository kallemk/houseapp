import {
  ActionIcon,
  Badge,
  Button,
  Card,
  Center,
  Group,
  Loader,
  Select,
  Stack,
  Table,
  TextInput,
  ThemeIcon,
  Title,
} from '@mantine/core'
import { useForm } from '@mantine/form'
import { IconHammer, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { Navigate, useParams } from 'react-router-dom'
import { EmptyState } from '../components/common/EmptyState'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { useCreateRenovationEntry, useDeleteRenovationEntry, useRenovationEntries } from '../hooks/useRenovationEntries'
import type { RenovationCategory } from '../api/types'
import { RENOVATION_CATEGORY_LABELS, RENOVATION_CATEGORY_OPTIONS } from '../utils/labels'
import { formatCurrency } from '../utils/currency'

const CATEGORY_COLORS: Record<RenovationCategory, string> = {
  Renovation: 'terracotta',
  Maintenance: 'blue',
  Furniture: 'grape',
  Other: 'gray',
}

interface RenovationFormValues {
  date: string
  category: RenovationCategory
  title: string
  amount: number
  vendor: string
}

export function RenovationsPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const { property, isLoading: loadingProperty, notFound } = useSelectedProperty(propertyId)
  const { data: entries, isLoading } = useRenovationEntries(propertyId ?? '')
  const createEntry = useCreateRenovationEntry(propertyId ?? '')
  const deleteEntry = useDeleteRenovationEntry(propertyId ?? '')
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

  if (notFound || !property) {
    return <Navigate to="/properties" replace />
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
      <Group gap="sm">
        <ThemeIcon variant="light" size={36} radius="md">
          <IconHammer size={20} />
        </ThemeIcon>
        <Title order={2}>Renoveringar &amp; investeringar</Title>
      </Group>

      <Card withBorder padding="md">
        <form onSubmit={form.onSubmit(handleSubmit)}>
          <Group align="flex-end">
            <TextInput label="Datum" type="date" required {...form.getInputProps('date')} />
            <Select
              label="Kategori"
              data={RENOVATION_CATEGORY_OPTIONS}
              allowDeselect={false}
              {...form.getInputProps('category')}
            />
            <TextInput label="Titel" required {...form.getInputProps('title')} />
            <TextInput label="Belopp (kr)" type="number" required {...form.getInputProps('amount')} />
            <TextInput label="Leverantör" {...form.getInputProps('vendor')} />
            <Button type="submit" loading={createEntry.isPending}>
              Lägg till
            </Button>
          </Group>
        </form>
      </Card>

      {!entries || entries.length === 0 ? (
        <EmptyState icon={IconHammer} message="Inga renoveringar registrerade ännu." />
      ) : (
        <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
          <Table striped highlightOnHover verticalSpacing="sm">
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Datum</Table.Th>
                <Table.Th>Kategori</Table.Th>
                <Table.Th>Titel</Table.Th>
                <Table.Th>Belopp</Table.Th>
                <Table.Th>Leverantör</Table.Th>
                <Table.Th />
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {entries.map((entry) => (
                <Table.Tr key={entry.id}>
                  <Table.Td>{entry.date}</Table.Td>
                  <Table.Td>
                    <Badge color={CATEGORY_COLORS[entry.category]} variant="light">
                      {RENOVATION_CATEGORY_LABELS[entry.category]}
                    </Badge>
                  </Table.Td>
                  <Table.Td>{entry.title}</Table.Td>
                  <Table.Td fw={600}>{formatCurrency(entry.amount)}</Table.Td>
                  <Table.Td c="dimmed">{entry.vendor ?? '—'}</Table.Td>
                  <Table.Td>
                    <ActionIcon color="red" variant="subtle" onClick={() => setPendingDeleteId(entry.id)}>
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
        title="Ta bort post"
        message="Detta kan inte ångras."
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
