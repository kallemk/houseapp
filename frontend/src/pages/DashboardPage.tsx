import { Card, Center, Group, Loader, SimpleGrid, Stack, Text, ThemeIcon, Title } from '@mantine/core'
import { IconHammer, IconHome2, IconTag } from '@tabler/icons-react'
import { useState } from 'react'
import { Navigate, useParams } from 'react-router-dom'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { useValuations } from '../hooks/useValuations'
import { useRenovationEntries } from '../hooks/useRenovationEntries'
import { useDocuments } from '../hooks/useDocuments'
import { PropertyTimeline } from '../components/dashboard/PropertyTimeline'
import { QuickAddModal, type QuickAddRequest } from '../components/dashboard/QuickAddModal'
import { formatCurrency } from '../utils/currency'

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
  const { propertyId } = useParams<{ propertyId: string }>()
  const { property, isLoading, notFound } = useSelectedProperty(propertyId)
  const { data: valuations } = useValuations(propertyId ?? '')
  const { data: renovations } = useRenovationEntries(propertyId ?? '')
  const { data: documents } = useDocuments(propertyId ?? '')
  const [quickAddRequest, setQuickAddRequest] = useState<QuickAddRequest | null>(null)

  if (isLoading) {
    return (
      <Center py="xl">
        <Loader />
      </Center>
    )
  }

  if (notFound || !property) {
    return <Navigate to="/properties" replace />
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
