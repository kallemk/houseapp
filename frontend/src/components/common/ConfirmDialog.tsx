import { Button, Group, Modal, Text } from '@mantine/core'

interface ConfirmDialogProps {
  opened: boolean
  title: string
  message: string
  confirmLabel?: string
  onConfirm: () => void
  onCancel: () => void
}

export function ConfirmDialog({ opened, title, message, confirmLabel = 'Ta bort', onConfirm, onCancel }: ConfirmDialogProps) {
  return (
    <Modal opened={opened} onClose={onCancel} title={title} centered>
      <Text mb="lg">{message}</Text>
      <Group justify="flex-end">
        <Button variant="default" onClick={onCancel}>
          Avbryt
        </Button>
        <Button color="red" onClick={onConfirm}>
          {confirmLabel}
        </Button>
      </Group>
    </Modal>
  )
}
