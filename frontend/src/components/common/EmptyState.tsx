import { Stack, Text } from '@mantine/core'
import type { ReactNode } from 'react'

export function EmptyState({ message, action }: { message: string; action?: ReactNode }) {
  return (
    <Stack align="center" py="xl" gap="sm">
      <Text c="dimmed">{message}</Text>
      {action}
    </Stack>
  )
}
