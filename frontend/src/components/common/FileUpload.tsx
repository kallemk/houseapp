import {
  ActionIcon,
  Alert,
  Button,
  Group,
  Image,
  Loader,
  Modal,
  Paper,
  Select,
  Stack,
  Text,
  TextInput,
  ThemeIcon,
} from '@mantine/core'
import { Dropzone } from '@mantine/dropzone'
import { notifications } from '@mantine/notifications'
import { IconAlertCircle, IconFile, IconSparkles, IconUpload, IconX } from '@tabler/icons-react'
import { useEffect, useRef, useState } from 'react'
import type { DocumentCategory } from '../../api/types'
import { suggestCategory, suggestTitle } from '../../utils/documentSuggestions'
import { DOCUMENT_CATEGORY_LABELS, DOCUMENT_CATEGORY_OPTIONS } from '../../utils/labels'
import type { UploadSuggestion } from '../../hooks/useUploadSuggestion'
import { MAX_FILE_BYTES, MAX_FILE_MB, SHORT_TEXT } from '../../utils/limits'

function todayIsoDate() {
  return new Date().toISOString().slice(0, 10)
}

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

export interface UploadMeta {
  category: DocumentCategory
  date: string
  /** Required — the dialog won't upload until every file has one. */
  title: string
}

interface FileUploadProps {
  /**
   * Uploads one file. Must reject on failure — the dialog keeps a failed file so it can be retried,
   * and stops there rather than carrying on. Reporting *why* it failed is the caller's job (it knows
   * about Drive connections and the like); the dialog only marks which file it was.
   */
  onUpload: (file: File, meta: UploadMeta) => Promise<unknown>
  /** Called once every file in the dialog has been uploaded. */
  onComplete?: () => void
  defaultDate?: string
  /**
   * Pre-fill from a link Claude made (see `useUploadSuggestion`). Overrides what the filename
   * suggests: the title goes to the first file dropped, the category and date to every file. The
   * drop area is scrolled into view and says what Claude suggested.
   */
  suggestion?: UploadSuggestion | null
}

interface PendingFile {
  key: string
  file: File
  title: string
  category: DocumentCategory
  date: string
  status: 'ready' | 'uploading' | 'failed'
}

/**
 * Pick or drop the files first, then confirm what to call them. The reverse order — type a title,
 * then choose the file — asked people to name something they hadn't picked yet.
 *
 * The details are asked in a modal rather than inline because this widget also lives inside the
 * project form: its own fields there once made the browser block saving the project over an empty
 * document title. Nothing in the dialog is a <form> either — the modal is portalled out of the DOM,
 * but React still bubbles a submit through the component tree to the project form's handler.
 */
export function FileUpload({ onUpload, onComplete, defaultDate, suggestion }: FileUploadProps) {
  const [pending, setPending] = useState<PendingFile[]>([])
  const dropRef = useRef<HTMLDivElement>(null)

  // Arriving from Claude's link: bring the drop area into view so the next step is obvious.
  // Keyed on whether there is one, not the object, which is rebuilt every render.
  const hasSuggestion = !!suggestion
  useEffect(() => {
    if (hasSuggestion) dropRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' })
  }, [hasSuggestion])
  const [uploading, setUploading] = useState(false)

  function addFiles(files: File[]) {
    const date = suggestion?.date ?? defaultDate ?? todayIsoDate()
    setPending((current) => [
      ...current,
      ...files.map((file, index) => ({
        key: `${file.name}-${file.size}-${file.lastModified}-${Math.random()}`,
        file,
        // Claude's title describes one document, so only the first file dropped gets it.
        title: (current.length === 0 && index === 0 && suggestion?.title) || suggestTitle(file.name),
        category: suggestion?.category ?? suggestCategory(file),
        date,
        status: 'ready' as const,
      })),
    ])
  }

  function update(key: string, changes: Partial<PendingFile>) {
    setPending((current) => current.map((p) => (p.key === key ? { ...p, ...changes } : p)))
  }

  function remove(key: string) {
    setPending((current) => current.filter((p) => p.key !== key))
  }

  const complete = pending.every((p) => p.title.trim().length > 0 && p.date)
  const anyFailed = pending.some((p) => p.status === 'failed')

  /**
   * One at a time, not in parallel. The first upload into a project creates its Drive folder, so two
   * at once could each create one; and when something fails (an expired Drive connection, typically)
   * every later file would fail the same way and stack up identical notifications.
   */
  async function uploadAll() {
    setUploading(true)
    for (const item of pending) {
      update(item.key, { status: 'uploading' })
      try {
        await onUpload(item.file, { category: item.category, date: item.date, title: item.title.trim() })
        remove(item.key)
      } catch {
        update(item.key, { status: 'failed' })
        setUploading(false)
        return
      }
    }
    setUploading(false)
    onComplete?.()
  }

  return (
    <>
      {suggestion && (
        <Alert color="violet" variant="light" icon={<IconSparkles size={16} />} mb="sm">
          <Text size="sm">
            Claude föreslår{suggestion.title ? <> <b>{suggestion.title}</b></> : ' ett dokument'}
            {suggestion.category && ` · ${DOCUMENT_CATEGORY_LABELS[suggestion.category]}`}
            {suggestion.date && ` · ${suggestion.date}`}. Släpp originalfilen här, så fylls det i åt dig.
          </Text>
        </Alert>
      )}
      <div ref={dropRef}>
      <Dropzone
        onDrop={addFiles}
        onReject={(rejections) =>
          notifications.show({
            color: 'red',
            message:
              rejections.length === 1
                ? `${rejections[0].file.name} är för stor – högst ${MAX_FILE_MB} MB per fil.`
                : `${rejections.length} filer är för stora – högst ${MAX_FILE_MB} MB per fil.`,
          })
        }
        maxSize={MAX_FILE_BYTES}
        multiple
        p="md"
        radius="md"
      >
        <Group justify="center" gap="sm" mih={48} style={{ pointerEvents: 'none' }}>
          <Dropzone.Accept>
            <IconUpload size={28} stroke={1.5} />
          </Dropzone.Accept>
          <Dropzone.Idle>
            <IconUpload size={28} stroke={1.5} />
          </Dropzone.Idle>
          <div>
            <Text size="sm" fw={500}>
              Ladda upp filer
            </Text>
            <Text size="xs" c="dimmed">
              Dra filerna hit eller klicka för att välja — du namnger dem i nästa steg. Högst {MAX_FILE_MB} MB per fil.
            </Text>
          </div>
        </Group>
      </Dropzone>
      </div>

      <Modal
        opened={pending.length > 0}
        onClose={() => setPending([])}
        title={pending.length === 1 ? 'Ladda upp fil' : `Ladda upp ${pending.length} filer`}
        size="lg"
        centered
        closeOnClickOutside={!uploading}
        closeOnEscape={!uploading}
        withCloseButton={!uploading}
      >
        <Stack>
          {anyFailed && (
            <Alert color="red" variant="light" icon={<IconAlertCircle size={18} />}>
              Uppladdningen stoppades vid den markerade filen. Filer ovanför den är redan sparade.
            </Alert>
          )}

          {pending.map((item) => (
            <PendingFileRow
              key={item.key}
              item={item}
              locked={uploading}
              removable={pending.length > 1}
              onChange={(changes) => update(item.key, changes)}
              onRemove={() => remove(item.key)}
            />
          ))}

          <Group justify="space-between">
            <Button variant="subtle" size="xs" disabled={uploading} component="label">
              Lägg till fler
              <input
                type="file"
                multiple
                hidden
                onChange={(e) => {
                  addFiles(Array.from(e.currentTarget.files ?? []))
                  e.currentTarget.value = ''
                }}
              />
            </Button>
            <Group gap="sm">
              <Button variant="default" disabled={uploading} onClick={() => setPending([])}>
                Avbryt
              </Button>
              <Button
                leftSection={<IconUpload size={16} />}
                loading={uploading}
                disabled={!complete}
                onClick={uploadAll}
              >
                {anyFailed ? 'Försök igen' : pending.length === 1 ? 'Ladda upp' : `Ladda upp ${pending.length}`}
              </Button>
            </Group>
          </Group>
        </Stack>
      </Modal>
    </>
  )
}

