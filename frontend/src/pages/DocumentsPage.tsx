import {
  ActionIcon,
  Anchor,
  Badge,
  Card,
  Center,
  Checkbox,
  Group,
  Loader,
  Stack,
  Table,
  Text,
  ThemeIcon,
  Title,
} from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconDownload, IconEdit, IconFiles, IconTrash } from '@tabler/icons-react'
import { useEffect, useState } from 'react'
import { Link, Navigate, useParams, useSearchParams } from 'react-router-dom'
import { EmptyState } from '../components/common/EmptyState'
import { SortableTh } from '../components/common/SortableTh'
import { useTableSort } from '../hooks/useTableSort'
import { ConfirmDialog } from '../components/common/ConfirmDialog'
import { FileUpload, type UploadMeta } from '../components/common/FileUpload'
import { useSelectedProperty } from '../hooks/useSelectedProperty'
import { useDeleteDocument, useDocuments, useUploadDocument } from '../hooks/useDocuments'
import { useProjects } from '../hooks/useProjects'
import { DriveConnectionCard } from '../components/documents/DriveConnectionCard'
import { EditDocumentModal } from '../components/documents/EditDocumentModal'
import { documentsApi } from '../api/documents'
import type { DocumentCategory, DocumentDto } from '../api/types'
import { DOCUMENT_CATEGORY_LABELS } from '../utils/labels'

const CATEGORY_COLORS: Record<DocumentCategory, string> = {
  Deed: 'terracotta',
  Warranty: 'blue',
  Receipt: 'green',
  Invoice: 'orange',
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
  const [pendingDelete, setPendingDelete] = useState<DocumentDto | null>(null)
  const [alsoDeleteFromDrive, setAlsoDeleteFromDrive] = useState(false)
  const [editing, setEditing] = useState<DocumentDto | null>(null)
  const [searchParams, setSearchParams] = useSearchParams()
  const projectsById = new Map((projects ?? []).map((p) => [p.id, p]))
  const { sorted, sortProps } = useTableSort(documents ?? [], {
    name: (d) => d.title ?? d.fileName,
    category: (d) => DOCUMENT_CATEGORY_LABELS[d.category],
    project: (d) => (d.projectId ? projectsById.get(d.projectId)?.name : null),
    size: (d) => d.sizeBytes,
    date: (d) => d.date,
  })

  // The Drive callback redirects back here with the outcome, since an OAuth round trip leaves the
  // app entirely and can't resolve a promise.
  const driveOutcome = searchParams.get('drive')
  useEffect(() => {
    if (!driveOutcome) return
    const messages: Record<string, { color: string; message: string }> = {
      connected: { color: 'green', message: 'Google Drive är anslutet. Nya dokument sparas där.' },
      cancelled: { color: 'yellow', message: 'Anslutningen till Google Drive avbröts.' },
      failed: { color: 'red', message: 'Kunde inte ansluta till Google Drive. Försök igen.' },
    }
    const feedback = messages[driveOutcome]
    if (feedback) {
      notifications.show(feedback)
    }
    // Cleared so a refresh doesn't re-announce it.
    searchParams.delete('drive')
    setSearchParams(searchParams, { replace: true })
  }, [driveOutcome, searchParams, setSearchParams])

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

      <DriveConnectionCard property={property} />

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
                  <SortableTh {...sortProps('name')}>Namn</SortableTh>
                  <SortableTh {...sortProps('category')}>Kategori</SortableTh>
                  <SortableTh {...sortProps('project')}>Projekt</SortableTh>
                  <SortableTh {...sortProps('size')}>Storlek</SortableTh>
                  <SortableTh {...sortProps('date')}>Datum</SortableTh>
                  <Table.Th />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {sorted.map((doc) => (
                  <Table.Tr key={doc.id}>
                    <Table.Td>
                      <Anchor onClick={() => documentsApi.download(doc)} fw={500}>
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
                      <ActionIcon variant="subtle" onClick={() => documentsApi.download(doc)} mr="xs">
                        <IconDownload size={16} />
                      </ActionIcon>
                      <ActionIcon variant="subtle" title="Redigera" onClick={() => setEditing(doc)} mr="xs">
                        <IconEdit size={16} />
                      </ActionIcon>
                      <ActionIcon color="red" variant="subtle" onClick={() => setPendingDelete(doc)}>
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

      <EditDocumentModal
        document={editing}
        propertyId={property.id}
        onClose={() => setEditing(null)}
      />

      <ConfirmDialog
        opened={pendingDelete !== null}
        title="Ta bort dokument"
        message={
          pendingDelete?.storageKind === 'Drive'
            ? 'Dokumentet tas bort från appen. Filen ligger kvar i Google Drive om du inte kryssar i rutan nedan.'
            : 'Detta tar bort filen och dess post. Detta kan inte ångras.'
        }
        onCancel={() => {
          setPendingDelete(null)
          setAlsoDeleteFromDrive(false)
        }}
        onConfirm={() => {
          if (pendingDelete) {
            deleteDocument.mutate(
              { id: pendingDelete.id, deleteFromDrive: alsoDeleteFromDrive },
              {
                onError: () =>
                  notifications.show({ color: 'red', message: 'Kunde inte ta bort dokumentet. Försök igen.' }),
              },
            )
          }
          setPendingDelete(null)
          setAlsoDeleteFromDrive(false)
        }}
      >
        {/* Opt-in, and unticked every time: the file is in someone's personal Drive, so removing it
            is a separate decision from removing the app's record of it. */}
        {pendingDelete?.storageKind === 'Drive' && (
          <Checkbox
            label="Ta bort även filen från Google Drive"
            checked={alsoDeleteFromDrive}
            onChange={(e) => setAlsoDeleteFromDrive(e.currentTarget.checked)}
          />
        )}
      </ConfirmDialog>
    </Stack>
  )
}
