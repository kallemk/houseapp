import { Button, Card, Group, SimpleGrid, Stack, Text, TextInput, Title } from '@mantine/core'
import { useForm } from '@mantine/form'
import { Center, Loader } from '@mantine/core'
import { usePrimaryProperty } from '../hooks/usePrimaryProperty'
import { useCreateProperty } from '../hooks/useProperties'
import { useValuations } from '../hooks/useValuations'
import { useRenovationEntries } from '../hooks/useRenovationEntries'
import { EmptyState } from '../components/common/EmptyState'

// No currency is configured anywhere in the data model, so this formats a plain grouped number
// rather than guessing a currency symbol.
function formatCurrency(value: number) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 }).format(value)
}

function CreatePropertyForm() {
  const createProperty = useCreateProperty()
  const form = useForm({
    initialValues: { nickname: '', address: '', purchaseDate: '', purchasePrice: 0 },
  })

  return (
    <EmptyState
      message="No property yet — add the house to start tracking it."
      action={
        <form
          onSubmit={form.onSubmit((values) =>
            createProperty.mutate({ ...values, purchasePrice: Number(values.purchasePrice) }),
          )}
        >
          <Stack w={320}>
            <TextInput label="Nickname" required {...form.getInputProps('nickname')} />
            <TextInput label="Address" required {...form.getInputProps('address')} />
            <TextInput label="Purchase date" type="date" required {...form.getInputProps('purchaseDate')} />
            <TextInput label="Purchase price" type="number" required {...form.getInputProps('purchasePrice')} />
            <Button type="submit" loading={createProperty.isPending}>
              Add property
            </Button>
          </Stack>
        </form>
      }
    />
  )
}

export function DashboardPage() {
  const { property, isLoading } = usePrimaryProperty()
  const { data: valuations } = useValuations(property?.id ?? '')
  const { data: renovations } = useRenovationEntries(property?.id ?? '')

  if (isLoading) {
    return (
      <Center py="xl">
        <Loader />
      </Center>
    )
  }

  if (!property) {
    return <CreatePropertyForm />
  }

  const currentValue = valuations?.[0]?.value ?? property.purchasePrice
  const totalInvested = (renovations ?? []).reduce((sum, r) => sum + r.amount, 0)

  const recentActivity = [
    ...(valuations ?? []).map((v) => ({ date: v.date, label: `Valuation logged: ${formatCurrency(v.value)}` })),
    ...(renovations ?? []).map((r) => ({ date: r.date, label: `${r.title}: ${formatCurrency(r.amount)}` })),
  ]
    .sort((a, b) => b.date.localeCompare(a.date))
    .slice(0, 5)

  return (
    <Stack>
      <Title order={2}>{property.nickname}</Title>
      <Text c="dimmed">{property.address}</Text>

      <SimpleGrid cols={{ base: 1, sm: 3 }} mt="md">
        <Card withBorder padding="lg">
          <Text size="sm" c="dimmed">
            Current value
          </Text>
          <Text size="xl" fw={700}>
            {formatCurrency(currentValue)}
          </Text>
        </Card>
        <Card withBorder padding="lg">
          <Text size="sm" c="dimmed">
            Purchase price
          </Text>
          <Text size="xl" fw={700}>
            {formatCurrency(property.purchasePrice)}
          </Text>
        </Card>
        <Card withBorder padding="lg">
          <Text size="sm" c="dimmed">
            Total invested
          </Text>
          <Text size="xl" fw={700}>
            {formatCurrency(totalInvested)}
          </Text>
        </Card>
      </SimpleGrid>

      <Title order={4} mt="lg">
        Recent activity
      </Title>
      {recentActivity.length === 0 ? (
        <EmptyState message="No valuations or renovation entries logged yet." />
      ) : (
        <Stack gap="xs">
          {recentActivity.map((item, index) => (
            <Group key={index} justify="space-between">
              <Text>{item.label}</Text>
              <Text c="dimmed" size="sm">
                {item.date}
              </Text>
            </Group>
          ))}
        </Stack>
      )}
    </Stack>
  )
}
