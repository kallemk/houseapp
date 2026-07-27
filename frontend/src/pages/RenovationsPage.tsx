import {
  ActionIcon,
  Anchor,
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
import { IconHammer, IconSettings, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { EmptyState } from '../components/common/EmptyState'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { useCreateRenovationEntry, useDeleteRenovationEntry, useRenovationEntries } from '../hooks/useRenovationEntries'
import { useRenovationTypes } from '../hooks/useRenovationTypes'
import { formatCurrency } from '../utils/currency'
import { colorForId } from '../utils/typeColor'

interface RenovationFormValues {
  date: string
  renovationTypeId: string
  title: string
  amount: number
  vendor: string
}

export function RenovationsPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const { property, isLoading: loadingProperty, notFound } = useSelectedProperty(propertyId)
  const { data: entries, isLoading } = useRenovationEntries(propertyId ?? '')
  const { data: types, isLoading: loadingTypes } = useRenovationTypes()
  const createEntry = useCreateRenovationEntry(propertyId ?? '')
  const deleteEntry = useDeleteRenovationEntry(propertyId ?? '')
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)

  const form = useForm<RenovationFormValues>({
    initialValues: { date: '', renovationTypeId: '', title: '', amount: 0, vendor: '' },
    validate: {
      renovationTypeId: (value) => (value ? null : 'Välj en typ'),
    },
  })

  if (loadingProperty || isLoading || loadingTypes) {
    return (
      <Center py="xl">
        <Loader />
      </Center>
    )
  }

  if (notFound || !property) {
    return <Navigate to="/properties" replace />
  }

  const typeOptions = (types ?? []).map((t) => ({ value: t.id, label: t.name }))
  const typesById = new Map((types ?? []).map((t) => [t.id, t]))

  function handleSubmit(values: RenovationFormValues) {
    createEntry.mutate(
      {
        date: values.date,
        renovationTypeId: values.renovationTypeId,
        title: values.title,
        amount: Number(values.amount),
        vendor: values.vendor || null,
      },
      { onSuccess: () => form.reset() },
    )
  }

  return (
    <Stack>
      <Group justify="space-between">
        <Group gap="sm">
          <ThemeIcon variant="light" size={36} radius="md">
            <IconHammer size={20} />
          </ThemeIcon>
          <Title order={2}>Renoveringar &amp; investeringar</Title>
        </Group>
        <Anchor component={Link} to={`/properties/${propertyId}/renovation-types`} size="sm">
          <Group gap={4}>
            <IconSettings size={14} />
            Hantera typer
          </Group>
        </Anchor>
      </Group>

      <Card withBorder padding="md">
        <form onSubmit={form.onSubmit(handleSubmit)}>
          <Group align="flex-end">
            <TextInput label="Datum" type="date" required {...form.getInputProps('date')} />
            <Select
              label="Typ"
              placeholder="Välj typ"
              data={typeOptions}
              {...form.getInputProps('renovationTypeId')}
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
                <Table.Th>Typ</Table.Th>
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
                    <Badge color={colorForId(entry.renovationTypeId)} variant="light">
                      {typesById.get(entry.renovationTypeId)?.name ?? 'Okänd typ'}
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
