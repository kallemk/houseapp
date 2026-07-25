import { Button, Group, Text } from '@mantine/core'
import { NavLink } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'

const links = [
  { to: '/', label: 'Dashboard' },
  { to: '/valuations', label: 'Valuations' },
  { to: '/renovations', label: 'Renovations' },
  { to: '/documents', label: 'Documents' },
]

export function NavBar() {
  const { user, logout } = useAuth()

  return (
    <Group h="100%" px="md" justify="space-between">
      <Group gap="lg">
        <Text fw={700}>HouseApp</Text>
        {links.map((link) => (
          <Text
            key={link.to}
            component={NavLink}
            to={link.to}
            end={link.to === '/'}
            style={({ isActive }: { isActive: boolean }) => ({
              fontWeight: isActive ? 700 : 400,
              textDecoration: 'none',
              color: 'inherit',
            })}
          >
            {link.label}
          </Text>
        ))}
      </Group>
      <Group gap="sm">
        <Text size="sm" c="dimmed">
          {user?.displayName}
        </Text>
        <Button variant="subtle" size="xs" onClick={() => logout()}>
          Log out
        </Button>
      </Group>
    </Group>
  )
}
