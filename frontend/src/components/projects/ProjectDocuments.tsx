import { ActionIcon, Anchor, Badge, Card, Group, Select, Stack, Table, Text, Title } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconEdit, IconPaperclip, IconX } from '@tabler/icons-react'
import { useState } from 'react'
import { documentsApi } from '../../api/documents'
import type { DocumentDto } from '../../api/types'
import { EditDocumentModal } from '../documents/EditDocumentModal'
import { ApiError } from '../../api/client'
import { useDocuments, useSetDocumentProject, useUploadDocument } from '../../hooks/useDocuments'
import { DOCUMENT_CATEGORY_LABELS } from '../../utils/labels'
import { FileUpload, type UploadMeta } from '../common/FileUpload'

/**
 * Attachments live in the `documents` container, not inside the project document, so they're saved
 * immediately rather than as part of the project form — uploading here takes effect straight away
 * even if the surrounding form is never submitted. That's why this needs a saved project to attach
 * to, and isn't rendered while creating one.
 */
export function ProjectDocuments({ propertyId, projectId }: { propertyId: string; projectId: string }) {
  const { data: documents } = useDocuments(propertyId)
  const uploadDocument = useUploadDocument(propertyId)
  const setDocumentProject = useSetDocumentProject(propertyId)
  const [attachingId, setAttachingId] = useState<string | null>(null)
  const [editing, setEditing] = useState<DocumentDto | null>(null)

  const attached = (documents ?? []).filter((d) => d.projectId === projectId)
  const unattached = (documents ?? []).filter((d) => d.projectId === null)

  function handleUpload(file: File, meta: UploadMeta) {
    uploadDocument.mutate(
      { file, ...meta, projectId },
      { onError: () => notifications.show({ color: 'red', message: 'Uppladdningen misslyckades. Försök igen.' }) },
    )
  }

  /**
   * Attaching and detaching also re-file the document in Google Drive, so these can now fail with a
   * dead Drive connection where they used to be a pure metadata write. Silence would look like the
   * click simply not registering.
   */
  function moveTo(id: string, target: string | null) {
    setDocumentProject.mutate(
      { id, projectId: target },
      {
        onError: (error) =>
          notifications.show({
            color: 'red',
            message:
              error instanceof ApiError && error.status === 409
                ? 'Google Drive-anslutningen behöver förnyas innan dokumentet kan flyttas.'
                : 'Kunde inte ändra dokumentets koppling. Försök igen.',
          }),
      },
    )
  }

  return (
    <Card withBorder padding="lg">
      <Stack>
        <Title order={5}>Dokument</Title>
        <Text size="xs" c="dimmed">
          Filer som laddas upp här kopplas direkt till projektet och sparas med en gång — de syns
          även på dokumentsidan.
        </Text>

        <FileUpload onUpload={handleUpload} uploading={uploadDocument.isPending} />

        {attached.length > 0 && (
          <Table.ScrollContainer minWidth={420}>
            <Table verticalSpacing="xs">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Namn</Table.Th>
                  <Table.Th w={140}>Kategori</Table.Th>
                  <Table.Th w={130}>Datum</Table.Th>
                  <Table.Th w={50} />
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {attached.map((doc) => (
                  <Table.Tr key={doc.id}>
                    <Table.Td>
                      <Anchor onClick={() => documentsApi.download(doc)} fw={500}>
                        {doc.title ?? doc.fileName}
                      </Anchor>
                    </Table.Td>
                    <Table.Td>
                      <Badge variant="light">{DOCUMENT_CATEGORY_LABELS[doc.category]}</Badge>
                    </Table.Td>
                    <Table.Td c="dimmed">{doc.date}</Table.Td>
                    <Table.Td>
                      <ActionIcon variant="subtle" color="gray" title="Redigera" onClick={() => setEditing(doc)}>
                        <IconEdit size={16} />
                      </ActionIcon>
                      {/* Detaches only — deleting the file itself belongs on the documents page. */}
                      <ActionIcon variant="subtle" color="gray" title="Koppla loss" onClick={() => moveTo(doc.id, null)}>
                        <IconX size={16} />
                      </ActionIcon>
                    </Table.Td>
                  </Table.Tr>
                ))}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        )}

        {unattached.length > 0 && (
          <Group align="flex-end">
            <Select
              label="Koppla ett befintligt dokument"
              placeholder="Välj dokument"
              searchable
              value={attachingId}
              onChange={(value) => {
                setAttachingId(null)
                if (value) {
                  moveTo(value, projectId)
                }
              }}
              data={unattached.map((d) => ({ value: d.id, label: `${d.title ?? d.fileName} (${d.date})` }))}
              leftSection={<IconPaperclip size={16} />}
              w={340}
            />
          </Group>
        )}
      </Stack>

      <EditDocumentModal document={editing} propertyId={propertyId} onClose={() => setEditing(null)} />
    </Card>
  )
}
