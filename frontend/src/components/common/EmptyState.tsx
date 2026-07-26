import { Stack, Text, ThemeIcon } from '@mantine/core'
import { IconMoodEmpty, type TablerIcon } from '@tabler/icons-react'
import type { ReactNode } from 'react'

interface EmptyStateProps {
  message: string
  action?: ReactNode
  icon?: TablerIcon
}

export function EmptyState({ message, action, icon: Icon = IconMoodEmpty }: EmptyStateProps) {
  return (
    <Stack align="center" py="xl" gap="sm">
      <ThemeIcon variant="light" size={48} radius="xl" color="gray">
        <Icon size={26} />
      </ThemeIcon>
      <Text c="dimmed">{message}</Text>
      {action}
    </Stack>
  )
}
