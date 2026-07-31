import { Button, Group, Modal, Stack, Text } from '@mantine/core'
import type { ReactNode } from 'react'

interface ConfirmDialogProps {
  opened: boolean
  title: string
  message: string
  confirmLabel?: string
  /** Extra controls between the message and the buttons — e.g. an opt-in checkbox for the action. */
  children?: ReactNode
  onConfirm: () => void
  onCancel: () => void
}

export function ConfirmDialog({
  opened,
  title,
  message,
  confirmLabel = 'Ta bort',
  children,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  return (
    <Modal opened={opened} onClose={onCancel} title={title} centered>
      <Stack gap="md">
        <Text>{message}</Text>
        {children}
        <Group justify="flex-end">
          <Button variant="default" onClick={onCancel}>
            Avbryt
          </Button>
          <Button color="red" onClick={onConfirm}>
            {confirmLabel}
          </Button>
        </Group>
      </Stack>
    </Modal>
  )
}
