import { Anchor, Container, Group, Stack, Tabs, ThemeIcon, Title } from '@mantine/core'
import { IconAdjustments, IconArrowLeft, IconSettings, IconUsers } from '@tabler/icons-react'
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { AppFooter } from '../components/layout/AppFooter'

const TABS = [
  // "Centrala" distinguishes it from each property's own list, which lives under a property's
  // Underhåll section.
  { value: 'components', label: 'Centrala komponenter', icon: IconAdjustments },
  { value: 'users', label: 'Användare', icon: IconUsers },
]

/**
 * Layout route for everything under /admin — the shared heading plus the tab bar, with each
 * management page rendered through the Outlet.
 *
 * **This sits outside the property routes**, and used to sit inside them at
 * `/properties/:id/admin`. Nothing in here is property-scoped: it manages the central component
 * registry every property inherits from, and every user in the app. The prefix was a lie, and it
 * cost a slot in a navigation bar where every other entry was about one house.
 *
 * Like `/properties` and `/feedback`, it therefore renders its own shell rather than `AppLayout` —
 * the navbar needs a property to build its links from, and there isn't one here.
 *
 * The section is open to everyone, not gated on `isAdmin`: the components list is genuinely useful
 * read-only (it's the vocabulary the projects page is built on), and hiding the whole section from
 * regular users would make the tab bar mean different things to different people. Each page decides
 * for itself what a non-admin gets — read-only for components, a plain "no access" for users.
 */
export function AdministrationPage() {
  const navigate = useNavigate()
  const location = useLocation()

  // The last path segment is the tab. `index` redirects to /components, so there's always one.
  const active = TABS.find((tab) => location.pathname.endsWith(`/${tab.value}`))?.value ?? null

  return (
    <>
      <Container size="lg" py="xl">
        <Stack>
          {/* "/" resolves to the property you were last on, or the picker if there isn't one. */}
          <Anchor component={Link} to="/" size="sm">
            <Group gap={4}>
              <IconArrowLeft size={14} />
              Tillbaka
            </Group>
          </Anchor>

          <Group gap="sm">
            <ThemeIcon variant="light" size={36} radius="md">
              <IconSettings size={20} />
            </ThemeIcon>
            <Title order={2}>Administration</Title>
          </Group>

          <Tabs value={active} onChange={(value) => navigate(`/admin/${value}`)}>
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
      </Container>
      <AppFooter />
    </>
  )
}
