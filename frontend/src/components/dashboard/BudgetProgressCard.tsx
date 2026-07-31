import { Anchor, Card, Group, Progress, Stack, Text, ThemeIcon } from '@mantine/core'
import { IconWallet } from '@tabler/icons-react'
import { Link } from 'react-router-dom'
import type { BudgetDto } from '../../api/types'
import { formatCurrency } from '../../utils/currency'
import { WORK_TYPE_COLORS, WORK_TYPE_LABELS } from '../../utils/labels'

/**
 * How this year's plan is holding up. Rendered only when a budget has actually been set for the
 * current year — an "ingen budget"-placeholder on the dashboard would be a permanent nag on every
 * property whose owners don't budget, and the budget page is one click away either way.
 */
export function BudgetProgressCard({ budget, propertyId }: { budget: BudgetDto; propertyId: string }) {
  const remaining = budget.totalBudgeted - budget.totalSpent
  const overspent = remaining < 0
  const percent = budget.totalBudgeted > 0 ? Math.min((budget.totalSpent / budget.totalBudgeted) * 100, 100) : 0

  return (
    <Card withBorder padding="lg">
      <Group justify="space-between" mb="sm">
        <Group gap="sm">
          <ThemeIcon variant="light" size={40} radius="md">
            <IconWallet size={20} />
          </ThemeIcon>
          <div>
            <Text size="sm" c="dimmed">
              Budget {budget.year}
            </Text>
            <Text size="xl" fw={700} c={overspent ? 'red' : undefined}>
              {overspent
                ? `${formatCurrency(Math.abs(remaining))} över budget`
                : `${formatCurrency(remaining)} kvar`}
            </Text>
            <Text size="xs" c="dimmed">
              {formatCurrency(budget.totalSpent)} av {formatCurrency(budget.totalBudgeted)} använt
            </Text>
          </div>
        </Group>
        <Anchor component={Link} to={`/properties/${propertyId}/budget`} size="sm">
          Till budgeten
        </Anchor>
      </Group>

      <Progress value={percent} color={overspent ? 'red' : 'terracotta'} size="lg" radius="sm" mb="md" />

      <Stack gap={6}>
        {budget.lines.map((line) => {
          const lineOver = line.remaining < 0
          return (
            <Group key={line.workType} justify="space-between" gap="xs" wrap="nowrap">
              <Text size="sm" c={WORK_TYPE_COLORS[line.workType]} fw={500}>
                {WORK_TYPE_LABELS[line.workType]}
              </Text>
              <Text size="sm" c={lineOver ? 'red' : 'dimmed'} fw={lineOver ? 600 : undefined}>
                {formatCurrency(line.spent)} / {formatCurrency(line.budgeted)}
              </Text>
            </Group>
          )
        })}
      </Stack>
    </Card>
  )
}
