import { ActionIcon, Anchor, Center, Loader, Stack, Table, Title } from '@mantine/core'
import { IconDownload, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { EmptyState } from '../components/common/EmptyState'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { FileUpload } from '../components/common/FileUpload'
import { usePrimaryProperty } from '../hooks/usePrimaryProperty'
import { useDeleteDocument, useDocuments, useUploadDocument } from '../hooks/useDocuments'
import { documentsApi } from '../api/documents'
import type { DocumentCategory } from '../api/types'

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export function DocumentsPage() {
  const { property, isLoading: loadingProperty } = usePrimaryProperty()
  const propertyId = property?.id ?? ''
  const { data: documents, isLoading } = useDocuments(propertyId)
  const uploadDocument = useUploadDocument(propertyId)
  const deleteDocument = useDeleteDocument(propertyId)
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)

  if (loadingProperty || isLoading) {
    return (
      <Center py="xl">
        <Loader />
      </Center>
    )
  }

  if (!property) {
    return <EmptyState message="Add a property on the Dashboard first." />
  }

  function handleUpload(file: File, category: DocumentCategory) {
    uploadDocument.mutate({ file, category })
  }

  return (
    <Stack>
      <Title order={2}>Documents</Title>

      <FileUpload onUpload={handleUpload} uploading={uploadDocument.isPending} />

      {!documents || documents.length === 0 ? (
        <EmptyState message="No documents uploaded yet." />
      ) : (
        <Table striped highlightOnHover>
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Name</Table.Th>
              <Table.Th>Category</Table.Th>
              <Table.Th>Size</Table.Th>
              <Table.Th>Uploaded</Table.Th>
              <Table.Th />
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {documents.map((doc) => (
              <Table.Tr key={doc.id}>
                <Table.Td>
                  <Anchor onClick={() => documentsApi.download(doc.id, propertyId)}>{doc.fileName}</Anchor>
                </Table.Td>
                <Table.Td>{doc.category}</Table.Td>
                <Table.Td>{formatSize(doc.sizeBytes)}</Table.Td>
                <Table.Td>{new Date(doc.uploadedAt).toLocaleDateString()}</Table.Td>
                <Table.Td>
                  <ActionIcon variant="subtle" onClick={() => documentsApi.download(doc.id, propertyId)} mr="xs">
                    <IconDownload size={16} />
                  </ActionIcon>
                  <ActionIcon color="red" variant="subtle" onClick={() => setPendingDeleteId(doc.id)}>
                    <IconTrash size={16} />
                  </ActionIcon>
                </Table.Td>
              </Table.Tr>
            ))}
          </Table.Tbody>
        </Table>
      )}

      <ConfirmDialog
        opened={pendingDeleteId !== null}
        title="Delete document"
        message="This removes the file and its record. This can't be undone."
        onCancel={() => setPendingDeleteId(null)}
        onConfirm={() => {
          if (pendingDeleteId) {
            deleteDocument.mutate(pendingDeleteId)
          }
          setPendingDeleteId(null)
        }}
      />
    </Stack>
  )
}
