import { ActionIcon, Anchor, Badge, Card, Center, Group, Loader, Stack, Table, Text, ThemeIcon, Title } from '@mantine/core'
import { IconDownload, IconFiles, IconTrash } from '@tabler/icons-react'
import { useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { EmptyState } from '../components/common/EmptyState'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { FileUpload, type UploadMeta } from '../components/common/FileUpload'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { useDeleteDocument, useDocuments, useUploadDocument } from '../hooks/useDocuments'
import { useProjects } from '../hooks/useProjects'
import { documentsApi } from '../api/documents'
import type { DocumentCategory } from '../api/types'
import { DOCUMENT_CATEGORY_LABELS } from '../utils/labels'

const CATEGORY_COLORS: Record<DocumentCategory, string> = {
  Deed: 'terracotta',
  Warranty: 'blue',
  Receipt: 'green',
  Photo: 'grape',
  Other: 'gray',
}

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export function DocumentsPage() {
  const { propertyId } = useParams<{ propertyId: string }>()
  const { property, isLoading: loadingProperty, notFound } = useSelectedProperty(propertyId)
  const { data: documents, isLoading } = useDocuments(propertyId ?? '')
  const uploadDocument = useUploadDocument(propertyId ?? '')
  const deleteDocument = useDeleteDocument(propertyId ?? '')
  const { data: projects } = useProjects(propertyId ?? '')
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null)
  const projectsById = new Map((projects ?? []).map((p) => [p.id, p]))

  if (loadingProperty || isLoading) {
    return (
      <Center py="xl">
        <Loader />
      </Center>
    )
  }

  if (notFound || !property) {
    return <Navigate to="/properties" replace />
  }

  function handleUpload(file: File, meta: UploadMeta) {
    uploadDocument.mutate({ file, ...meta })
  }

  return (
    <Stack>
      <Group gap="sm">
        <ThemeIcon variant="light" size={36} radius="md">
          <IconFiles size={20} />
        </ThemeIcon>
        <Title order={2}>Dokument</Title>
      </Group>

      <Card withBorder padding="md">
        <FileUpload onUpload={handleUpload} uploading={uploadDocument.isPending} />
      </Card>

      {!documents || documents.length === 0 ? (
        <EmptyState icon={IconFiles} message="Inga dokument uppladdade ännu." />
      ) : (
        <Card withBorder padding={0} style={{ overflow: 'hidden' }}>
          <Table.ScrollContainer minWidth={760}>
            <Table striped highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Namn</Table.Th>
                  <Table.Th>Kategori</Table.Th>
                  <Table.Th>Projekt</Table.Th>
                  <Table.Th>Storlek</Table.Th>
                  <Table.Th>Datum</Table.Th>
                  <Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {documents.map((doc) => (
                  <Table.Tr key={doc.id}>
                    <Table.Td>
                      <Anchor onClick={() => documentsApi.download(doc.id, property.id)} fw={500}>
                        {doc.title ?? doc.fileName}
                      </Anchor>
                      {/* Keep the filename visible when a title replaces it — it's still what
                          actually lands on disk when you download. */}
                      {doc.title && (
                        <Text size="xs" c="dimmed">
                          {doc.fileName}
                        </Text>
                      )}
                    </Table.Td>
                    <Table.Td>
                      <Badge color={CATEGORY_COLORS[doc.category]} variant="light">
                        {DOCUMENT_CATEGORY_LABELS[doc.category]}
                      </Badge>
                    </Table.Td>
                    <Table.Td>
                      {doc.projectId && projectsById.has(doc.projectId) ? (
                        <Anchor component={Link} to={`/properties/${property.id}/projects/${doc.projectId}`} size="sm">
                          {projectsById.get(doc.projectId)!.name}
                        </Anchor>
                      ) : (
                        <Text c="dimmed" size="sm">
                          —
                        </Text>
                      )}
                    </Table.Td>
                    <Table.Td c="dimmed">{formatSize(doc.sizeBytes)}</Table.Td>
                    <Table.Td c="dimmed">{doc.date}</Table.Td>
                    <Table.Td>
                      <ActionIcon variant="subtle" onClick={() => documentsApi.download(doc.id, property.id)} mr="xs">
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
          </Table.ScrollContainer>
        </Card>
      )}

      <ConfirmDialog
        opened={pendingDeleteId !== null}
        title="Ta bort dokument"
        message="Detta tar bort filen och dess post. Detta kan inte ångras."
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
