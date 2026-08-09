import { Card, Group, Table, Text, ThemeIcon } from '@mantine/core'
import { IconCoins } from '@tabler/icons-react'
import type { ProjectDto, WorkType } from '../../api/types'
import { formatCurrency, formatNumber } from '../../utils/currency'
import { WORK_TYPE_LABELS } from '../../utils/labels'
import { anyDate, inYear, since, spent, twelveMonthsAgo } from '../../utils/spend'

const WORK_TYPES: WorkType[] = ['Maintenance', 'Renovation', 'Investment', 'Purchase']

/** A rough industry rule of thumb for annual upkeep on a Swedish house, as a share of its value. */
const MAINTENANCE_RULE_OF_THUMB_PERCENT = 1

interface Column {
  label: string
  /** Cost rows counted by this column. */
  includes: (dateIncurred: string) => boolean
  /** Divides the column by the years owned — only the average column does. */
  perYear?: boolean
}

export function SpendBreakdown({
  projects,
  purchaseDate,
  currentValue,
}: {
  projects: ProjectDto[]
  purchaseDate: string
  currentValue: number
}) {
  const today = new Date()
  const thisYear = today.getFullYear()
  const lastYear = thisYear - 1

  // Fractional, so a house owned for 18 months averages over 1.5 years rather than 1 or 2.
  const yearsOwned = (today.getTime() - new Date(purchaseDate).getTime()) / (365.25 * 24 * 60 * 60 * 1000)
  // Under a year there's no meaningful annual average to state — dividing by a fraction would
  // extrapolate a few months of spending into a confident yearly figure.
  const canAverage = yearsOwned >= 1

  const columns: Column[] = [
    { label: `I år (${thisYear})`, includes: inYear(thisYear) },
    // Sidesteps the artifact that "i år" is nearly empty every January, which would otherwise read
    // as a collapse in spending next to a full previous year.
    { label: 'Rullande 12 mån', includes: since(twelveMonthsAgo(today)) },
    { label: `Förra året (${lastYear})`, includes: inYear(lastYear) },
    { label: 'Totalt', includes: anyDate },
    { label: 'Snitt/år', includes: anyDate, perYear: true },
  ]

  const rows = WORK_TYPES.map((workType) => ({
    workType,
    amounts: columns.map((column) => {
      const total = spent(projects, column.includes, workType)
      return column.perYear ? total / yearsOwned : total
    }),
  }))

  const totals = columns.map((_, index) => rows.reduce((sum, row) => sum + row.amounts[index], 0))

  const maintenancePerYear = rows.find((r) => r.workType === 'Maintenance')?.amounts.at(-1) ?? 0
  const maintenancePercent = currentValue > 0 ? (maintenancePerYear / currentValue) * 100 : null

  function cell(amount: number, column: Column) {
    if (column.perYear && !canAverage) {
      return '—'
    }
    return formatCurrency(amount)
  }

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
      <Table.ScrollContainer minWidth={820}>
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
                  <Table.Td
                    key={columns[index].label}
                    ta="right"
                    c={amount === 0 || (columns[index].perYear && !canAverage) ? 'dimmed' : undefined}
                  >
                    {cell(amount, columns[index])}
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
                  {cell(total, columns[index])}
                </Table.Th>
              ))}
            </Table.Tr>
          </Table.Tfoot>
        </Table>
      </Table.ScrollContainer>

      {/* A yardstick rather than a target: it says where you sit, not what you should spend. */}
      {canAverage && maintenancePercent !== null && (
        <Text size="xs" c="dimmed" px="lg" py="sm">
          Underhållet motsvarar {formatNumber(maintenancePercent, 1)} % av bostadens nuvarande värde
          per år. En vanlig tumregel för villor är omkring {MAINTENANCE_RULE_OF_THUMB_PERCENT} %.
        </Text>
      )}
    </Card>
  )
}
