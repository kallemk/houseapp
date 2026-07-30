import { Group, Stack, Tabs, ThemeIcon, Title } from '@mantine/core'
import { IconAdjustments, IconSettings, IconUsers } from '@tabler/icons-react'
import { Outlet, useLocation, useNavigate, useParams } from 'react-router-dom'

const TABS = [
  { value: 'components', label: 'Komponenter', icon: IconAdjustments },
  { value: 'users', label: 'Användare', icon: IconUsers },
]

/**
 * Layout route for everything under /admin — the shared heading plus the tab bar, with each
 * management page rendered through the Outlet.
 *
 * The section is open to everyone, not gated on `isAdmin`: the components list is genuinely useful
 * read-only (it's the vocabulary the projects page is built on), and hiding the whole section from
 * regular users would make the tab bar mean different things to different people. Each page decides
 * for itself what a non-admin gets — read-only for components, a plain "no access" for users.
 */
export function AdministrationPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const navigate = useNavigate()
  const location = useLocation()

  // The last path segment is the tab. `index` redirects to /components, so there's always one.
  const active = TABS.find((tab) => location.pathname.endsWith(`/${tab.value}`))?.value ?? null

  return (
    <Stack>
      <Group gap="sm">
        <ThemeIcon variant="light" size={36} radius="md">
          <IconSettings size={20} />
        </ThemeIcon>
        <Title order={2}>Administration</Title>
      </Group>

      <Tabs value={active} onChange={(value) => navigate(`/properties/${propertyId}/admin/${value}`)}>
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
