import { Center, Loader, Stack } from '@mantine/core'
import { Navigate, useParams } from 'react-router-dom'
import { SpendBreakdown } from '../components/finances/SpendBreakdown'
import { SpendByComponent } from '../components/finances/SpendByComponent'
import { useProjects } from '../hooks/useProjects'
import { usePropertyComponentList } from '../hooks/usePropertyComponents'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { useValuations } from '../hooks/useValuations'

/**
 * What the house has cost, from two angles: by *kind* of work over time, and by *part* of the house
 * over all time. Both lived on the dashboard, where they were the bulk of its weight and answered a
 * question nobody lands on the overview to ask.
 */
export function SpendingPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const { property, isLoading: loadingProperty, notFound } = useSelectedProperty(propertyId)
  const { data: projects, isLoading } = useProjects(propertyId ?? '')
  const { data: valuations } = useValuations(propertyId ?? '')
  const { data: components } = usePropertyComponentList(propertyId ?? '')

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

  // Same fallback as the overview: with no valuation on record, the purchase price is the best
  // figure available for the maintenance-share yardstick.
  const currentValue = valuations?.[0]?.value ?? property.purchasePrice

  return (
    <Stack>
      <SpendBreakdown projects={projects ?? []} purchaseDate={property.purchaseDate} currentValue={currentValue} />
      <SpendByComponent projects={projects ?? []} components={components ?? []} />
    </Stack>
  )
}
