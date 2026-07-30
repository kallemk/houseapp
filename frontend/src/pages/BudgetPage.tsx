import {
  Button,
  Card,
  Center,
  Group,
  Loader,
  Modal,
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
import { IconPlus, IconWallet } from '@tabler/icons-react'
import { useState } from 'react'
import { Navigate, useParams } from 'react-router-dom'
import type { BudgetDto, WorkType } from '../api/types'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { useBudgets, useDeleteBudget, useSaveBudget } from '../hooks/useBudgets'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { formatCurrency } from '../utils/currency'
import { WORK_TYPE_COLORS, WORK_TYPE_LABELS } from '../utils/labels'

interface BudgetFormValues {
  maintenanceBudget: number | string
  renovationBudget: number | string
  investmentBudget: number | string
}

interface NewYearFormValues extends BudgetFormValues {
  year: number | string
}

function budgetedFor(budget: BudgetDto | undefined, workType: WorkType): number {
  return budget?.lines.find((l) => l.workType === workType)?.budgeted ?? 0
}

function toAmounts(values: BudgetFormValues) {
  return {
    maintenanceBudget: Number(values.maintenanceBudget) || 0,
    renovationBudget: Number(values.renovationBudget) || 0,
    investmentBudget: Number(values.investmentBudget) || 0,
  }
}

/** The same three inputs in the inline edit card and in the "new year" modal. */
function BudgetAmountFields({
  getInputProps,
}: {
  getInputProps: (path: keyof BudgetFormValues) => object
}) {
  return (
    <>
      <NumberInput label="Underhåll (kr)" min={0} {...getInputProps('maintenanceBudget')} />
      <NumberInput label="Renovering (kr)" min={0} {...getInputProps('renovationBudget')} />
      <NumberInput label="Nyinvestering (kr)" min={0} {...getInputProps('investmentBudget')} />
    </>
  )
}

export function BudgetPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const { property, isLoading: loadingProperty, notFound } = useSelectedProperty(propertyId)
  const { data: budgets, isLoading } = useBudgets(propertyId ?? '')
  const saveBudget = useSaveBudget(propertyId ?? '')
  const deleteBudget = useDeleteBudget(propertyId ?? '')
  const [selectedYear, setSelectedYear] = useState<number | null>(null)
  const [editing, setEditing] = useState(false)
  const [addingYear, setAddingYear] = useState(false)
  const [confirmingDelete, setConfirmingDelete] = useState(false)

  const form = useForm<BudgetFormValues>({
    initialValues: { maintenanceBudget: '', renovationBudget: '', investmentBudget: '' },
  })

  const newYearForm = useForm<NewYearFormValues>({
    initialValues: { year: '', maintenanceBudget: '', renovationBudget: '', investmentBudget: '' },
    validate: {
      year: (value) => {
        const year = Number(value)
        if (!Number.isInteger(year) || year < 1900 || year > 2200) {
          return 'Ange ett årtal'
        }
        // The endpoint upserts, so saving an existing year would quietly overwrite it. Point at the
        // year picker instead of silently replacing a plan someone already made.
        if ((budgets ?? []).some((b) => b.year === year && b.id)) {
          return 'Det finns redan en budget för det året — välj året i listan för att ändra den.'
        }
        return null
      },
    },
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
  // even when it has neither, since that's the one you'd want to plan. selectedYear is included so
  // a year just created stays picked while the list refetches, rather than blanking the Select.
  const years = Array.from(
    new Set([currentYear, ...(budgets ?? []).map((b) => b.year), ...(selectedYear ? [selectedYear] : [])]),
  ).sort((a, b) => b - a)
  // The year you're in wins whenever it has a plan of its own — that's the one you're actually
  // spending against. Only when it hasn't does the newest year stand in, so planning 2027 ahead of
  // time still lands you on it rather than on an empty current year.
  const defaultYear = (budgets ?? []).some((b) => b.year === currentYear && b.id)
    ? currentYear
    : (years[0] ?? currentYear)
  const year = selectedYear ?? defaultYear
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
      { year, ...toAmounts(values) },
      {
        onSuccess: () => setEditing(false),
        onError: () => notifications.show({ color: 'red', message: 'Kunde inte spara budgeten. Försök igen.' }),
      },
    )
  }

  function startAddingYear() {
    // Next year after the latest one on record — the year you'd normally be planning.
    newYearForm.setValues({
      year: Math.max(currentYear, ...years) + 1,
      maintenanceBudget: '',
      renovationBudget: '',
      investmentBudget: '',
    })
    newYearForm.clearErrors()
    setAddingYear(true)
  }

  function handleCreateYear(values: NewYearFormValues) {
    const newYear = Number(values.year)
    saveBudget.mutate(
      { year: newYear, ...toAmounts(values) },
      {
        onSuccess: () => {
          setAddingYear(false)
          setSelectedYear(newYear)
          setEditing(false)
        },
        onError: () => notifications.show({ color: 'red', message: 'Kunde inte spara budgeten. Försök igen.' }),
      },
    )
  }

  function handleDelete() {
    deleteBudget.mutate(year, {
      onSuccess: () => {
        setEditing(false)
        setSelectedYear(null)
      },
      onError: () => notifications.show({ color: 'red', message: 'Kunde inte ta bort budgeten. Försök igen.' }),
    })
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
          {!editing && (
            <>
              <Button variant="default" leftSection={<IconPlus size={16} />} onClick={startAddingYear}>
                Nytt år
              </Button>
              <Button onClick={startEditing}>{budget?.id ? 'Redigera' : 'Sätt budget'}</Button>
            </>
          )}
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
              <BudgetAmountFields getInputProps={form.getInputProps} />
              <Group justify="space-between">
                <Group>
                  <Button type="submit" loading={saveBudget.isPending}>
                    Spara
                  </Button>
                  <Button variant="default" onClick={() => setEditing(false)}>
                    Avbryt
                  </Button>
                </Group>
                {/* Only for a year that actually has a saved budget — otherwise there's nothing to
                    remove, and a year that only has spend can't be deleted anyway. */}
                {budget?.id && (
                  <Button color="red" variant="subtle" onClick={() => setConfirmingDelete(true)}>
                    Ta bort
                  </Button>
                )}
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

      <Modal
        opened={addingYear}
        onClose={() => setAddingYear(false)}
        title="Nytt budgetår"
        centered
      >
        <form onSubmit={newYearForm.onSubmit(handleCreateYear)}>
          <Stack>
            <NumberInput
              label="År"
              required
              allowDecimal={false}
              {...newYearForm.getInputProps('year')}
            />
            <BudgetAmountFields getInputProps={newYearForm.getInputProps} />
            <Group justify="flex-end">
              <Button variant="default" onClick={() => setAddingYear(false)}>
                Avbryt
              </Button>
              <Button type="submit" loading={saveBudget.isPending}>
                Spara
              </Button>
            </Group>
          </Stack>
        </form>
      </Modal>

      <ConfirmDialog
        opened={confirmingDelete}
        title="Ta bort budget"
        message={`Budgeten för ${year} tas bort. Utfallet påverkas inte — det räknas fram från projektens kostnader.`}
        onCancel={() => setConfirmingDelete(false)}
        onConfirm={() => {
          setConfirmingDelete(false)
          handleDelete()
        }}
      />
    </Stack>
  )
}
