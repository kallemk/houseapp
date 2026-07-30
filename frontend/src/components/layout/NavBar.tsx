import { Burger, Divider, Drawer, Group, Menu, Stack, Text, ThemeIcon, UnstyledButton } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import {
  IconCalendarClock,
  IconChartLine,
  IconChevronDown,
  IconFiles,
  IconHammer,
  IconHome2,
  IconHomeStar,
  IconLogout,
  IconPlus,
  IconUsers,
  IconWallet,
} from '@tabler/icons-react'
import { NavLink, useLocation, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { useProperties } from '../../hooks/useProperties'
import { setLastPropertyId } from '../../utils/lastProperty'

const desktopLinkStyle = ({ isActive }: { isActive: boolean }) => ({
  display: 'flex',
  alignItems: 'center',
  gap: 6,
  padding: '6px 12px',
  borderRadius: 'var(--mantine-radius-md)',
  fontSize: 'var(--mantine-font-size-sm)',
  fontWeight: isActive ? 600 : 500,
  color: isActive ? 'var(--mantine-color-terracotta-7)' : 'var(--mantine-color-gray-7)',
  backgroundColor: isActive ? 'var(--mantine-color-terracotta-0)' : 'transparent',
})

const drawerLinkStyle = ({ isActive }: { isActive: boolean }) => ({
  display: 'flex',
  alignItems: 'center',
  gap: 10,
  padding: '10px 12px',
  borderRadius: 'var(--mantine-radius-md)',
  fontWeight: isActive ? 600 : 500,
  color: isActive ? 'var(--mantine-color-terracotta-7)' : 'var(--mantine-color-gray-7)',
  backgroundColor: isActive ? 'var(--mantine-color-terracotta-0)' : 'transparent',
})

export function NavBar() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const { propertyId } = useParams<{ propertyId: string }>()
  const { data: properties } = useProperties()
  const currentProperty = properties?.find((p) => p.id === propertyId)
  const [drawerOpened, { toggle: toggleDrawer, close: closeDrawer }] = useDisclosure(false)

  const links = [
    { to: `/properties/${propertyId}`, label: 'Översikt', icon: IconHome2, end: true },
    { to: `/properties/${propertyId}/valuations`, label: 'Värderingar', icon: IconChartLine, end: false },
    { to: `/properties/${propertyId}/projects`, label: 'Projekt', icon: IconHammer, end: false },
    { to: `/properties/${propertyId}/maintenance`, label: 'Underhållsplan', icon: IconCalendarClock, end: false },
    { to: `/properties/${propertyId}/budget`, label: 'Budget', icon: IconWallet, end: false },
    { to: `/properties/${propertyId}/documents`, label: 'Dokument', icon: IconFiles, end: false },
  ]

  // Preserves which sub-page you're on (dashboard/valuations/renovations/documents) when
  // switching to a different property, rather than always resetting to the dashboard.
  const suffix = location.pathname.match(/^\/properties\/[^/]+(\/.*)?$/)?.[1] ?? ''

  function switchTo(id: string) {
    setLastPropertyId(id)
    navigate(`/properties/${id}${suffix}`)
    closeDrawer()
  }

  return (
    <>
      <Group h="100%" px="md" justify="space-between" wrap="nowrap">
        <Group gap="xl" wrap="nowrap" style={{ minWidth: 0 }}>
          {/* The logo is the way back to the current property's overview — the conventional
              "click the wordmark to go home", where home is the property you're in. */}
          <UnstyledButton
            component={NavLink}
            to={`/properties/${propertyId}`}
            end
            style={{ display: 'flex', alignItems: 'center', gap: 8 }}
          >
            <ThemeIcon variant="light" radius="md" size="md">
              <IconHomeStar size={18} />
            </ThemeIcon>
            <Text fw={700}>HusTracker</Text>
          </UnstyledButton>

          {/* Desktop: property switcher + horizontal links. Collapses into the burger/drawer
              below "sm" instead of wrapping onto a second line, which used to spill out of the
              header's fixed height and overlap the page content underneath. */}
          <Group gap="xl" wrap="nowrap" visibleFrom="sm">
            <Menu position="bottom-start" withArrow shadow="md">
              <Menu.Target>
                <UnstyledButton
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 4,
                    padding: '4px 8px',
                    borderRadius: 'var(--mantine-radius-md)',
                  }}
                >
                  <Text size="sm" fw={600}>
                    {currentProperty?.nickname ?? '…'}
                  </Text>
                  <IconChevronDown size={14} />
                </UnstyledButton>
              </Menu.Target>
              <Menu.Dropdown>
                <Menu.Label>Bostäder</Menu.Label>
                {properties?.map((property) => (
                  <Menu.Item
                    key={property.id}
                    fw={property.id === propertyId ? 700 : 400}
                    onClick={() => switchTo(property.id)}
                  >
                    {property.nickname}
                  </Menu.Item>
                ))}
                <Menu.Divider />
                <Menu.Item leftSection={<IconPlus size={14} />} onClick={() => navigate('/properties')}>
                  Hantera / lägg till bostad
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>

            <Group gap={4} wrap="nowrap">
              {links.map((link) => (
                <UnstyledButton key={link.to} component={NavLink} to={link.to} end={link.end} style={desktopLinkStyle}>
                  <link.icon size={16} />
                  {link.label}
                </UnstyledButton>
              ))}
            </Group>
          </Group>
        </Group>

        <Group gap="sm" visibleFrom="sm" wrap="nowrap">
          {/* Admin-only, matching the API: every /api/users endpoint returns 403 for regular users,
              so showing the link would only lead to a broken page. */}
          {user?.isAdmin && (
            <UnstyledButton
              component={NavLink}
              to={`/properties/${propertyId}/users`}
              title="Användare"
              style={{ display: 'flex', alignItems: 'center', color: 'var(--mantine-color-gray-6)' }}
            >
              <IconUsers size={16} />
            </UnstyledButton>
          )}
          <Text size="sm" c="dimmed">
            {user?.displayName}
          </Text>
          <UnstyledButton
            onClick={() => logout()}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              padding: '6px 10px',
              borderRadius: 'var(--mantine-radius-md)',
              fontSize: 'var(--mantine-font-size-sm)',
              color: 'var(--mantine-color-gray-6)',
            }}
          >
            <IconLogout size={16} />
            Logga ut
          </UnstyledButton>
        </Group>

        <Burger opened={drawerOpened} onClick={toggleDrawer} hiddenFrom="sm" size="sm" />
      </Group>

      <Drawer opened={drawerOpened} onClose={closeDrawer} position="right" size="xs" title="Meny">
        <Stack gap="xs">
          <Text size="sm" c="dimmed">
            {user?.displayName}
          </Text>

          <Divider label="Bostäder" labelPosition="left" mt="sm" />
          {properties?.map((property) => (
            <UnstyledButton
              key={property.id}
              onClick={() => switchTo(property.id)}
              fw={property.id === propertyId ? 700 : 500}
              style={{ padding: '8px 12px', borderRadius: 'var(--mantine-radius-md)' }}
            >
              {property.nickname}
            </UnstyledButton>
          ))}
          <UnstyledButton
            onClick={() => {
              navigate('/properties')
              closeDrawer()
            }}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 8,
              padding: '8px 12px',
              borderRadius: 'var(--mantine-radius-md)',
              color: 'var(--mantine-color-gray-6)',
            }}
          >
            <IconPlus size={16} />
            Hantera / lägg till bostad
          </UnstyledButton>

          <Divider label="Sidor" labelPosition="left" mt="sm" />
          {links.map((link) => (
            <UnstyledButton
              key={link.to}
              component={NavLink}
              to={link.to}
              end={link.end}
              onClick={closeDrawer}
              style={drawerLinkStyle}
            >
              <link.icon size={18} />
              {link.label}
            </UnstyledButton>
          ))}
          {user?.isAdmin && (
            <UnstyledButton
              component={NavLink}
              to={`/properties/${propertyId}/users`}
              onClick={closeDrawer}
              style={drawerLinkStyle}
            >
              <IconUsers size={18} />
              Användare
            </UnstyledButton>
          )}

          <Divider mt="sm" />
          <UnstyledButton
            onClick={() => logout()}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 10,
              padding: '10px 12px',
              borderRadius: 'var(--mantine-radius-md)',
              color: 'var(--mantine-color-gray-6)',
            }}
          >
            <IconLogout size={18} />
            Logga ut
          </UnstyledButton>
        </Stack>
      </Drawer>
    </>
  )
}
