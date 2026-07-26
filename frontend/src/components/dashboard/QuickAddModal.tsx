import { Button, Group, Modal, Select, Stack, TextInput } from '@mantine/core'
import { useForm } from '@mantine/form'
import { FileUpload } from '../common/FileUpload'
import { useCreateValuation } from '../../hooks/useValuations'
import { useCreateRenovationEntry } from '../../hooks/useRenovationEntries'
import { useUploadDocument } from '../../hooks/useDocuments'
import type { DocumentCategory, RenovationCategory } from '../../api/types'
import { RENOVATION_CATEGORY_OPTIONS } from '../../utils/labels'

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
  valuation: 'Lägg till värdering',
  renovation: 'Lägg till renovering',
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
        <TextInput label="Datum" type="date" required {...form.getInputProps('date')} />
        <Select
          label="Kategori"
          data={RENOVATION_CATEGORY_OPTIONS}
          allowDeselect={false}
          {...form.getInputProps('category')}
        />
        <TextInput label="Titel" required {...form.getInputProps('title')} />
        <TextInput label="Belopp (kr)" type="number" required {...form.getInputProps('amount')} />
        <TextInput label="Leverantör" {...form.getInputProps('vendor')} />
        <Group justify="flex-end">
          <Button type="submit" loading={createEntry.isPending}>
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
