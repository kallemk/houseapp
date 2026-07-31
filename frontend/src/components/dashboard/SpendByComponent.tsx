import { Card, Group, Progress, Stack, Text, ThemeIcon } from '@mantine/core'
import { IconChartPie } from '@tabler/icons-react'
import type { ProjectDto, PropertyLocalComponentDto } from '../../api/types'
import { formatCurrency, formatNumber } from '../../utils/currency'

/** Enough to show where the money concentrates without turning into the projects table. */
const TOP_N = 5

/**
 * Which parts of the house have cost the most, over all time. Answers a question none of the
 * work-type figures do — those say *what kind* of work it was, not *what* it was done to.
 *
 * Components are resolved through the property's own list, so a locally renamed component shows its
 * local name. An id with no match is grouped under "Okänd" rather than dropped: the money was
 * spent either way, and silently omitting it would make the shares add up to less than the total.
 */
export function SpendByComponent({
  projects,
  components,
}: {
  projects: ProjectDto[]
  components: PropertyLocalComponentDto[]
}) {
  const namesById = new Map(components.map((c) => [c.id, c.name]))

  const totals = new Map<string, number>()
  for (const project of projects) {
    const spent = project.costs.reduce((sum, c) => sum + c.amount, 0)
    if (spent === 0) {
      continue
    }
    const name = namesById.get(project.componentId) ?? 'Okänd'
    totals.set(name, (totals.get(name) ?? 0) + spent)
  }

  const ranked = [...totals.entries()].sort((a, b) => b[1] - a[1])
  const grandTotal = ranked.reduce((sum, [, amount]) => sum + amount, 0)

  if (ranked.length === 0) {
    return null
  }

  const top = ranked.slice(0, TOP_N)
  const rest = ranked.slice(TOP_N)
  const restTotal = rest.reduce((sum, [, amount]) => sum + amount, 0)

  return (
    <Card withBorder padding="lg">
      <Group gap="sm" mb="md">
        <ThemeIcon variant="light" size={40} radius="md">
          <IconChartPie size={20} />
        </ThemeIcon>
        <div>
          <Text size="sm" c="dimmed">
            Vart pengarna går
          </Text>
          <Text size="xs" c="dimmed">
            Total kostnad per del av bostaden.
          </Text>
        </div>
      </Group>

      <Stack gap="sm">
        {top.map(([name, amount]) => {
          const share = grandTotal > 0 ? (amount / grandTotal) * 100 : 0
          return (
            <div key={name}>
              <Group justify="space-between" gap="xs" wrap="nowrap" mb={4}>
                <Text size="sm">{name}</Text>
                <Text size="sm" fw={600} style={{ whiteSpace: 'nowrap' }}>
                  {formatCurrency(amount)}{' '}
                  <Text span size="xs" c="dimmed" fw={400}>
                    ({formatNumber(share, 0)} %)
                  </Text>
                </Text>
              </Group>
              <Progress value={share} color="terracotta" size="sm" radius="sm" />
            </div>
          )
        })}
        {rest.length > 0 && (
          <Text size="xs" c="dimmed">
            Övriga {rest.length} delar: {formatCurrency(restTotal)}
          </Text>
        )}
      </Stack>
    </Card>
  )
}
