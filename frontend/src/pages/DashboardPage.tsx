import {
  ActionIcon,
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
  IconCoins,
  IconHome2,
  IconListCheck,
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
import { formatAddress } from '../utils/address'
import {
  MAINTENANCE_URGENCY_COLORS,
  MAINTENANCE_URGENCY_LABELS,
  PROJECT_STATUS_COLORS,
  PROJECT_STATUS_LABELS,
} from '../utils/labels'
import { PropertyTimeline } from '../components/dashboard/PropertyTimeline'
import { QuickAddModal, type QuickAddRequest } from '../components/dashboard/QuickAddModal'
import { formatCurrency, formatNumber } from '../utils/currency'
import { anyDate, inYear, spent } from '../utils/spend'

/** Enough to see what's pressing without turning into the projects table. */
const MAX_TODOS = 6

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

/**
 * One row of "Att göra" — overdue upkeep and open projects reduced to the same shape.
 *
 * `rank` is the sort order, and the interleaving is the point: work that's already late comes first,
 * then anything you've flagged as brådskande, then upkeep that's merely approaching, then the rest.
 * These were two separate blocks before — a maintenance Alert and a projects Card — which split one
 * conversation ("taket är försenat" / "projekt: byte tak, planerat") across two places on the page.
 */
interface TodoItem {
  key: string
  rank: number
  date: string | null
  badges: { label: string; color: string }[]
  label: string
  note?: string
  to: string
}

export function DashboardPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const { property, isLoading, notFound } = useSelectedProperty(propertyId)
  const { data: valuations } = useValuations(propertyId ?? '')
  const { data: projects } = useProjects(propertyId ?? '')
  const { data: schedule } = useMaintenanceSchedule(propertyId ?? '')
  const { data: budgets } = useBudgets(propertyId ?? '')
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

  // Capital put into the house: the purchase plus work that adds to it.
  //
  // An allowlist rather than an exclusion list, and deliberately so — a new WorkType must be argued
  // into this figure rather than landing in it by default. Two are out today: Maintenance is upkeep
  // that's consumed rather than money still sitting in the building, and Purchase buys movable
  // things that can leave with you, so neither raises what the property is worth.
  const capitalWork = (projects ?? [])
    .filter((p) => p.workType === 'Renovation' || p.workType === 'Investment')
    .reduce((sum, p) => sum + p.actualCost, 0)
  const investedTotal = property.purchasePrice + capitalWork
  const netPosition = currentValue - investedTotal

  const openProjects = (projects ?? []).filter((p) => p.status !== 'Completed' && p.status !== 'Cancelled')

  const todos: TodoItem[] = [
    ...(schedule ?? [])
      .filter((i) => i.urgency === 'Overdue' || i.urgency === 'DueSoon')
      .map((item) => ({
        key: `maintenance-${item.componentId}`,
        rank: item.urgency === 'Overdue' ? 0 : 2,
        date: item.nextDueDate,
        badges: [
          { label: MAINTENANCE_URGENCY_LABELS[item.urgency], color: MAINTENANCE_URGENCY_COLORS[item.urgency] },
        ],
        label: item.componentName,
        note: item.hasUpcomingProject ? 'projekt planerat' : undefined,
        to: `/properties/${property.id}/maintenance/schedule`,
      })),
    ...openProjects.map((p) => ({
      key: `project-${p.id}`,
      rank: p.isUrgent ? 1 : 3,
      date: p.plannedStartDate,
      badges: [
        { label: PROJECT_STATUS_LABELS[p.status], color: PROJECT_STATUS_COLORS[p.status] },
        ...(p.isUrgent ? [{ label: 'Brådskande', color: 'red' }] : []),
      ],
      label: p.name,
      to: `/properties/${property.id}/projects/${p.id}`,
    })),
    // Undated work sorts last within its rank rather than first, which is what an empty string would
    // have done.
  ].sort((a, b) => a.rank - b.rank || (a.date ?? '9999').localeCompare(b.date ?? '9999'))

  const thisYear = new Date().getFullYear()
  const spentThisYear = spent(projects ?? [], inYear(thisYear))
  const spentLastYear = spent(projects ?? [], inYear(thisYear - 1))
  const spentTotal = spent(projects ?? [], anyDate)
  const thisYearsBudget = (budgets ?? []).find((b) => b.year === thisYear && b.id)
  // Estimates, matching the Kommande tab exactly — these are mostly projects nobody has invoiced
  // yet, so the estimate is all there is.
  const upcoming = openProjects.reduce((sum, p) => sum + p.estimatedCost, 0)
  const hasFinances = spentTotal > 0 || thisYearsBudget !== undefined || upcoming > 0

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

      {/* The one part of the app that says what to do next rather than what was done. Hidden
          entirely when there's nothing — an empty "allt är i ordning" panel is just noise. */}
      {todos.length > 0 && (
        <Card withBorder padding="lg">
          <Group gap="sm" mb="sm">
            <ThemeIcon variant="light" size={40} radius="md">
              <IconListCheck size={20} />
            </ThemeIcon>
            <div>
              <Text size="sm" c="dimmed">
                Att göra
              </Text>
              <Text size="xl" fw={700}>
                {todos.length} st
              </Text>
              <Text size="xs" c="dimmed">
                Underhåll som behöver ses över, och projekt som inte är klara.
              </Text>
            </div>
          </Group>

          <Stack gap={6}>
            {todos.slice(0, MAX_TODOS).map((todo) => (
              <Group key={todo.key} gap="xs" wrap="nowrap">
                {todo.badges.map((badge) => (
                  <Badge key={badge.label} size="sm" variant="light" color={badge.color}>
                    {badge.label}
                  </Badge>
                ))}
                <Anchor component={Link} to={todo.to} size="sm" truncate>
                  {todo.label}
                </Anchor>
                <Text size="xs" c="dimmed" style={{ whiteSpace: 'nowrap' }}>
                  {todo.date ?? ''}
                  {todo.note && ` (${todo.note})`}
                </Text>
              </Group>
            ))}
          </Stack>

          <Group gap="md" mt="sm">
            {todos.length > MAX_TODOS && (
              <Text size="xs" c="dimmed">
                +{todos.length - MAX_TODOS} till
              </Text>
            )}
            <Anchor component={Link} to={`/properties/${property.id}/projects`} size="sm">
              Alla projekt
            </Anchor>
            <Anchor component={Link} to={`/properties/${property.id}/maintenance`} size="sm">
              Hela underhållsplanen
            </Anchor>
          </Group>
        </Card>
      )}

      {/* A summary, not an analysis. The work-type × period matrix and the per-component breakdown
          both moved to Ekonomi — this says whether it's worth going there. */}
      {hasFinances && (
        <Card withBorder padding="lg">
          <Group justify="space-between" mb="md" wrap="nowrap">
            <Group gap="sm">
              <ThemeIcon variant="light" size={40} radius="md">
                <IconCoins size={20} />
              </ThemeIcon>
              <div>
                <Text size="sm" c="dimmed">
                  Ekonomi i korthet
                </Text>
                <Text size="xs" c="dimmed">
                  Utbetalt, räknat efter datumet på varje kostnadspost.
                </Text>
              </div>
            </Group>
            <Anchor component={Link} to={`/properties/${property.id}/finances`} size="sm">
              Till ekonomin
            </Anchor>
          </Group>

          <SimpleGrid cols={{ base: 1, xs: 3 }}>
            <div>
              <Text size="xs" c="dimmed">
                I år ({thisYear})
              </Text>
              <Text size="lg" fw={700}>
                {formatCurrency(spentThisYear)}
              </Text>
            </div>
            <div>
              <Text size="xs" c="dimmed">
                Förra året ({thisYear - 1})
              </Text>
              <Text size="lg" fw={700}>
                {formatCurrency(spentLastYear)}
              </Text>
            </div>
            <div>
              <Text size="xs" c="dimmed">
                Totalt
              </Text>
              <Text size="lg" fw={700}>
                {formatCurrency(spentTotal)}
              </Text>
            </div>
          </SimpleGrid>

          {(thisYearsBudget || upcoming > 0) && (
            <Stack gap={2} mt="sm">
              {thisYearsBudget && (
                <Text size="xs" c={thisYearsBudget.totalSpent > thisYearsBudget.totalBudgeted ? 'red' : 'dimmed'}>
                  Budget {thisYearsBudget.year}:{' '}
                  {formatCurrency(thisYearsBudget.totalBudgeted - thisYearsBudget.totalSpent)} kvar av{' '}
                  {formatCurrency(thisYearsBudget.totalBudgeted)}
                </Text>
              )}
              {upcoming > 0 && (
                <Text size="xs" c="dimmed">
                  Kommande utgifter: {formatCurrency(upcoming)} för projekt som inte är klara
                </Text>
              )}
            </Stack>
          )}
        </Card>
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

      <Modal opened={editing} onClose={() => setEditing(false)} title="Redigera bostad" size="lg" centered>
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
