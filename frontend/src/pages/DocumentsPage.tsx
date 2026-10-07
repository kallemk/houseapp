import {
  ActionIcon,
  Anchor,
  Badge,
  Card,
  Center,
  Checkbox,
  Group,
  Loader,
  Select,
  Stack,
  Table,
  Text,
  TextInput,
  ThemeIcon,
  Title,
} from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconDownload, IconEdit, IconFiles, IconSearch, IconTrash } from '@tabler/icons-react'
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
import { notifyDriveFailure } from '../components/documents/driveNotifications'
import { documentsApi } from '../api/documents'
import type { DocumentCategory, DocumentDto } from '../api/types'
import { DOCUMENT_CATEGORY_LABELS, DOCUMENT_CATEGORY_OPTIONS } from '../utils/labels'

const ALL = 'all'
/** Project filter value for documents not attached to any project. */
const NO_PROJECT = 'none'

const CATEGORY_COLORS: Record<DocumentCategory, string> = {
  Deed: 'terracotta',
  Warranty: 'blue',
  Receipt: 'green',
  Invoice: 'orange',
  Quote: 'cyan',
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
  const [search, setSearch] = useState('')
  const [year, setYear] = useState<string>(ALL)
  const [category, setCategory] = useState<string>(ALL)
  const [projectFilter, setProjectFilter] = useState<string>(ALL)
  const projectsById = new Map((projects ?? []).map((p) => [p.id, p]))

  const years = [...new Set((documents ?? []).map((d) => d.date.slice(0, 4)))].sort((a, b) => b.localeCompare(a))
  // Only projects that actually have documents — every project in the house would mostly be dead
  // options here. Sorted by name, since that's how people look for them.
  const projectsWithDocuments = [...new Set((documents ?? []).map((d) => d.projectId))]
    .flatMap((id) => (id && projectsById.has(id) ? [projectsById.get(id)!] : []))
    .sort((a, b) => a.name.localeCompare(b.name, 'sv'))

  // Title *and* filename: the title is what people chose to call it, but the filename is often what
  // they remember ("that PDF from the roofer").
  const trimmedSearch = search.trim().toLowerCase()
  const filtered = (documents ?? []).filter(
    (d) =>
      (year === ALL || d.date.startsWith(year)) &&
      (category === ALL || d.category === category) &&
      (projectFilter === ALL ||
        (projectFilter === NO_PROJECT ? d.projectId === null : d.projectId === projectFilter)) &&
      (trimmedSearch === '' ||
        (d.title ?? '').toLowerCase().includes(trimmedSearch) ||
        d.fileName.toLowerCase().includes(trimmedSearch)),
  )

  const { sorted, sortProps } = useTableSort(filtered, {
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
    return uploadDocument.mutateAsync(
      { file, ...meta },
      { onError: (error) => notifyDriveFailure(error, propertyId ?? '', 'Uppladdningen misslyckades. Försök igen.') },
    )
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
        <FileUpload onUpload={handleUpload} />
      </Card>

      {documents && documents.length > 0 && (
        <Card withBorder padding="md">
          <Group>
            <TextInput
              label="Sök"
              placeholder="Titel eller filnamn"
              leftSection={<IconSearch size={16} />}
              value={search}
              onChange={(e) => setSearch(e.currentTarget.value)}
              w={220}
            />
            <Select
              label="År"
              value={year}
              onChange={(value) => setYear(value ?? ALL)}
              allowDeselect={false}
              w={120}
              data={[{ value: ALL, label: 'Alla' }, ...years.map((y) => ({ value: y, label: y }))]}
            />
            <Select
              label="Kategori"
              value={category}
              onChange={(value) => setCategory(value ?? ALL)}
              allowDeselect={false}
              w={170}
              data={[{ value: ALL, label: 'Alla' }, ...DOCUMENT_CATEGORY_OPTIONS]}
            />
            <Select
              label="Projekt"
              value={projectFilter}
              onChange={(value) => setProjectFilter(value ?? ALL)}
              allowDeselect={false}
              searchable
              w={220}
              data={[
                { value: ALL, label: 'Alla' },
                { value: NO_PROJECT, label: 'Utan projekt' },
                ...projectsWithDocuments.map((p) => ({ value: p.id, label: p.name })),
              ]}
            />
          </Group>
        </Card>
      )}

      {filtered.length === 0 ? (
        <EmptyState
          icon={IconFiles}
          message={
            (documents ?? []).length === 0 ? 'Inga dokument uppladdade ännu.' : 'Inga dokument matchar sökningen.'
          }
        />
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
