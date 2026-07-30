import { Button, FileButton, Group, Select, TextInput } from '@mantine/core'
import { IconUpload } from '@tabler/icons-react'
import { useState } from 'react'
import type { DocumentCategory } from '../../api/types'
import { DOCUMENT_CATEGORY_OPTIONS } from '../../utils/labels'

function todayIsoDate() {
  return new Date().toISOString().slice(0, 10)
}

export interface UploadMeta {
  category: DocumentCategory
  date: string
  /** Required — the upload button stays disabled until it's filled in. */
  title: string
}

interface FileUploadProps {
  onUpload: (file: File, meta: UploadMeta) => void
  uploading?: boolean
  defaultDate?: string
}

export function FileUpload({ onUpload, uploading, defaultDate }: FileUploadProps) {
  const [category, setCategory] = useState<DocumentCategory>('Other')
  const [date, setDate] = useState(defaultDate ?? todayIsoDate())
  const [title, setTitle] = useState('')

  const titled = title.trim().length > 0

  function handleFile(file: File | null) {
    if (!file || !date || !titled) {
      return
    }
    onUpload(file, { category, date, title: title.trim() })
    setTitle('')
  }

  return (
    <Group align="flex-end">
      <TextInput label="Datum" type="date" value={date} onChange={(e) => setDate(e.currentTarget.value)} w={160} />
      <Select
        label="Kategori"
        data={DOCUMENT_CATEGORY_OPTIONS}
        value={category}
        onChange={(value) => setCategory((value as DocumentCategory) ?? 'Other')}
        allowDeselect={false}
        w={160}
      />
      {/* Filenames are often meaningless ("scan_0042.pdf"), so this is what gets shown instead.
          `withAsterisk` rather than `required`: this widget renders inside the project form on the
          project page, and a native `required` there made the browser block *saving the project*
          over an empty document title. The asterisk keeps the cue; the disabled button below is what
          actually enforces it, at the only moment it matters. For the same reason Enter mustn't
          bubble — it would submit the surrounding form instead of doing nothing. */}
      <TextInput
        label="Titel"
        placeholder="t.ex. Besiktningsprotokoll"
        withAsterisk
        value={title}
        onChange={(e) => setTitle(e.currentTarget.value)}
        onKeyDown={(e) => e.key === 'Enter' && e.preventDefault()}
        w={240}
      />
      {/* Disabled rather than validated after the fact: the file picker opens on click, so there's
          no natural moment to show an error before the upload would already be under way. */}
      <FileButton onChange={handleFile}>
        {(props) => (
          <Button
            {...props}
            leftSection={<IconUpload size={16} />}
            loading={uploading}
            disabled={!titled}
            title={titled ? undefined : 'Fyll i en titel först'}
          >
            Ladda upp fil
          </Button>
        )}
      </FileButton>
    </Group>
  )
}
