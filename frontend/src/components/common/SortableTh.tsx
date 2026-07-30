import { Group, Table, Text, UnstyledButton } from '@mantine/core'
import { IconChevronDown, IconChevronUp, IconSelector } from '@tabler/icons-react'
import type { ReactNode } from 'react'

/**
 * A table header you can click to sort. Pair with useTableSort:
 * `<SortableTh {...sortProps('name')}>Namn</SortableTh>`
 *
 * The neutral up/down icon is always shown rather than appearing on hover, so it's discoverable —
 * on a touch screen there is no hover to reveal it.
 */
export function SortableTh({
  children,
  sorted,
  descending,
  onSort,
  width,
}: {
  children: ReactNode
  sorted: boolean
  descending: boolean
  onSort: () => void
  width?: number
}) {
  const Icon = sorted ? (descending ? IconChevronDown : IconChevronUp) : IconSelector

  return (
    <Table.Th w={width}>
      <UnstyledButton onClick={onSort} style={{ width: '100%' }}>
        <Group gap={4} wrap="nowrap">
          <Text size="sm" fw={600}>
            {children}
          </Text>
          <Icon size={14} style={{ opacity: sorted ? 1 : 0.35, flexShrink: 0 }} />
        </Group>
      </UnstyledButton>
    </Table.Th>
  )
}
