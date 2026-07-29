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
  /** Null when left blank — the UI falls back to the filename. */
  title: string | null
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

  function handleFile(file: File | null) {
    if (!file || !date) {
      return
    }
    onUpload(file, { category, date, title: title.trim() || null })
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
      {/* Filenames are often meaningless ("scan_0042.pdf"), so this is what gets shown instead. */}
      <TextInput
        label="Titel"
        placeholder="valfritt, t.ex. Besiktningsprotokoll"
        value={title}
        onChange={(e) => setTitle(e.currentTarget.value)}
        w={240}
      />
      <FileButton onChange={handleFile}>
        {(props) => (
          <Button {...props} leftSection={<IconUpload size={16} />} loading={uploading}>
            Ladda upp fil
          </Button>
        )}
      </FileButton>
    </Group>
  )
}
