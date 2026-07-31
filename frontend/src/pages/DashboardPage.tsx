import {
  ActionIcon,
  Alert,
  Anchor,
  Badge,
  Card,
  Center,
  Group,
  Loader,
  Modal,
  SimpleGrid,
  Stack,
  Text,
  ThemeIcon,
  Title,
} from '@mantine/core'
import { notifications } from '@mantine/notifications'
import {
  IconBuildingCommunity,
  IconCalendarClock,
  IconHome2,
  IconPencil,
  IconPigMoney,
  IconTag,
  IconTrendingDown,
  IconTrendingUp,
} from '@tabler/icons-react'
import { useState, type ReactNode } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { PropertyForm } from '../components/properties/PropertyForm'
import { propertyFormToInput, propertyToFormValues } from '../utils/propertyForm'
import { useUpdateProperty } from '../hooks/useProperties'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { useValuations } from '../hooks/useValuations'
import { useProjects } from '../hooks/useProjects'
import { useMaintenanceSchedule } from '../hooks/useMaintenanceSchedule'
import { useBudgets } from '../hooks/useBudgets'
import { usePropertyComponentList } from '../hooks/usePropertyComponents'
import { formatAddress } from '../utils/address'
import {
  MAINTENANCE_URGENCY_COLORS,
  MAINTENANCE_URGENCY_LABELS,
  PROJECT_STATUS_COLORS,
  PROJECT_STATUS_LABELS,
} from '../utils/labels'
import { PropertyTimeline } from '../components/dashboard/PropertyTimeline'
import { QuickAddModal, type QuickAddRequest } from '../components/dashboard/QuickAddModal'
import { BudgetProgressCard } from '../components/dashboard/BudgetProgressCard'
import { SpendBreakdown } from '../components/dashboard/SpendBreakdown'
import { SpendByComponent } from '../components/dashboard/SpendByComponent'
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
      <Text size="sm" fw={600} c={color} style={{ whiteSpace: 'nowrap' }}>
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
  const { data: schedule } = useMaintenanceSchedule(propertyId ?? '')
  const { data: budgets } = useBudgets(propertyId ?? '')
  const { data: components } = usePropertyComponentList(propertyId ?? '')
  const [quickAddRequest, setQuickAddRequest] = useState<QuickAddRequest | null>(null)
  const [editing, setEditing] = useState(false)
  const updateProperty = useUpdateProperty()

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

  // Capital put into the house: the purchase plus work that adds to it. Maintenance is deliberately
  // excluded — it's upkeep that's consumed, not money that's still sitting in the building. Same
  // Renovation/Investment split the rest of the app makes.
  const capitalWork = (projects ?? [])
    .filter((p) => p.workType === 'Renovation' || p.workType === 'Investment')
    .reduce((sum, p) => sum + p.actualCost, 0)
  const investedTotal = property.purchasePrice + capitalWork
  const netPosition = currentValue - investedTotal

  const openProjects = (projects ?? []).filter((p) => p.status !== 'Completed' && p.status !== 'Cancelled')
  const needsAttention = (schedule ?? []).filter((i) => i.urgency === 'Overdue' || i.urgency === 'DueSoon')
  // Money committed but not yet paid out. Estimates rather than cost rows on purpose: these are
  // projects that mostly haven't been invoiced yet, so the estimate is all there is.
  const pipeline = openProjects.reduce((sum, p) => sum + (p.actualCost > 0 ? p.actualCost : p.estimatedCost), 0)
  const thisYearsBudget = (budgets ?? []).find((b) => b.year === new Date().getFullYear() && b.id)
  // Both cards below hide themselves when they'd say nothing; this keeps the grid from rendering
  // empty, and drops it to one column when only one of them has anything to show.
  const hasSpend = (projects ?? []).some((p) => p.actualCost > 0)
  const lowerCards = [thisYearsBudget !== undefined, hasSpend].filter(Boolean).length

  return (
    <Stack>
      <Group gap="sm" align="center">
        <Title order={2}>{property.nickname}</Title>
        <ActionIcon variant="subtle" color="gray" title="Redigera bostaden" onClick={() => setEditing(true)}>
          <IconPencil size={18} />
        </ActionIcon>
        {property.yearBuilt && (
          <Badge variant="light" color="gray">
            Byggt {property.yearBuilt}
          </Badge>
        )}
        {property.isDemo && (
          <Badge variant="light" color="grape">
            Demo
          </Badge>
        )}
      </Group>
      {/* So nobody mistakes the shared sandbox for their own house — anything entered here is
          visible to everyone with an account. */}
      {property.isDemo && (
        <Text size="sm" c="dimmed">
          Det här är en demobostad som alla användare kan se och ändra i. Prova gärna — men lägg inte
          in något du vill hålla för dig själv.
        </Text>
      )}
      <Text c="dimmed">{formatAddress(property)}</Text>
      {property.propertyDesignation && (
        <Text c="dimmed" size="sm">
          Fastighetsbeteckning: {property.propertyDesignation}
        </Text>
      )}

      {/* Three across, not two — an odd card wrapping onto its own row reads as a separate section
          rather than the third of a set. Stacked below sm, where three wouldn't fit. */}
      <SimpleGrid cols={{ base: 1, sm: 3 }} mt="md">
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
        {/* The value change above compares against the purchase price alone, which flatters a house
            with a lot of renovation in it. This one counts that money too. */}
        <StatCard
          icon={IconPigMoney}
          label="Mot insatt kapital"
          value={hasValuation ? formatCurrency(netPosition) : '—'}
          footer={
            hasValuation ? (
              <Text size="xs" c={netPosition >= 0 ? 'teal' : 'red'} mt={2}>
                Värde mot {formatCurrency(investedTotal)} (köpeskilling + renovering & investering)
              </Text>
            ) : (
              <Text size="xs" c="dimmed" mt={2}>
                Kräver en värdering
              </Text>
            )
          }
        />
      </SimpleGrid>

      {/* Replaces the two single-figure spend cards that used to sit above: the same totals are the
          rightmost column here, with the years the cards couldn't show. */}
      <SpendBreakdown
        projects={projects ?? []}
        purchaseDate={property.purchaseDate}
        currentValue={currentValue}
      />

      {lowerCards > 0 && (
        <SimpleGrid cols={{ base: 1, lg: lowerCards > 1 ? 2 : 1 }}>
          {thisYearsBudget && <BudgetProgressCard budget={thisYearsBudget} propertyId={property.id} />}
          <SpendByComponent projects={projects ?? []} components={components ?? []} />
        </SimpleGrid>
      )}

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
              {pipeline > 0 && (
                <Text size="xs" c="dimmed">
                  {formatCurrency(pipeline)} beräknad kostnad kvar att betala
                </Text>
              )}
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
          onQuickAdd={setQuickAddRequest}
        />
      </Card>

      <QuickAddModal propertyId={property.id} request={quickAddRequest} onClose={() => setQuickAddRequest(null)} />

      <Modal opened={editing} onClose={() => setEditing(false)} title="Redigera bostad" centered>
        <PropertyForm
          initial={propertyToFormValues(property)}
          submitLabel="Spara"
          submitting={updateProperty.isPending}
          onSubmit={(values) =>
            updateProperty.mutate(
              { id: property.id, input: propertyFormToInput(values) },
              {
                onSuccess: () => setEditing(false),
                onError: () => notifications.show({ color: 'red', message: 'Kunde inte spara bostaden.' }),
              },
            )
          }
        />
      </Modal>
    </Stack>
  )
}
