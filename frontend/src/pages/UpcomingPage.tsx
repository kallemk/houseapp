import { Center, Loader } from '@mantine/core'
import { IconClockDollar } from '@tabler/icons-react'
import { useParams } from 'react-router-dom'
import { EmptyState } from '../components/common/EmptyState'
import { UpcomingExpenses } from '../components/finances/UpcomingExpenses'
import { useProjects } from '../hooks/useProjects'

/**
 * What the work you've already entered is going to cost. `UpcomingExpenses` renders nothing when no
 * project is open, so the empty state lives here rather than inside it — on the budget page it sat
 * below a table that still had something to say, but this tab would otherwise be blank.
 */
export function UpcomingPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const { data: projects, isLoading } = useProjects(propertyId ?? '')

  if (isLoading) {
    return (
      <Center py="xl">
        <Loader />
      </Center>
    )
  }

  const hasOpenWork = (projects ?? []).some((p) => p.status !== 'Completed' && p.status !== 'Cancelled')
  if (!hasOpenWork) {
    return <EmptyState icon={IconClockDollar} message="Inga planerade eller pågående projekt att räkna på." />
  }

  return <UpcomingExpenses projects={projects ?? []} propertyId={propertyId ?? ''} />
}
