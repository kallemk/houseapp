import { Alert, Anchor, Badge, Card, Center, Group, Loader, SimpleGrid, Stack, Text, ThemeIcon, Title } from '@mantine/core'
import {
  IconBuildingCommunity,
  IconCalendarClock,
  IconHammer,
  IconHome2,
  IconTag,
  IconTool,
  IconTrendingDown,
  IconTrendingUp,
} from '@tabler/icons-react'
import { useState, type ReactNode } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import type { WorkType } from '../api/types'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { useValuations } from '../hooks/useValuations'
import { useProjects } from '../hooks/useProjects'
import { useDocuments } from '../hooks/useDocuments'
import { useMaintenanceSchedule } from '../hooks/useMaintenanceSchedule'
import { formatAddress } from '../utils/address'
import {
  MAINTENANCE_URGENCY_COLORS,
  MAINTENANCE_URGENCY_LABELS,
  PROJECT_STATUS_COLORS,
  PROJECT_STATUS_LABELS,
} from '../utils/labels'
import { PropertyMap } from '../components/dashboard/PropertyMap'
import { PropertyTimeline } from '../components/dashboard/PropertyTimeline'
import { QuickAddModal, type QuickAddRequest } from '../components/dashboard/QuickAddModal'
import { formatCurrency, formatNumber } from '../utils/currency'

interface StatCardProps {
  icon: typeof IconHome2
  label: string
  value: string
  /** Optional second line — used for the change against the purchase price. */
  footer?: ReactNode
}

function StatCard({ icon: Icon, label, value, footer }: StatCardProps) {
  return (
    <Card withBorder padding="lg">
      <Group gap="sm" wrap="nowrap">
        <ThemeIcon variant="light" size={40} radius="md">
          <Icon size={20} />
        </ThemeIcon>
        <div style={{ minWidth: 0 }}>
          <Text size="sm" c="dimmed">
            {label}
          </Text>
          <Text size="xl" fw={700}>
            {value}
          </Text>
          {footer}
        </div>
      </Group>
    </Card>
  )
}

/**
 * Change from purchase price to latest valuation. Deliberately not shown when there's no valuation:
 * `currentValue` falls back to the purchase price, and rendering that as a confident "0 %" would be
 * stating "your house hasn't changed in value" when the truth is "nobody has valued it".
 */
function ValueChange({ purchasePrice, currentValue }: { purchasePrice: number; currentValue: number }) {
  if (purchasePrice <= 0) {
    return null
  }

  const difference = currentValue - purchasePrice
  const percent = (difference / purchasePrice) * 100
  const up = difference >= 0
  const color = difference === 0 ? 'gray' : up ? 'teal' : 'red'
  const Icon = up ? IconTrendingUp : IconTrendingDown

  return (
    <Group gap={4} mt={2} wrap="nowrap">
      <ThemeIcon size={18} radius="xl" variant="light" color={color}>
        <Icon size={12} />
      </ThemeIcon>
      <Text size="sm" fw={600} c={color}>
        {up ? '+' : '−'}
        {formatNumber(Math.abs(percent), 1)} %
      </Text>
      <Text size="xs" c="dimmed">
        ({up ? '+' : '−'}
        {formatCurrency(Math.abs(difference))} sedan köpet)
      </Text>
    </Group>
  )
}

