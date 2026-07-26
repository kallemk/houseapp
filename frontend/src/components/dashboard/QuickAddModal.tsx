import { Button, Group, Modal, Select, Stack, TextInput } from '@mantine/core'
import { useForm } from '@mantine/form'
import { FileUpload } from '../common/FileUpload'
import { useCreateValuation } from '../../hooks/useValuations'
import { useCreateRenovationEntry } from '../../hooks/useRenovationEntries'
import { useUploadDocument } from '../../hooks/useDocuments'
import type { DocumentCategory, RenovationCategory } from '../../api/types'

export type QuickAddType = 'valuation' | 'renovation' | 'document'

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
  valuation: 'Add valuation',
  renovation: 'Add renovation',
  document: 'Add document',
}

const RENOVATION_CATEGORY_OPTIONS: RenovationCategory[] = ['Renovation', 'Maintenance', 'Furniture', 'Other']

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
        <TextInput label="Date" type="date" required {...form.getInputProps('date')} />
        <TextInput label="Value" type="number" required {...form.getInputProps('value')} />
        <TextInput label="Source" placeholder="e.g. Appraisal" {...form.getInputProps('source')} />
        <Group justify="flex-end">
          <Button type="submit" loading={createValuation.isPending}>
            Save
          </Button>
        </Group>
      </Stack>
    </form>
  )
}

function QuickAddRenovationForm({
  propertyId,
  defaultDate,
  onDone,
}: {
  propertyId: string
  defaultDate: string
  onDone: () => void
}) {
  const createEntry = useCreateRenovationEntry(propertyId)
  const form = useForm({
    initialValues: { date: defaultDate, category: 'Renovation' as RenovationCategory, title: '', amount: 0, vendor: '' },
  })

  function handleSubmit(values: typeof form.values) {
    createEntry.mutate(
      {
        date: values.date,
        category: values.category,
        title: values.title,
        amount: Number(values.amount),
        vendor: values.vendor || null,
      },
      { onSuccess: onDone },
    )
  }

  return (
    <form onSubmit={form.onSubmit(handleSubmit)}>
      <Stack>
        <TextInput label="Date" type="date" required {...form.getInputProps('date')} />
        <Select
          label="Category"
          data={RENOVATION_CATEGORY_OPTIONS}
          allowDeselect={false}
          {...form.getInputProps('category')}
        />
        <TextInput label="Title" required {...form.getInputProps('title')} />
        <TextInput label="Amount" type="number" required {...form.getInputProps('amount')} />
        <TextInput label="Vendor" {...form.getInputProps('vendor')} />
        <Group justify="flex-end">
          <Button type="submit" loading={createEntry.isPending}>
            Save
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

  function handleUpload(file: File, category: DocumentCategory, date: string) {
    uploadDocument.mutate({ file, category, date }, { onSuccess: onDone })
  }

  return <FileUpload onUpload={handleUpload} uploading={uploadDocument.isPending} defaultDate={defaultDate} />
}

export function QuickAddModal({ propertyId, request, onClose }: QuickAddModalProps) {
  return (
    <Modal opened={request !== null} onClose={onClose} title={request ? TITLES[request.type] : ''} centered>
      {request?.type === 'valuation' && (
        <QuickAddValuationForm key={request.defaultDate} propertyId={propertyId} defaultDate={request.defaultDate} onDone={onClose} />
      )}
      {request?.type === 'renovation' && (
        <QuickAddRenovationForm key={request.defaultDate} propertyId={propertyId} defaultDate={request.defaultDate} onDone={onClose} />
      )}
      {request?.type === 'document' && (
        <QuickAddDocumentForm key={request.defaultDate} propertyId={propertyId} defaultDate={request.defaultDate} onDone={onClose} />
      )}
    </Modal>
  )
}