function PendingFileRow({
  item,
  locked,
  removable,
  onChange,
  onRemove,
}: {
  item: PendingFile
  locked: boolean
  removable: boolean
  onChange: (changes: Partial<PendingFile>) => void
  onRemove: () => void
}) {
  const isImage = item.file.type.startsWith('image/')
  const [preview, setPreview] = useState<string | null>(null)

  // A thumbnail is what makes a phone photo nameable — "IMG_4512" says nothing, the picture does.
  useEffect(() => {
    if (!isImage) return
    const url = URL.createObjectURL(item.file)
    setPreview(url)
    return () => URL.revokeObjectURL(url)
  }, [item.file, isImage])

  return (
    <Paper withBorder p="sm" radius="md" style={item.status === 'failed' ? { borderColor: 'var(--mantine-color-red-6)' } : undefined}>
      <Stack gap="xs">
        <Group justify="space-between" wrap="nowrap" gap="sm">
          <Group gap="sm" wrap="nowrap" style={{ minWidth: 0 }}>
            {preview ? (
              <Image src={preview} w={40} h={40} radius="sm" fit="cover" alt="" />
            ) : (
              <ThemeIcon variant="light" size={40} radius="sm">
                <IconFile size={20} />
              </ThemeIcon>
            )}
            <div style={{ minWidth: 0 }}>
              <Text size="sm" truncate>
                {item.file.name}
              </Text>
              <Text size="xs" c={item.status === 'failed' ? 'red' : 'dimmed'}>
                {item.status === 'failed' ? 'Kunde inte laddas upp' : formatSize(item.file.size)}
              </Text>
            </div>
          </Group>
          {item.status === 'uploading' ? (
            <Loader size="sm" />
          ) : (
            removable && (
              <ActionIcon variant="subtle" color="gray" title="Ta bort från listan" disabled={locked} onClick={onRemove}>
                <IconX size={16} />
              </ActionIcon>
            )
          )}
        </Group>

        {/* Enter is swallowed: nothing here is a form, and a stray Enter must not reach the project
            form this dialog may have been opened from. */}
        <TextInput
          label="Titel"
          placeholder="t.ex. Besiktningsprotokoll"
          withAsterisk
          maxLength={SHORT_TEXT}
          value={item.title}
          disabled={locked}
          onChange={(e) => onChange({ title: e.currentTarget.value })}
          onKeyDown={(e) => e.key === 'Enter' && e.preventDefault()}
          data-autofocus
        />
        <Group grow gap="sm">
          <Select
            label="Kategori"
            data={DOCUMENT_CATEGORY_OPTIONS}
            value={item.category}
            disabled={locked}
            onChange={(value) => onChange({ category: (value as DocumentCategory) ?? 'Other' })}
            allowDeselect={false}
          />
          <TextInput
            label="Datum"
            type="date"
            value={item.date}
            disabled={locked}
            onChange={(e) => onChange({ date: e.currentTarget.value })}
            onKeyDown={(e) => e.key === 'Enter' && e.preventDefault()}
          />
        </Group>
      </Stack>
    </Paper>
  )
}
