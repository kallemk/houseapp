import {
  Button,
  Card,
  Center,
  Group,
  Loader,
  NumberInput,
  Progress,
  Select,
  Stack,
  Table,
  Text,
  ThemeIcon,
  Title,
} from '@mantine/core'
import { useForm } from '@mantine/form'
import { notifications } from '@mantine/notifications'
import { IconWallet } from '@tabler/icons-react'
import { useState } from 'react'
import { Navigate, useParams } from 'react-router-dom'
import type { BudgetDto, WorkType } from '../api/types'
import { useBudgets, useSaveBudget } from '../hooks/useBudgets'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { formatCurrency } from '../utils/currency'
import { WORK_TYPE_COLORS, WORK_TYPE_LABELS } from '../utils/labels'

interface BudgetFormValues {
  maintenanceBudget: number | string
  renovationBudget: number | string
  investmentBudget: number | string
}

function budgetedFor(budget: BudgetDto | undefined, workType: WorkType): number {
  return budget?.lines.find((l) => l.workType === workType)?.budgeted ?? 0
}

export function BudgetPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const { property, isLoading: loadingProperty, notFound } = useSelectedProperty(propertyId)
  const { data: budgets, isLoading } = useBudgets(propertyId ?? '')
  const saveBudget = useSaveBudget(propertyId ?? '')
  const [selectedYear, setSelectedYear] = useState<number | null>(null)
  const [editing, setEditing] = useState(false)

  const form = useForm<BudgetFormValues>({
    initialValues: { maintenanceBudget: '', renovationBudget: '', investmentBudget: '' },
  })

  if (loadingProperty || isLoading) {
    return (
      <Center py="xl">
        <Loader />
      </Center>
    )
  }

  if (notFound || !property) {
    return <Navigate to="/properties" replace />
  }

  const currentYear = new Date().getFullYear()
  // The API returns every year that has a budget or any spend; the current year is always offered
  // even when it has neither, since that's the one you'd want to plan.
  const years = Array.from(new Set([currentYear, ...(budgets ?? []).map((b) => b.year)])).sort((a, b) => b - a)
  const year = selectedYear ?? years[0] ?? currentYear
  const budget = (budgets ?? []).find((b) => b.year === year)

  function startEditing() {
    form.setValues({
      maintenanceBudget: budgetedFor(budget, 'Maintenance') || '',
      renovationBudget: budgetedFor(budget, 'Renovation') || '',
      investmentBudget: budgetedFor(budget, 'Investment') || '',
    })
    setEditing(true)
  }

  function handleSubmit(values: BudgetFormValues) {
    saveBudget.mutate(
      {
        year,
        maintenanceBudget: Number(values.maintenanceBudget) || 0,
        renovationBudget: Number(values.renovationBudget) || 0,
        investmentBudget: Number(values.investmentBudget) || 0,
      },
      {
        onSuccess: () => setEditing(false),
        onError: () => notifications.show({ color: 'red', message: 'Kunde inte spara budgeten. Försök igen.' }),
      },
    )
  }

  return (
    <Stack>
      <Group justify="space-between">
        <Group gap="sm">
          <ThemeIcon variant="light" size={36} radius="md">
            <IconWallet size={20} />
          </ThemeIcon>
          <Title order={2}>Budget</Title>
        </Group>
        <Group>
          <Select
            value={String(year)}
            onChange={(value) => {
              setSelectedYear(value ? Number(value) : null)
              setEditing(false)
            }}
            allowDeselect={false}
            w={120}
            data={years.map((y) => ({ value: String(y), label: String(y) }))}
          />
          {!editing && <Button onClick={startEditing}>{budget?.id ? 'Redigera' : 'Sätt budget'}</Button>}
        </Group>
      </Group>
      <Text c="dimmed" size="sm">
        Utfallet räknas fram från projektens kostnadsposter — en kostnad hör till året den betalades,
        så ett projekt över årsskiftet delas mellan åren.
      </Text>

      {editing ? (
        <Card withBorder padding="lg">
          <form onSubmit={form.onSubmit(handleSubmit)}>
            <Stack maw={360}>
              <NumberInput label="Underhåll (kr)" min={0} {...form.getInputProps('maintenanceBudget')} />
              <NumberInput label="Renovering (kr)" min={0} {...form.getInputProps('renovationBudget')} />
              <NumberInput label="Nyinvestering (kr)" min={0} {...form.getInputProps('investmentBudget')} />
              <Group>
                <Button type="submit" loading={saveBudget.isPending}>
                  Spara
                </Button>
                <Button variant="default" onClick={() => setEditing(false)}>
                  Avbryt
                </Button>
              </Group>
            </Stack>
          </form>
        </Card>
      ) : (
        <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
          <Table.ScrollContainer minWidth={700}>
            <Table verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Typ av arbete</Table.Th>
                  <Table.Th>Budget</Table.Th>
                  <Table.Th>Utfall</Table.Th>
                  <Table.Th>Kvar</Table.Th>
                  <Table.Th w={200} />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {(budget?.lines ?? []).map((line) => {
                  const overspent = line.remaining < 0
                  const percent = line.budgeted > 0 ? Math.min((line.spent / line.budgeted) * 100, 100) : 0
                  return (
                    <Table.Tr key={line.workType}>
                      <Table.Td>{WORK_TYPE_LABELS[line.workType]}</Table.Td>
                      <Table.Td c="dimmed">{formatCurrency(line.budgeted)}</Table.Td>
                      <Table.Td fw={600}>{formatCurrency(line.spent)}</Table.Td>
                      <Table.Td c={overspent ? 'red' : undefined} fw={overspent ? 600 : undefined}>
                        {formatCurrency(line.remaining)}
                      </Table.Td>
                      <Table.Td>
                        {line.budgeted > 0 ? (
                          <Progress
                            value={percent}
                            color={overspent ? 'red' : WORK_TYPE_COLORS[line.workType]}
                            size="lg"
                            radius="sm"
                          />
                        ) : (
                          <Text size="xs" c="dimmed">
                            Ingen budget satt
                          </Text>
                        )}
                      </Table.Td>
                    </Table.Tr>
                  )
                })}
              </Table.Tbody>
              <Table.Tfoot>
                <Table.Tr>
                  <Table.Th>Totalt</Table.Th>
                  <Table.Th>{formatCurrency(budget?.totalBudgeted ?? 0)}</Table.Th>
                  <Table.Th>{formatCurrency(budget?.totalSpent ?? 0)}</Table.Th>
                  <Table.Th>{formatCurrency((budget?.totalBudgeted ?? 0) - (budget?.totalSpent ?? 0))}</Table.Th>
                  <Table.Th />
                </Table.Tr>
              </Table.Tfoot>
            </Table>
          </Table.ScrollContainer>
        </Card>
      )}
    </Stack>
  )
}