export function DashboardPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const { property, isLoading, notFound } = useSelectedProperty(propertyId)
  const { data: valuations } = useValuations(propertyId ?? '')
  const { data: projects } = useProjects(propertyId ?? '')
  const { data: documents } = useDocuments(propertyId ?? '')
  const { data: schedule } = useMaintenanceSchedule(propertyId ?? '')
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

  const hasValuation = (valuations?.length ?? 0) > 0
  const currentValue = valuations?.[0]?.value ?? property.purchasePrice

  // Split by what kind of work it was, rather than lumping everything into "invested" — routine
  // maintenance isn't money that went into the house's value.
  const spentByWorkType = (workTypes: WorkType[]) =>
    (projects ?? [])
      .filter((p) => workTypes.includes(p.workType))
      .reduce((sum, p) => sum + p.actualCost, 0)

  const invested = spentByWorkType(['Renovation', 'Investment'])
  const maintenance = spentByWorkType(['Maintenance'])
  const openProjects = (projects ?? []).filter((p) => p.status !== 'Completed' && p.status !== 'Cancelled')
  const needsAttention = (schedule ?? []).filter((i) => i.urgency === 'Overdue' || i.urgency === 'DueSoon')

  return (
    <Stack>
      <Group gap="sm" align="center">
        <Title order={2}>{property.nickname}</Title>
        {property.yearBuilt && (
          <Badge variant="light" color="gray">
            Byggt {property.yearBuilt}
          </Badge>
        )}
      </Group>
      <Text c="dimmed">{formatAddress(property)}</Text>
      {property.propertyDesignation && (
        <Text c="dimmed" size="sm">
          Fastighetsbeteckning: {property.propertyDesignation}
        </Text>
      )}

      <SimpleGrid cols={{ base: 1, sm: 2, lg: 4 }} mt="md">
        <StatCard
          icon={IconHome2}
          label="Nuvarande värde"
          value={formatCurrency(currentValue)}
          footer={
            hasValuation ? (
              <ValueChange purchasePrice={property.purchasePrice} currentValue={currentValue} />
            ) : (
              <Text size="xs" c="dimmed" mt={2}>
                Ingen värdering ännu — visar köpeskillingen
              </Text>
            )
          }
        />
        <StatCard icon={IconTag} label="Köpeskilling" value={formatCurrency(property.purchasePrice)} />
        <StatCard icon={IconHammer} label="Renovering & investering" value={formatCurrency(invested)} />
        <StatCard icon={IconTool} label="Underhållskostnad" value={formatCurrency(maintenance)} />
      </SimpleGrid>

      {openProjects.length > 0 && (
        <Card withBorder padding="lg">
          <Group gap="sm" mb="sm">
            <ThemeIcon variant="light" size={40} radius="md">
              <IconBuildingCommunity size={20} />
            </ThemeIcon>
            <div>
              <Text size="sm" c="dimmed">
                Pågående och planerade projekt
              </Text>
              <Text size="xl" fw={700}>
                {openProjects.length} st
              </Text>
            </div>
          </Group>
          <Stack gap={6}>
            {openProjects.map((p) => (
              <Group key={p.id} gap="xs" wrap="nowrap">
                <Badge size="sm" variant="light" color={PROJECT_STATUS_COLORS[p.status]}>
                  {PROJECT_STATUS_LABELS[p.status]}
                </Badge>
                {p.isUrgent && (
                  <Badge size="sm" variant="light" color="red">
                    Brådskande
                  </Badge>
                )}
                <Anchor component={Link} to={`/properties/${property.id}/projects/${p.id}`} size="sm">
                  {p.name}
                </Anchor>
                <Text size="xs" c="dimmed">
                  {p.plannedStartDate ?? ''}
                </Text>
              </Group>
            ))}
          </Stack>
        </Card>
      )}

      {property.latitude !== null && property.longitude !== null && (
        <PropertyMap latitude={property.latitude} longitude={property.longitude} label={property.nickname} />
      )}

      {/* The one part of the app that says what to do next rather than what was done. Only shown
          when something actually needs attention — an empty "all ok" panel is just noise. */}
      {needsAttention.length > 0 && (
        <Alert
          variant="light"
          color={needsAttention.some((i) => i.urgency === 'Overdue') ? 'red' : 'orange'}
          icon={<IconCalendarClock size={18} />}
          title="Underhåll att se över"
        >
          <Stack gap={4} mt="xs">
            {needsAttention.slice(0, 5).map((item) => (
              <Group key={item.componentId} gap="xs">
                <Badge size="sm" variant="light" color={MAINTENANCE_URGENCY_COLORS[item.urgency]}>
                  {MAINTENANCE_URGENCY_LABELS[item.urgency]}
                </Badge>
                <Text size="sm">
                  {item.componentName} — {item.nextDueDate}
                </Text>
                {item.hasUpcomingProject && (
                  <Text size="xs" c="dimmed">
                    (projekt planerat)
                  </Text>
                )}
              </Group>
            ))}
            <Anchor component={Link} to={`/properties/${property.id}/maintenance`} size="sm" mt={4}>
              Visa hela underhållsplanen
            </Anchor>
          </Stack>
        </Alert>
      )}

      <Title order={4} mt="lg">
        Tidslinje
      </Title>
      <Card withBorder padding="lg">
        <PropertyTimeline
          propertyId={property.id}
          purchaseDate={property.purchaseDate}
          valuations={valuations ?? []}
          projects={projects ?? []}
          documents={documents ?? []}
          onQuickAdd={setQuickAddRequest}
        />
      </Card>

      <QuickAddModal propertyId={property.id} request={quickAddRequest} onClose={() => setQuickAddRequest(null)} />
    </Stack>
  )
}
