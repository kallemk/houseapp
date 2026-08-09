import { Burger, Divider, Drawer, Group, Menu, Stack, Text, ThemeIcon, UnstyledButton } from '@mantine/core'
import { useDisclosure } from '@mantine/hooks'
import {
  IconBulb,
  IconChevronDown,
  IconFiles,
  IconHammer,
  IconHome2,
  IconHomeStar,
  IconListCheck,
  IconLogout,
  IconPigMoney,
  IconPlus,
  IconSettings,
} from '@tabler/icons-react'
import { NavLink, useLocation, useNavigate } from 'react-router-dom'
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
  const { data: properties } = useProperties()
  const [drawerOpened, { toggle: toggleDrawer, close: closeDrawer }] = useDisclosure(false)

  // Read from the path rather than `useParams`, because the bar is rendered by a layout route that
  // sits *above* the `:propertyId` segment — it also covers the picker, /admin and /feedback, where
  // there is no property at all. A hook in an ancestor route can't see a descendant's params.
  //
  // Group 1 is the property, group 2 the sub-page: the same match drives the switcher and the
  // "stay on the page you're on" behaviour when swapping property.
  const [, propertyId, suffix = ''] = location.pathname.match(/^\/properties\/([^/]+)(\/.*)?$/) ?? []
  const currentProperty = properties?.find((p) => p.id === propertyId)

  // Five, each owning one subject completely. Värderingar folded into Ekonomi and Administration
  // moved to the account menu below — the latter was never property-scoped despite its URL, since
  // it manages the central component registry and every user in the app.
  const links = !propertyId ? [] : [
    { to: `/properties/${propertyId}`, label: 'Översikt', icon: IconHome2, end: true },
    { to: `/properties/${propertyId}/projects`, label: 'Projekt', icon: IconHammer, end: false },
    { to: `/properties/${propertyId}/maintenance`, label: 'Underhåll', icon: IconListCheck, end: false },
    { to: `/properties/${propertyId}/finances`, label: 'Ekonomi', icon: IconPigMoney, end: false },
    { to: `/properties/${propertyId}/documents`, label: 'Dokument', icon: IconFiles, end: false },
  ]

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
              "click the wordmark to go home", where home is the property you're in. With no
              property in the path it goes to the picker, which is home in that case. */}
          <UnstyledButton
            component={NavLink}
            to={propertyId ? `/properties/${propertyId}` : '/properties'}
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
                  {/* Off a property — the picker, /admin, /feedback — this is the way into one,
                      so it stays rather than leaving the bar empty. */}
                  <Text size="sm" fw={600} c={propertyId ? undefined : 'dimmed'}>
                    {propertyId ? (currentProperty?.nickname ?? '…') : 'Välj bostad'}
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

            {/* The page links are per property, so there are none to show without one. */}
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

        {/* Everything that isn't about *this property* lives behind the name: administration (the
            central registry and the user list), feedback, and signing out. */}
        <Group gap="sm" visibleFrom="sm" wrap="nowrap">
          <Menu position="bottom-end" withArrow shadow="md">
            <Menu.Target>
              <UnstyledButton
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 4,
                  padding: '6px 10px',
                  borderRadius: 'var(--mantine-radius-md)',
                  fontSize: 'var(--mantine-font-size-sm)',
                  color: 'var(--mantine-color-gray-6)',
                }}
              >
                {user?.displayName}
                <IconChevronDown size={14} />
              </UnstyledButton>
            </Menu.Target>
            <Menu.Dropdown>
              {/* Open to everyone: the components list is worth reading even if you can't change
                  it, and the users page inside says plainly that it's admin-only rather than
                  hiding. */}
              <Menu.Item leftSection={<IconSettings size={14} />} onClick={() => navigate('/admin')}>
                Administration
              </Menu.Item>
              <Menu.Item leftSection={<IconBulb size={14} />} onClick={() => navigate('/feedback')}>
                Förslag &amp; feedback
              </Menu.Item>
              <Menu.Divider />
              <Menu.Item leftSection={<IconLogout size={14} />} onClick={() => logout()}>
                Logga ut
              </Menu.Item>
            </Menu.Dropdown>
          </Menu>
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

          {links.length > 0 && <Divider label="Sidor" labelPosition="left" mt="sm" />}
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

          {/* The drawer's counterpart to the desktop account menu — same three entries, since a
              dropdown inside a drawer is a worse version of a list. */}
          <Divider label="Konto" labelPosition="left" mt="sm" />
          {[
            { label: 'Administration', icon: IconSettings, onClick: () => navigate('/admin') },
            { label: 'Förslag & feedback', icon: IconBulb, onClick: () => navigate('/feedback') },
            { label: 'Logga ut', icon: IconLogout, onClick: () => logout() },
          ].map((entry) => (
            <UnstyledButton
              key={entry.label}
              onClick={() => {
                closeDrawer()
                entry.onClick()
              }}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 10,
                padding: '10px 12px',
                borderRadius: 'var(--mantine-radius-md)',
                color: 'var(--mantine-color-gray-6)',
              }}
            >
              <entry.icon size={18} />
              {entry.label}
            </UnstyledButton>
          ))}
        </Stack>
      </Drawer>
    </>
  )
}
