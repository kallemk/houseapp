import { Group, Menu, Text, ThemeIcon, UnstyledButton } from '@mantine/core'
import {
  IconChartLine,
  IconChevronDown,
  IconFiles,
  IconHammer,
  IconHome2,
  IconHomeStar,
  IconLogout,
  IconPlus,
} from '@tabler/icons-react'
import { NavLink, useLocation, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { useProperties } from '../../hooks/useProperties'
import { setLastPropertyId } from '../../utils/lastProperty'

export function NavBar() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const { propertyId } = useParams<{ propertyId: string }>()
  const { data: properties } = useProperties()
  const currentProperty = properties?.find((p) => p.id === propertyId)

  const links = [
    { to: `/properties/${propertyId}`, label: 'Översikt', icon: IconHome2, end: true },
    { to: `/properties/${propertyId}/valuations`, label: 'Värderingar', icon: IconChartLine, end: false },
    { to: `/properties/${propertyId}/renovations`, label: 'Renoveringar', icon: IconHammer, end: false },
    { to: `/properties/${propertyId}/documents`, label: 'Dokument', icon: IconFiles, end: false },
  ]

  // Preserves which sub-page you're on (dashboard/valuations/renovations/documents) when
  // switching to a different property, rather than always resetting to the dashboard.
  const suffix = location.pathname.match(/^\/properties\/[^/]+(\/.*)?$/)?.[1] ?? ''

  function switchTo(id: string) {
    setLastPropertyId(id)
    navigate(`/properties/${id}${suffix}`)
  }

  return (
    <Group h="100%" px="md" justify="space-between">
      <Group gap="xl">
        <Group gap="xs">
          <ThemeIcon variant="light" radius="md" size="md">
            <IconHomeStar size={18} />
          </ThemeIcon>
          <Text fw={700}>HusTracker</Text>
        </Group>

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

        <Group gap={4}>
          {links.map((link) => (
            <UnstyledButton
              key={link.to}
              component={NavLink}
              to={link.to}
              end={link.end}
              style={({ isActive }: { isActive: boolean }) => ({
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '6px 12px',
                borderRadius: 'var(--mantine-radius-md)',
                fontSize: 'var(--mantine-font-size-sm)',
                fontWeight: isActive ? 600 : 500,
                color: isActive ? 'var(--mantine-color-terracotta-7)' : 'var(--mantine-color-gray-7)',
                backgroundColor: isActive ? 'var(--mantine-color-terracotta-0)' : 'transparent',
              })}
            >
              <link.icon size={16} />
              {link.label}
            </UnstyledButton>
          ))}
        </Group>
      </Group>
      <Group gap="sm">
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
    </Group>
  )
}
