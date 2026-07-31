import { Card, Group, Table, Text, ThemeIcon } from '@mantine/core'
import { IconCoins } from '@tabler/icons-react'
import type { ProjectDto, WorkType } from '../../api/types'
import { formatCurrency } from '../../utils/currency'
import { WORK_TYPE_LABELS } from '../../utils/labels'

const WORK_TYPES: WorkType[] = ['Maintenance', 'Renovation', 'Investment']

/**
 * A cost belongs to the year of its own dateIncurred, not the project's completion date — the same
 * rule the budget page uses, so the two can't disagree about what a year cost. A job running over
 * New Year therefore splits across both years, the way the money actually left the account.
 *
 * Only itemised cost rows count. A project carrying nothing but an estimate contributes 0 here,
 * which is deliberate: an estimate is a plan, not a payment.
 */
function spent(projects: ProjectDto[], workType: WorkType, year: number | null): number {
  return projects
    .filter((p) => p.workType === workType)
    .flatMap((p) => p.costs)
    .filter((c) => year === null || Number(c.dateIncurred.slice(0, 4)) === year)
    .reduce((sum, c) => sum + c.amount, 0)
}

export function SpendBreakdown({ projects }: { projects: ProjectDto[] }) {
  const thisYear = new Date().getFullYear()
  const lastYear = thisYear - 1

  const columns: { label: string; year: number | null }[] = [
    { label: `I år (${thisYear})`, year: thisYear },
    { label: `Förra året (${lastYear})`, year: lastYear },
    { label: 'Totalt', year: null },
  ]

  const rows = WORK_TYPES.map((workType) => ({
    workType,
    amounts: columns.map((column) => spent(projects, workType, column.year)),
  }))

  const totals = columns.map((_, index) => rows.reduce((sum, row) => sum + row.amounts[index], 0))

  return (
    <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
      <Group gap="sm" p="lg" pb="xs">
        <ThemeIcon variant="light" size={40} radius="md">
          <IconCoins size={20} />
        </ThemeIcon>
        <div>
          <Text size="sm" c="dimmed">
            Utgifter
          </Text>
          <Text size="xs" c="dimmed">
            Räknas från projektens kostnadsposter, efter datumet på varje post.
          </Text>
        </div>
      </Group>
      <Table.ScrollContainer minWidth={520}>
        <Table verticalSpacing="sm">
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Typ av arbete</Table.Th>
              {columns.map((column) => (
                <Table.Th key={column.label} ta="right">
                  {column.label}
                </Table.Th>
              ))}
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {rows.map((row) => (
              <Table.Tr key={row.workType}>
                <Table.Td>{WORK_TYPE_LABELS[row.workType]}</Table.Td>
                {row.amounts.map((amount, index) => (
                  <Table.Td key={columns[index].label} ta="right" c={amount === 0 ? 'dimmed' : undefined}>
                    {formatCurrency(amount)}
                  </Table.Td>
                ))}
              </Table.Tr>
            ))}
          </Table.Tbody>
          <Table.Tfoot>
            <Table.Tr>
              <Table.Th>Totalt</Table.Th>
              {totals.map((total, index) => (
                <Table.Th key={columns[index].label} ta="right">
                  {formatCurrency(total)}
                </Table.Th>
              ))}
            </Table.Tr>
          </Table.Tfoot>
        </Table>
      </Table.ScrollContainer>
    </Card>
  )
}
