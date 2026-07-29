import { ActionIcon, Anchor, Badge, Card, Group, Select, Stack, Table, Text, Title } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconPaperclip, IconX } from '@tabler/icons-react'
import { useState } from 'react'
import { documentsApi } from '../../api/documents'
import type { DocumentCategory } from '../../api/types'
import { useDocuments, useSetDocumentProject, useUploadDocument } from '../../hooks/useDocuments'
import { DOCUMENT_CATEGORY_LABELS } from '../../utils/labels'
import { FileUpload } from '../common/FileUpload'

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

  const attached = (documents ?? []).filter((d) => d.projectId === projectId)
  const unattached = (documents ?? []).filter((d) => d.projectId === null)

  function handleUpload(file: File, category: DocumentCategory, date: string) {
    uploadDocument.mutate(
      { file, category, date, projectId },
      { onError: () => notifications.show({ color: 'red', message: 'Uppladdningen misslyckades. Försök igen.' }) },
    )
  }

  function detach(id: string) {
    setDocumentProject.mutate({ id, projectId: null })
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
                      <Anchor onClick={() => documentsApi.download(doc.id, propertyId)} fw={500}>
                        {doc.fileName}
                      </Anchor>
                    </Table.Td>
                    <Table.Td>
                      <Badge variant="light">{DOCUMENT_CATEGORY_LABELS[doc.category]}</Badge>
                    </Table.Td>
                    <Table.Td c="dimmed">{doc.date}</Table.Td>
                    <Table.Td>
                      {/* Detaches only — deleting the file itself belongs on the documents page. */}
                      <ActionIcon variant="subtle" color="gray" title="Koppla loss" onClick={() => detach(doc.id)}>
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
                  setDocumentProject.mutate({ id: value, projectId })
                }
              }}
              data={unattached.map((d) => ({ value: d.id, label: `${d.fileName} (${d.date})` }))}
              leftSection={<IconPaperclip size={16} />}
              w={340}
            />
          </Group>
        )}
      </Stack>
    </Card>
  )
}
