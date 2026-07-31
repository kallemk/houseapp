import { Button, Group, Modal, Select, Stack, Text, TextInput } from '@mantine/core'
import { useForm } from '@mantine/form'
import { notifications } from '@mantine/notifications'
import type { DocumentCategory, DocumentDto } from '../../api/types'
import { useUpdateDocument } from '../../hooks/useDocuments'
import { DOCUMENT_CATEGORY_OPTIONS } from '../../utils/labels'

interface EditDocumentFormValues {
  title: string
  date: string
  category: DocumentCategory
}

/**
 * The fields, as their own component so `useForm` can take the document's values as its
 * initialValues. Mounted with a key on the document id below, which is what makes opening a
 * different row reset the fields instead of carrying the previous one's over.
 */
function EditDocumentForm({
  document,
  propertyId,
  onClose,
}: {
  document: DocumentDto
  propertyId: string
  onClose: () => void
}) {
  const updateDocument = useUpdateDocument(propertyId)
  const form = useForm<EditDocumentFormValues>({
    initialValues: {
      // Documents uploaded before titles existed have none; editing one is where that finally gets
      // filled in rather than being an exception to the "title required" rule.
      title: document.title ?? '',
      date: document.date,
      category: document.category,
    },
    validate: {
      title: (value) => (value.trim().length === 0 ? 'Titel krävs' : null),
    },
  })

  function handleSubmit(values: EditDocumentFormValues) {
    updateDocument.mutate(
      { id: document.id, title: values.title.trim(), date: values.date, category: values.category },
      {
        onSuccess: onClose,
        onError: () =>
          notifications.show({ color: 'red', message: 'Kunde inte spara ändringarna. Försök igen.' }),
      },
    )
  }

  return (
    <form onSubmit={form.onSubmit(handleSubmit)}>
      <Stack>
        <TextInput label="Titel" required {...form.getInputProps('title')} />
        <TextInput label="Datum" type="date" required {...form.getInputProps('date')} />
        <Select
          label="Kategori"
          data={DOCUMENT_CATEGORY_OPTIONS}
          allowDeselect={false}
          {...form.getInputProps('category')}
        />
        <Text size="xs" c="dimmed">
          Filnamnet ({document.fileName}) och själva filen ändras inte.
        </Text>
        <Group justify="flex-end">
          <Button variant="default" onClick={onClose}>
            Avbryt
          </Button>
          <Button type="submit" loading={updateDocument.isPending}>
            Spara
          </Button>
        </Group>
      </Stack>
    </form>
  )
}

/**
 * Corrects what the app records about a document. Metadata only — the file itself is never touched,
 * so this behaves identically whether the document lives in Blob Storage or Google Drive.
 *
 * Worth knowing for Drive: the title is the app's label, not the Drive file's name. The file keeps
 * the filename it was uploaded under, which is why the filename is spelled out in the form.
 */
export function EditDocumentModal({
  document,
  propertyId,
  onClose,
}: {
  document: DocumentDto | null
  propertyId: string
  onClose: () => void
}) {
  return (
    <Modal opened={document !== null} onClose={onClose} title="Redigera dokument" centered>
      {document && (
        <EditDocumentForm key={document.id} document={document} propertyId={propertyId} onClose={onClose} />
      )}
    </Modal>
  )
}
