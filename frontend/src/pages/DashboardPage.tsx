import { Button, Card, Group, SimpleGrid, Stack, Text, TextInput, Title } from '@mantine/core'
import { useForm } from '@mantine/form'
import { Center, Loader, ThemeIcon } from '@mantine/core'
import { IconHammer, IconHome2, IconTag } from '@tabler/icons-react'
import { useState } from 'react'
import { usePrimaryProperty } from '../hooks/usePrimaryProperty'
import { useCreateProperty } from '../hooks/useProperties'
import { useValuations } from '../hooks/useValuations'
import { useRenovationEntries } from '../hooks/useRenovationEntries'
import { useDocuments } from '../hooks/useDocuments'
import { EmptyState } from '../components/common/EmptyState'
import { PropertyTimeline } from '../components/dashboard/PropertyTimeline'
import { QuickAddModal, type QuickAddRequest } from '../components/dashboard/QuickAddModal'

// No currency is configured anywhere in the data model, so this formats a plain grouped number
// rather than guessing a currency symbol.
function formatCurrency(value: number) {
  return new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 0 }).format(value)
}

function CreatePropertyForm() {
  const createProperty = useCreateProperty()
  const form = useForm({
    initialValues: { nickname: '', address: '', purchaseDate: '', purchasePrice: 0 },
  })

  return (
    <EmptyState
      icon={IconHome2}
      message="Ingen bostad ännu — lägg till huset för att börja spåra det."
      action={
        <form
          onSubmit={form.onSubmit((values) =>
            createProperty.mutate({ ...values, purchasePrice: Number(values.purchasePrice) }),
          )}
        >
          <Stack w={320}>
            <TextInput label="Smeknamn" required {...form.getInputProps('nickname')} />
            <TextInput label="Adress" required {...form.getInputProps('address')} />
            <TextInput label="Köpdatum" type="date" required {...form.getInputProps('purchaseDate')} />
            <TextInput label="Köpeskilling" type="number" required {...form.getInputProps('purchasePrice')} />
            <Button type="submit" loading={createProperty.isPending}>
              Lägg till bostad
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
  const { data: documents } = useDocuments(property?.id ?? '')
  const [quickAddRequest, setQuickAddRequest] = useState<QuickAddRequest | null>(null)

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

  return (
    <Stack>
      <Title order={2}>{property.nickname}</Title>
      <Text c="dimmed">{property.address}</Text>

      <SimpleGrid cols={{ base: 1, sm: 3 }} mt="md">
        <StatCard icon={IconHome2} label="Nuvarande värde" value={formatCurrency(currentValue)} />
        <StatCard icon={IconTag} label="Köpeskilling" value={formatCurrency(property.purchasePrice)} />
        <StatCard icon={IconHammer} label="Totalt investerat" value={formatCurrency(totalInvested)} />
      </SimpleGrid>

      <Title order={4} mt="lg">
        Tidslinje
      </Title>
      <Card withBorder padding="lg">
        <PropertyTimeline
          purchaseDate={property.purchaseDate}
          valuations={valuations ?? []}
          renovations={renovations ?? []}
          documents={documents ?? []}
          onQuickAdd={setQuickAddRequest}
        />
      </Card>

      <QuickAddModal propertyId={property.id} request={quickAddRequest} onClose={() => setQuickAddRequest(null)} />
    </Stack>
  )
}
