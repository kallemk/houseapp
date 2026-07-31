import { Alert, Anchor, Button, Group, Text } from '@mantine/core'
import { notifications } from '@mantine/notifications'
import { IconBrandGoogleDrive, IconCloud, IconExternalLink } from '@tabler/icons-react'
import { useState } from 'react'
import { driveApi } from '../../api/documents'
import type { PropertyDto } from '../../api/types'
import { ConfirmDialog } from '../common/ConfirmDialog'
import { useDisconnectDrive } from '../../hooks/useDocuments'

/**
 * Says where this property's documents are kept, and lets a member change it.
 *
 * Members only: connecting binds *your* Google account's Drive to the property, and every upload
 * afterwards goes through that grant — not something someone who is only here because it's the demo
 * property should be able to do.
 */
export function DriveConnectionCard({ property }: { property: PropertyDto }) {
  const disconnect = useDisconnectDrive(property.id)
  const [confirmingDisconnect, setConfirmingDisconnect] = useState(false)
  const connected = property.documentStorage === 'Drive'

  if (!property.isMember) {
    return null
  }

  return (
    <>
      <Alert
        variant="light"
        color={connected ? 'teal' : 'gray'}
        icon={connected ? <IconBrandGoogleDrive size={18} /> : <IconCloud size={18} />}
      >
        <Group justify="space-between" wrap="wrap" gap="sm">
          <div>
            {connected ? (
              <>
                <Text size="sm">
                  Dokument sparas i Google Drive
                  {property.driveConnectedByName && ` via ${property.driveConnectedByName}`}.
                </Text>
                {property.driveFolderUrl && (
                  <Anchor href={property.driveFolderUrl} target="_blank" rel="noreferrer" size="xs">
                    <Group gap={4}>
                      <IconExternalLink size={12} />
                      Öppna mappen i Drive
                    </Group>
                  </Anchor>
                )}
              </>
            ) : (
              <Text size="sm">
                Dokument sparas i appens egen lagring. Du kan istället spara dem i din Google Drive.
              </Text>
            )}
          </div>
          {connected ? (
            <Button
              variant="default"
              size="xs"
              loading={disconnect.isPending}
              onClick={() => setConfirmingDisconnect(true)}
            >
              Koppla från
            </Button>
          ) : (
            <Button
              variant="light"
              size="xs"
              leftSection={<IconBrandGoogleDrive size={14} />}
              onClick={() => driveApi.connect(property.id)}
            >
              Anslut Google Drive
            </Button>
          )}
        </Group>
      </Alert>

      <ConfirmDialog
        opened={confirmingDisconnect}
        title="Koppla från Google Drive"
        confirmLabel="Koppla från"
        message="Nya dokument sparas i appens egen lagring igen. Mappen och filerna i Google Drive rörs inte, och dokument som redan laddats upp går fortfarande att öppna."
        onCancel={() => setConfirmingDisconnect(false)}
        onConfirm={() => {
          setConfirmingDisconnect(false)
          disconnect.mutate(undefined, {
            onError: () =>
              notifications.show({ color: 'red', message: 'Kunde inte koppla från. Försök igen.' }),
          })
        }}
      />
    </>
  )
}
