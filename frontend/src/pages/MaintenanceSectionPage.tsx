import { Group, Stack, Tabs, Text, ThemeIcon, Title } from '@mantine/core'
import { IconAdjustments, IconCalendarClock, IconListCheck } from '@tabler/icons-react'
import { Outlet, useLocation, useNavigate, useParams } from 'react-router-dom'

const TABS = [
  { value: 'schedule', label: 'Plan', icon: IconCalendarClock },
  { value: 'components', label: 'Komponenter', icon: IconAdjustments },
]

/**
 * The schedule and the component list it's computed from, side by side.
 *
 * They belong together: every date on the plan comes from a component's interval, so "den här delen
 * behöver inte ses över så ofta" is the answer to half the questions the plan raises. The components
 * page used to be reachable only through a small link on the schedule, which meant it was in the
 * navigation nowhere at all.
 */
export function MaintenanceSectionPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const navigate = useNavigate()
  const location = useLocation()

  // The last path segment is the tab. `index` redirects to /schedule, so there's always one.
  const active = TABS.find((tab) => location.pathname.endsWith(`/${tab.value}`))?.value ?? null

  return (
    <Stack>
      <Group gap="sm">
        <ThemeIcon variant="light" size={36} radius="md">
          <IconListCheck size={20} />
        </ThemeIcon>
        <div>
          <Title order={2}>Underhåll</Title>
          <Text size="sm" c="dimmed">
            Vad som behöver ses över, och hur ofta varje del av bostaden brukar behöva det.
          </Text>
        </div>
      </Group>

      <Tabs value={active} onChange={(value) => navigate(`/properties/${propertyId}/maintenance/${value}`)}>
        <Tabs.List>
          {TABS.map((tab) => (
            <Tabs.Tab key={tab.value} value={tab.value} leftSection={<tab.icon size={16} />}>
              {tab.label}
            </Tabs.Tab>
          ))}
        </Tabs.List>
      </Tabs>

      <Outlet />
    </Stack>
  )
}
