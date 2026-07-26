import { Group, Text, ThemeIcon, UnstyledButton } from '@mantine/core'
import {
  IconChartLine,
  IconFiles,
  IconHammer,
  IconHome2,
  IconHomeStar,
  IconLogout,
} from '@tabler/icons-react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'

const links = [
  { to: '/', label: 'Översikt', icon: IconHome2, end: true },
  { to: '/valuations', label: 'Värderingar', icon: IconChartLine, end: false },
  { to: '/renovations', label: 'Renoveringar', icon: IconHammer, end: false },
  { to: '/documents', label: 'Dokument', icon: IconFiles, end: false },
]

export function NavBar() {
  const { user, logout } = useAuth()

  return (
    <Group h="100%" px="md" justify="space-between">
      <Group gap="xl">
        <Group gap="xs">
          <ThemeIcon variant="light" radius="md" size="md">
            <IconHomeStar size={18} />
          </ThemeIcon>
          <Text fw={700}>HusTracker</Text>
        </Group>
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
