import { Button, Center, Group, Loader, Modal, Select, Stack, TextInput } from '@mantine/core'
import { useForm } from '@mantine/form'
import { FileUpload, type UploadMeta } from '../common/FileUpload'
import { useCreateValuation } from '../../hooks/useValuations'
import { useCreateProject } from '../../hooks/useProjects'
import { usePropertyComponents } from '../../hooks/usePropertyComponents'
import { useUploadDocument } from '../../hooks/useDocuments'
import type { WorkType } from '../../api/types'
import { WORK_TYPE_OPTIONS } from '../../utils/labels'

export type QuickAddType = 'valuation' | 'project' | 'document'

export interface QuickAddRequest {
  type: QuickAddType
  defaultDate: string
}

interface QuickAddModalProps {
  propertyId: string
  request: QuickAddRequest | null
  onClose: () => void
}

const TITLES: Record<QuickAddType, string> = {
  valuation: 'Lägg till värdering',
  project: 'Lägg till projekt',
  document: 'Lägg till dokument',
}

function QuickAddValuationForm({
  propertyId,
  defaultDate,
  onDone,
}: {
  propertyId: string
  defaultDate: string
  onDone: () => void
}) {
  const createValuation = useCreateValuation(propertyId)
  const form = useForm({ initialValues: { date: defaultDate, value: 0, source: '' } })

  function handleSubmit(values: typeof form.values) {
    createValuation.mutate(
      { date: values.date, value: Number(values.value), source: values.source || null, notes: null },
      { onSuccess: onDone },
    )
  }

  return (
    <form onSubmit={form.onSubmit(handleSubmit)}>
      <Stack>
        <TextInput label="Datum" type="date" required {...form.getInputProps('date')} />
        <TextInput label="Värde (kr)" type="number" required {...form.getInputProps('value')} />
        <TextInput label="Källa" placeholder="t.ex. värdering" {...form.getInputProps('source')} />
        <Group justify="flex-end">
          <Button type="submit" loading={createValuation.isPending}>
            Spara
          </Button>
        </Group>
      </Stack>
    </form>
  )
}

/**
 * Deliberately a fraction of the full project form: this is for logging something that has already
 * happened, straight from the timeline. Everything else (contractor, itemised costs, impact) is
 * filled in afterwards on the project page.
 */
function QuickAddProjectForm({
  propertyId,
  defaultDate,
  onDone,
}: {
  propertyId: string
  defaultDate: string
  onDone: () => void
}) {
  const createProject = useCreateProject(propertyId)
  const { data: components, isLoading: loadingComponents } = usePropertyComponents()
  const form = useForm({
    initialValues: {
      date: defaultDate,
      name: '',
      workType: 'Maintenance' as WorkType,
      componentId: '',
      amount: 0,
      vendor: '',
    },
    validate: {
      componentId: (value) => (value ? null : 'Välj en komponent'),
    },
  })

  function handleSubmit(values: typeof form.values) {
    const amount = Number(values.amount) || 0
    createProject.mutate(
      {
        name: values.name,
        description: null,
        notes: null,
        workType: values.workType,
        componentId: values.componentId,
        status: 'Completed',
        priority: 'Medium',
        isUrgent: false,
        plannedStartDate: null,
        actualStartDate: values.date,
        completedDate: values.date,
        estimatedDurationDays: null,
        estimatedCost: amount,
        estimatedValueIncrease: null,
        expectedLifespanYears: null,
        energyEfficiencyGainPercent: null,
        contractor: values.vendor ? { name: values.vendor, phone: null, email: null, website: null, quotedPrice: null, quotedDate: null, notes: null } : null,
        costs: amount > 0 ? [{ type: 'Other', description: null, amount, dateIncurred: values.date, isBudgeted: false }] : [],
        milestones: [],
      },
      { onSuccess: onDone },
    )
  }

  if (loadingComponents) {
    return (
      <Center py="md">
        <Loader size="sm" />
      </Center>
    )
  }

  return (
    <form onSubmit={form.onSubmit(handleSubmit)}>
      <Stack>
        <TextInput label="Datum" type="date" required {...form.getInputProps('date')} />
        <TextInput label="Namn" required {...form.getInputProps('name')} />
        <Select
          label="Typ av arbete"
          data={WORK_TYPE_OPTIONS}
          allowDeselect={false}
          {...form.getInputProps('workType')}
        />
        <Select
          label="Komponent"
          placeholder="Välj komponent"
          data={(components ?? []).map((c) => ({ value: c.id, label: c.name }))}
          {...form.getInputProps('componentId')}
        />
        <TextInput label="Kostnad (kr)" type="number" {...form.getInputProps('amount')} />
        <TextInput label="Entreprenör" {...form.getInputProps('vendor')} />
        <Group justify="flex-end">
          <Button type="submit" loading={createProject.isPending}>
            Spara
          </Button>
        </Group>
      </Stack>
    </form>
  )
}

function QuickAddDocumentForm({
  propertyId,
  defaultDate,
  onDone,
}: {
  propertyId: string
  defaultDate: string
  onDone: () => void
}) {
  const uploadDocument = useUploadDocument(propertyId)

  function handleUpload(file: File, meta: UploadMeta) {
    uploadDocument.mutate({ file, ...meta }, { onSuccess: onDone })
  }

  return <FileUpload onUpload={handleUpload} uploading={uploadDocument.isPending} defaultDate={defaultDate} />
}

export function QuickAddModal({ propertyId, request, onClose }: QuickAddModalProps) {
  return (
    <Modal opened={request !== null} onClose={onClose} title={request ? TITLES[request.type] : ''} centered>
      {request?.type === 'valuation' && (
        <QuickAddValuationForm key={request.defaultDate} propertyId={propertyId} defaultDate={request.defaultDate} onDone={onClose} />
      )}
      {request?.type === 'project' && (
        <QuickAddProjectForm key={request.defaultDate} propertyId={propertyId} defaultDate={request.defaultDate} onDone={onClose} />
      )}
      {request?.type === 'document' && (
        <QuickAddDocumentForm key={request.defaultDate} propertyId={propertyId} defaultDate={request.defaultDate} onDone={onClose} />
      )}
    </Modal>
  )
}
