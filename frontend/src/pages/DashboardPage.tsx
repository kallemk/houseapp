import { Button, Card, Group, SimpleGrid, Stack, Text, TextInput, Title } from '@mantine/core'
import { useForm } from '@mantine/form'
import { Center, Loader, ThemeIcon } from '@mantine/core'
import { IconChartLine, IconHammer, IconHome2, IconTag } from '@tabler/icons-react'
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
      icon={IconHome2}
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

interface StatCardProps {
  icon: typeof IconHome2
  label: string
  value: string
}

function StatCard({ icon: Icon, label, value }: StatCardProps) {
  return (
    <Card withBorder padding="lg">
      <Group gap="sm">
        <ThemeIcon variant="light" size={40} radius="md">
          <Icon size={20} />
        </ThemeIcon>
        <div>
          <Text size="sm" c="dimmed">
            {label}
          </Text>
          <Text size="xl" fw={700}>
            {value}
          </Text>
        </div>
      </Group>
    </Card>
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
    ...(valuations ?? []).map((v) => ({
      date: v.date,
      label: `Valuation logged: ${formatCurrency(v.value)}`,
      icon: IconChartLine,
    })),
    ...(renovations ?? []).map((r) => ({
      date: r.date,
      label: `${r.title}: ${formatCurrency(r.amount)}`,
      icon: IconHammer,
    })),
  ]
    .sort((a, b) => b.date.localeCompare(a.date))
    .slice(0, 5)

  return (
    <Stack>
      <Title order={2}>{property.nickname}</Title>
      <Text c="dimmed">{property.address}</Text>

      <SimpleGrid cols={{ base: 1, sm: 3 }} mt="md">
        <StatCard icon={IconHome2} label="Current value" value={formatCurrency(currentValue)} />
        <StatCard icon={IconTag} label="Purchase price" value={formatCurrency(property.purchasePrice)} />
        <StatCard icon={IconHammer} label="Total invested" value={formatCurrency(totalInvested)} />
      </SimpleGrid>

      <Title order={4} mt="lg">
        Recent activity
      </Title>
      {recentActivity.length === 0 ? (
        <EmptyState message="No valuations or renovation entries logged yet." />
      ) : (
        <Card withBorder padding="sm">
          <Stack gap={0}>
            {recentActivity.map((item, index) => (
              <Group
                key={index}
                justify="space-between"
                py="xs"
                px="xs"
                style={
                  index < recentActivity.length - 1
                    ? { borderBottom: '1px solid var(--mantine-color-gray-1)' }
                    : undefined
                }
              >
                <Group gap="sm">
                  <ThemeIcon variant="light" size={30} radius="md">
                    <item.icon size={16} />
                  </ThemeIcon>
                  <Text size="sm">{item.label}</Text>
                </Group>
                <Text c="dimmed" size="sm">
                  {item.date}
                </Text>
              </Group>
            ))}
          </Stack>
        </Card>
      )}
    </Stack>
  )
}
