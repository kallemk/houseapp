import { Button, FileButton, Group, Select, TextInput } from '@mantine/core'
import { IconUpload } from '@tabler/icons-react'
import { useState } from 'react'
import type { DocumentCategory } from '../../api/types'

const CATEGORY_OPTIONS: { value: DocumentCategory; label: string }[] = [
  { value: 'Deed', label: 'Deed' },
  { value: 'Warranty', label: 'Warranty' },
  { value: 'Receipt', label: 'Receipt' },
  { value: 'Photo', label: 'Photo' },
  { value: 'Other', label: 'Other' },
]

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
      <TextInput label="Date" type="date" value={date} onChange={(e) => setDate(e.currentTarget.value)} w={160} />
      <Select
        label="Category"
        data={CATEGORY_OPTIONS}
        value={category}
        onChange={(value) => setCategory((value as DocumentCategory) ?? 'Other')}
        allowDeselect={false}
        w={160}
      />
      <FileButton onChange={(file) => file && date && onUpload(file, category, date)}>
        {(props) => (
          <Button {...props} leftSection={<IconUpload size={16} />} loading={uploading}>
            Upload file
          </Button>
        )}
      </FileButton>
    </Group>
  )
}
