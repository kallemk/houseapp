import { Button, FileButton, Group, Select, TextInput } from '@mantine/core'
import { IconUpload } from '@tabler/icons-react'
import { useState } from 'react'
import type { DocumentCategory } from '../../api/types'
import { DOCUMENT_CATEGORY_OPTIONS } from '../../utils/labels'

function todayIsoDate() {
  return new Date().toISOString().slice(0, 10)
}

interface FileUploadProps {
  onUpload: (file: File, category: DocumentCategory, date: string) => void
  uploading?: boolean
  defaultDate?: string
}

export function FileUpload({ onUpload, uploading, defaultDate }: FileUploadProps) {
  const [category, setCategory] = useState<DocumentCategory>('Other')
  const [date, setDate] = useState(defaultDate ?? todayIsoDate())

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
      <FileButton onChange={(file) => file && date && onUpload(file, category, date)}>
        {(props) => (
          <Button {...props} leftSection={<IconUpload size={16} />} loading={uploading}>
            Ladda upp fil
          </Button>
        )}
      </FileButton>
    </Group>
  )
}
