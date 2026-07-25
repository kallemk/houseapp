import { Button, FileButton, Group, Select } from '@mantine/core'
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

interface FileUploadProps {
  onUpload: (file: File, category: DocumentCategory) => void
  uploading?: boolean
}

export function FileUpload({ onUpload, uploading }: FileUploadProps) {
  const [category, setCategory] = useState<DocumentCategory>('Other')

  return (
    <Group>
      <Select
        data={CATEGORY_OPTIONS}
        value={category}
        onChange={(value) => setCategory((value as DocumentCategory) ?? 'Other')}
        allowDeselect={false}
        w={160}
      />
      <FileButton onChange={(file) => file && onUpload(file, category)}>
        {(props) => (
          <Button {...props} leftSection={<IconUpload size={16} />} loading={uploading}>
            Upload file
          </Button>
        )}
      </FileButton>
    </Group>
  )
}
